using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MaintenanceTimersPlugin
{
    public static class Program
    {
        /// <summary>
        /// Version of this application
        /// </summary>
        public static readonly string Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        /// Cancellation source used to terminate this application
        /// </summary>
        public static readonly CancellationTokenSource CancelSource = new CancellationTokenSource();

        /// <summary>
        /// Path to the UNIX socket provided by DCS
        /// </summary>
        public static string SocketPath = DuetAPI.Connection.Defaults.FullSocketPath;

        /// <summary>
        /// Path to the timer list to use
        /// </summary>
        public static string TimersFile = Path.Combine(Directory.GetCurrentDirectory(), "timers.json");

        /// <summary>
        /// Run this application in non-SPI mode (i.e. evaluate conditions internally)
        /// </summary>
        public static bool NoSpi = false;

        /// <summary>
        /// Entry point of this application
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        static async Task Main(string[] args)
        {
            Console.WriteLine($"Maintenance Timers Plugin v{Version}");
            Console.WriteLine("Written by Christian Hammacher for Sole Printer");

            // Parse command-line arguments
            string lastArg = string.Empty;
            foreach (string arg in args)
            {
                if (lastArg == "-s" || lastArg == "--socket-file")
                {
                    SocketPath = arg;
                }
                else if (lastArg == "-t" || lastArg == "--timers-file")
                {
                    TimersFile = arg;
                }
                else if (lastArg == "-D" || lastArg == "--no-spi")
                {
                    NoSpi = true;
                }
                lastArg = arg;
            }

            // Load the timers
            await Timers.Load();

            // Deal with program termination requests (SIGTERM and Ctrl+C)
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                if (!CancelSource.IsCancellationRequested)
                {
                    Console.WriteLine("[warn] Received SIGTERM, shutting down...");
                    CancelSource.Cancel();
                }
            };
            Console.CancelKeyPress += (sender, e) =>
            {
                if (!CancelSource.IsCancellationRequested)
                {
                    Console.WriteLine("[warn] Received SIGINT, shutting down...");
                    e.Cancel = true;
                    CancelSource.Cancel();
                }
            };

            // Create the main tasks
            Task modelObserverTask = NoSpi ? Task.Factory.StartNew(ModelObserver.Run, TaskCreationOptions.LongRunning).Unwrap() : Task.Delay(-1, Program.CancelSource.Token);
            Task timersTask = Task.Factory.StartNew(Timers.CheckContinuously, TaskCreationOptions.LongRunning).Unwrap();

            // Run this application and wait for the first task to be terminated
            Task terminatedTask = await Task.WhenAny(modelObserverTask, timersTask);
            if (terminatedTask.IsFaulted && !CancelSource.IsCancellationRequested)
            {
                Console.WriteLine("[err] Unhandled exception: {0}", terminatedTask.Exception);
            }
            CancelSource.Cancel();

            // Wait for the remaining tasks to exit
            try
            {
                await Task.WhenAll(modelObserverTask, timersTask);
            }
            catch
            {
                // ignored
            }
        }
    }
}
