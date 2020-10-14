using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaintenanceTimersPlugin
{
    public static class Timers
    {
        /// <summary>
        /// List of configured mainetenance timers
        /// </summary>
        public static List<MaintenanceTimer> TimerList { get; private set; }

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
            do
            {
                bool ruleChanged = false;

                // TODO build command connection, evaluate rules, increment them where necessary, and run requested actions
                if (Program.NoSpi)
                {
                    // TODO use https://github.com/davideicardi/DynamicExpresso/ to evaluate conditions in non-SPI mode,
                    // pass copy of cached object model first
                }
                else
                {
                    // TODO use DSF to evaluate expressions
                }
                // TODO save rules if any changed and update the object model

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            while (!Program.CancelSource.IsCancellationRequested);
        }
    }
}
