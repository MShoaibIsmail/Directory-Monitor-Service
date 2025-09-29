using DirectoryMonitorService.Logging;
using DirectoryMonitorService.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DirectoryMonitorService.Services
{
    /// <summary>
    /// Monitors a specified directory and triggers duplicate file checks on file system changes.
    /// </summary>
    public class DirectoryWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly FileLogger _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastProcessed = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _eventThreshold = TimeSpan.FromSeconds(1);
        private readonly ConcurrentDictionary<string, string> _fileHashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<string>> _hashToPaths = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly FileChangeProcessor _fileChangeProcessor;

        /// <summary>
        /// Initializes a new instance of the DirectoryWatcher class.
        /// </summary>
        /// <param name="directoryPath">The path of the directory to monitor.</param>
        /// <param name="logger">The logger instance for logging events.</param>
        /// <param name="duplicateChecker">The duplicate file checker instance.</param>
        public DirectoryWatcher(string directoryPath, FileLogger logger)
        {
            _logger = logger;

            // Ensure the directory exists
            Directory.CreateDirectory(directoryPath);

            // Initialize the FileSystemWatcher
            _watcher = new FileSystemWatcher(directoryPath)
            {
                // Monitor all files
                Filter = "*.*",

                // Monitor all relevant changes
                NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName |
                               NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size,

                // Include subdirectories
                IncludeSubdirectories = true,

                // Start monitoring
                EnableRaisingEvents = true,

               InternalBufferSize = 64 * 1024, // 64 KB
            };

            // Instantiate FileChangeProcessor
            _fileChangeProcessor = new FileChangeProcessor(_logger, _fileHashes, _hashToPaths);

            // Subscribe to events
            _watcher.Created += OnCreated;
            _watcher.Deleted += OnDeleted;
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
        }

        /// <summary>
        /// Handles the Created event of the FileSystemWatcher.
        /// </summary>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsDuplicateEvent(e.FullPath))
                return;

            // Delay to ensure file is fully written
            Task.Delay(2000).ContinueWith(_ =>
            {
                if (FileHelper.IsFileReady(e.FullPath, _logger))
                    _fileChangeProcessor.Process(e.FullPath);
            });
        }

        /// <summary>
        /// Handles the Changed event of the FileSystemWatcher.
        /// </summary>
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsDuplicateEvent(e.FullPath))
                return;

            if (!FileHelper.IsFileReady(e.FullPath, _logger))
                return;

            _logger.Log($"File Changed: {e.FullPath}");
            Task.Run(() => _fileChangeProcessor.Process(e.FullPath));
        }

        /// <summary>
        /// Handles the Deleted event of the FileSystemWatcher.
        /// </summary>
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (IsDuplicateEvent(e.FullPath))
                return;

            _logger.Log($"File Deleted: {e.FullPath}");
            _fileHashes.TryRemove(e.FullPath, out _);
        }

        /// <summary>
        /// Handles the Renamed event of the FileSystemWatcher.
        /// </summary>
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsDuplicateEvent(e.FullPath))
                return;

            Task.Delay(2000).ContinueWith(_ =>
            {
                if (FileHelper.IsFileReady(e.FullPath,_logger))
                {
                    _logger.Log($"File Renamed: {e.OldFullPath} -> {e.FullPath}");
                    string removedValue;
                    _fileHashes.TryRemove(e.OldFullPath, out removedValue);
                    _fileChangeProcessor.Process(e.FullPath);
                }
            });
        }

        /// <summary>
        /// Handles errors raised by the FileSystemWatcher.
        /// </summary>
        private void OnError(object sender, ErrorEventArgs e)
        {
            var ex = e.GetException();
            if (ex is InternalBufferOverflowException)
            {
                _logger.Log("Warning: FileSystemWatcher buffer overflow occurred. Some events may have been lost.");
            }
            _logger.Log($"FileSystemWatcher error: {ex.Message}");
        }

        /// <summary>
        /// Determines whether the specified file event is a duplicate within a defined threshold.
        /// </summary>
        private bool IsDuplicateEvent(string filePath)
        {
            var now = DateTime.Now;
            if (_lastProcessed.TryGetValue(filePath, out DateTime lastTime))
            {
                if ((now - lastTime) < _eventThreshold)
                    return true;
            }
            _lastProcessed[filePath] = now;
            return false;
        }

        /// <summary>
        /// Starts monitoring by enabling events on the FileSystemWatcher.
        /// </summary>
        public void Start()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Stops monitoring the directory and disposes the FileSystemWatcher instance.
        /// </summary>
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _logger.Log("Directory watcher stopped.");
        }

        /// <summary>
        /// Loads file hashes from a JSON file into memory.
        /// </summary>
        public void LoadFileHashes(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _fileHashes[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to load file hashes from '{path}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves the current file hashes to a JSON file on disk.
        /// </summary>
        public void SaveFileHashes(string path)
        {
            try
            {
                var dict = new Dictionary<string, string>(_fileHashes);
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to save file hashes to '{path}': {ex.Message}");
            }
        }
    }
}