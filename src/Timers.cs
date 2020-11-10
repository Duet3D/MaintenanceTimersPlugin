using DuetAPI.ObjectModel;
using DuetAPIClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaintenanceTimersPlugin
{
    /// <summary>
    /// Main class for timer management
    /// </summary>
    public static class Timers
    {
        /// <summary>
        /// List of configured mainetenance timers
        /// </summary>
        public static List<MaintenanceTimer> TimerList { get; private set; } = new List<MaintenanceTimer>();

        /// <summary>
        /// Task to load timers from the configured file
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public static async Task Load()
        {
            if (File.Exists(Program.TimersFile))
            {
                using FileStream fileStream = new FileStream(Program.TimersFile, FileMode.Open, FileAccess.Read);
                TimerList = await JsonSerializer.DeserializeAsync<List<MaintenanceTimer>>(fileStream);
            }
        }

        /// <summary>
        /// Save the timers to the configured file
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public static async Task Save()
        {
            using FileStream fileStream = new FileStream(Program.TimersFile, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(fileStream, TimerList);
        }

        /// <summary>
        /// Check the timers every minute
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public static async Task CheckContinuously()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await Task.Delay(-1, Program.CancelSource.Token);
                return;
            }

            do
            {
                try
                {
                    using CommandConnection commandConnection = new CommandConnection();
                    await commandConnection.Connect(Program.SocketPath, Program.CancelSource.Token);
                    await commandConnection.SetPluginData("timers", TimerList);

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
                                    if (!await commandConnection.EvaluateExpression<bool>(condition))
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
                            await commandConnection.SetPluginData("timers", TimerList);
                            await Save();
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
    }
}
