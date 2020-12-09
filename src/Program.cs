using System;
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
        public static string TimersFile = "../timers.json";

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
            Console.WriteLine("Written by Duet3D Ltd");

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

            // Keep the timers ticking...
            try
            {
                await Timers.CheckContinuously();
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException) || !Program.CancelSource.IsCancellationRequested)
                {
                    Console.WriteLine("[err] Unhandled exception: {0}", e);
                }
            }
        }
    }
}
