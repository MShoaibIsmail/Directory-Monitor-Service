using DirectoryMonitorService.Logging;
using DirectoryMonitorService.Services;
using System.IO;
using System.ServiceProcess;

namespace DirectoryMonitorService
{
    /// <summary>
    /// Main Windows Service class responsible for initializing and controlling the lifecycle of directory monitoring.
    /// </summary>
    public partial class DirectoryMonitorService : ServiceBase
    {
        private DirectoryWatcher _directoryWatcher;
        private FileLogger _logger;

        public DirectoryMonitorService(DirectoryWatcher directoryWatcher, FileLogger logger)
        {
            _directoryWatcher = directoryWatcher;
            _logger = logger;
        }


        /// <summary>
        /// Starts the directory monitoring service and logs the start.
        /// </summary>
        public void StartService()
        {
            _directoryWatcher?.Start();
            _logger.Log("Service started.");
        }

        /// <summary>
        /// Stops the directory monitoring service and logs the stop.
        /// </summary>
        public void StopService()
        {
            _directoryWatcher?.Stop();
            _logger?.Log("Service stopped.");
        }

        /// <summary>
        /// Loads file hashes from disk into the directory watcher.
        /// </summary>
        public void LoadFileHashes(string path)
        {
            _directoryWatcher?.LoadFileHashes(path);
            _logger?.Log("Load File Hashes.");
        }

        /// <summary>
        /// Saves file hashes from the directory watcher to disk.
        /// </summary>
        public void SaveFileHashes(string path)
        {
            _directoryWatcher?.SaveFileHashes(path);
            _logger?.Log("Save File Hashes.");
        }
    }
}