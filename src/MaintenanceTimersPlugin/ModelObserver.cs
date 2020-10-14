using DuetAPI.ObjectModel;
using Nito.AsyncEx;
using System;
using System.Threading.Tasks;

namespace MaintenanceTimersPlugin
{
    /// <summary>
    /// Static class used to synchronize the object model in non-SPI mode
    /// </summary>
    public static class ModelObserver
    {
        /// <summary>
        /// Lock around the object model copy
        /// </summary>
        private static readonly AsyncLock _lock = new AsyncLock();

        /// <summary>
        /// Lock the object model copy
        /// </summary>
        /// <returns></returns>
        public static AwaitableDisposable<IDisposable> LockAsync() => _lock.LockAsync(Program.CancelSource.Token);

        /// <summary>
        /// Cached object model
        /// </summary>
        public static ObjectModel Model { get; private set; } 

        /// <summary>
        /// Keep the local object model synchronized with the one from DSF
        /// </summary>
        /// <returns></returns>
        public static async Task Run()
        {
            // TODO synchronize object model in patch mode
        }
    }
}
