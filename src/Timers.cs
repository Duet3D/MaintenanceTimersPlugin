using DuetAPI.ObjectModel;
using DuetAPIClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace MaintenanceTimersPlugin;

/// <summary>
/// Main class for timer management
/// </summary>
public static class Timers
{
    /// <summary>
    /// List of configured mainetenance timers
    /// </summary>
    public static List<MaintenanceTimer> TimerList { get; private set; } = [];

    /// <summary>
    /// Load timers from the given file
    /// </summary>
    /// <returns>True on success</returns>
    public static async Task<bool> LoadAsync(string filename)
    {
        try
        {
            if (File.Exists(filename))
            {
                using FileStream fileStream = new(filename, FileMode.Open, FileAccess.Read);
                TimerList = (List<MaintenanceTimer>)await JsonSerializer.DeserializeAsync(fileStream, typeof(List<MaintenanceTimer>), JsonContext.Default);
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[error] Failed to load timers from file '{0}': {1}", filename, e);
        }
        return false;
    }

    /// <summary>
    /// Save the timers to the configured file
    /// </summary>
    /// <returns>Asynchronous task</returns>
    public static async Task SaveAsync()
    {
        // Move old file to backup
        if (File.Exists(Program.TimersFile))
        {
            File.Move(Program.TimersFile, Program.TimersBackupFile, true);
        }

        // Save new file
        using FileStream fileStream = new(Program.TimersFile, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
    }

    /// <summary>
    /// Check the timers every minute
    /// </summary>
    /// <returns>Asynchronous task</returns>
    public static async Task CheckContinuouslyAsync()
    {
        do
        {
            try
            {
                using CommandConnection commandConnection = new();
                await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                JsonElement pluginData = JsonSerializer.SerializeToElement(TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
                await commandConnection.SetPluginData("timers", pluginData);
                await RegisterResetEndpoint(commandConnection);

                do
                {
                    DateTime startTime = DateTime.Now;

                    // Check the timers
                    bool timersChanged = false;
                    foreach (MaintenanceTimer timer in TimerList)
                    {
                        bool conditionsMet = true;
                        foreach (string condition in timer.Conditions)
                        {
                            try
                            {
                                if (!(await commandConnection.EvaluateExpression(condition)).GetBoolean())
                                {
                                    conditionsMet = false;
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                conditionsMet = false;
                                await commandConnection.WriteMessage(MessageType.Error, $"Failed to evaluate condition '{condition}' of timer {timer.Name}: {e.Message}", true, LogLevel.Warn);
                                Console.WriteLine("[error] Failed to evaluate condition '{0}' of timer {1}: {2}", condition, timer.Name, e);
                            }
                        }

                        if (conditionsMet)
                        {
                            bool timerChanged = false;
                            if (timer.InitialValue == 0)
                            {
                                timer.Value++;
                                timerChanged = timersChanged = true;
                            }
                            else if (timer.Value > 0)
                            {
                                timer.Value--;
                                timerChanged = timersChanged = true;
                            }

                            if (timerChanged)
                            {
                                Console.WriteLine("[info] Timer {0} has been updated", timer.Name);
                                if (timer.Value == timer.ThresholdValue && !string.IsNullOrEmpty(timer.Action))
                                {
                                    string result = await commandConnection.PerformSimpleCode(timer.Action);
                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        // Output the code result if applicable
                                        await commandConnection.WriteMessage(MessageType.Success, result, true, LogLevel.Info);
                                    }
                                }
                            }
                        }
                    }

                    // Apply new values if anything has changed
                    if (timersChanged)
                    {
                        pluginData = JsonSerializer.SerializeToElement(TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
                        await commandConnection.SetPluginData("timers", pluginData);
                        await SaveAsync();
                    }
                    else
                    {
                        commandConnection.Poll();
                    }

                    // Wait one minute
                    await Task.Delay(TimeSpan.FromMinutes(1) - (DateTime.Now - startTime), Program.CancelSource.Token);
                }
                while (!Program.CancelSource.IsCancellationRequested);
            }
            catch (SocketException)
            {
                Console.WriteLine("[warn] Failed to connect to DCS");
                await Task.Delay(2000, Program.CancelSource.Token);
            }
        }
        while (!Program.CancelSource.IsCancellationRequested);
    }

    /// <summary>
    /// Registers an http endpoint for this plugin
    /// </summary>
    /// <param name="commandConnection">The current instantiated command connection</param>
    public static async Task RegisterResetEndpoint(CommandConnection commandConnection)
    {
        // This is the reset endpoint which resets a selected timer. Expected format is /machine/MaintenanceTimers/Reset?timerName={Name}
        Console.WriteLine("[info] Registering Reset Endpoint");
        var resetEndpoint = await commandConnection.AddHttpEndpoint(HttpEndpointType.PUT, "MaintenanceTimers", "Reset");
        resetEndpoint.OnEndpointRequestReceived += async (HttpEndpointUnixSocket unixSocket, HttpEndpointConnection requestConnection) =>
        {
            var request = await requestConnection.ReadRequest();

            //Request is missing the timerName query string. Fail with 400 error
            if (!request.Queries.ContainsKey("timerName"))
            {
                await requestConnection.SendResponse(400);
                return;
            }

            var timer = TimerList.FirstOrDefault(t => t.Name == request.Queries["timerName"]);

            //A timer with a maching name was not found. Fail with 400 error
            if (timer == null)
            {
                await requestConnection.SendResponse(400);
                return;
            }

            if (!timer.CanReset)
            {
                await requestConnection.SendResponse(403);
                return;
            }

            timer.Value = timer.InitialValue;
            JsonElement pluginData = JsonSerializer.SerializeToElement(TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
            await commandConnection.SetPluginData("timers", pluginData);
            await SaveAsync();
            await commandConnection.WriteMessage(MessageType.Success, $"Reset '{timer.Title}'", true, LogLevel.Info);
            await requestConnection.SendResponse();
        };

        //This is a test endpoint to change time. It adds 1 hour to each of the timers.
        var testEndpoint = await commandConnection.AddHttpEndpoint(HttpEndpointType.PUT, "MaintenanceTimers", "test");
        testEndpoint.OnEndpointRequestReceived += async (HttpEndpointUnixSocket unixSocket, HttpEndpointConnection requestConnection) =>
        {
            foreach (var timer in TimerList)
            {
                timer.Value += 60;
            }

            JsonElement pluginData = JsonSerializer.SerializeToElement(TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
            await commandConnection.SetPluginData("timers", pluginData);
            await SaveAsync();

            await requestConnection.SendResponse();
        };

        //This test endpoint sets all the timers to 1 minute.
        var resetToOneEndpoint = await commandConnection.AddHttpEndpoint(HttpEndpointType.PUT, "MaintenanceTimers", "testone");
        resetToOneEndpoint.OnEndpointRequestReceived += async (HttpEndpointUnixSocket unixSocket, HttpEndpointConnection requestConnection) =>
        {
            foreach (var timer in TimerList)
            {
                timer.Value = timer.InitialValue;
            }

            Console.WriteLine("[info] Save Timer Updates");
            JsonElement pluginData = JsonSerializer.SerializeToElement(TimerList, typeof(List<MaintenanceTimer>), JsonContext.Default);
            await commandConnection.SetPluginData("timers", pluginData);
            await SaveAsync();
            await commandConnection.WriteMessage(MessageType.Success, $"Setting timers to 1 minute", true, LogLevel.Info);

            await requestConnection.SendResponse();
            requestConnection.Close();
            Console.WriteLine("Test Update");
        };
    }
}

/// <summary>
/// Context for JSON handling
/// </summary>
[JsonSerializable(typeof(List<MaintenanceTimer>))]
[JsonSourceGenerationOptions(PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate)]
public sealed partial class JsonContext : JsonSerializerContext
{
    static JsonContext() => Default = new JsonContext(CreateJsonSerializerOptions(Default));

    private static JsonSerializerOptions CreateJsonSerializerOptions(JsonContext defaultContext) => new(defaultContext.GeneratedSerializerOptions!)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
