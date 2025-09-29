using DirectoryMonitorService.Logging;
using DirectoryMonitorService.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DirectoryMonitorService.Services
{
    /// <summary>
    /// Handles processing of file changes, including computing hashes and checking for duplicates.
    /// </summary>
    public class FileChangeProcessor
    {
        private readonly FileLogger _logger;
        private readonly ConcurrentDictionary<string, string> _fileHashes;
        private readonly ConcurrentDictionary<string, HashSet<string>> _hashToPaths = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Initializes a new instance of the FileChangeProcessor class.
        /// </summary>
        /// <param name="logger">The logger instance for logging events.</param>
        /// <param name="duplicateChecker">The duplicate file checker instance.</param>
        /// <param name="fileHashes">A thread-safe dictionary to store file hashes.</param>
        public FileChangeProcessor(FileLogger logger, ConcurrentDictionary<string, string> fileHashes, ConcurrentDictionary<string, HashSet<string>> hashToPaths)
        {
            _logger = logger;
            _fileHashes = fileHashes;
            _hashToPaths= hashToPaths;
        }

        /// <summary>
        /// Processes the file change by computing its hash and checking for duplicates.
        /// Implements retry logic to handle files locked by other processes.
        /// </summary>
        /// <param name="filePath">The full path of the file to process.</param>
        public void Process(string filePath)
        {
            const int maxRetries = 10;
            const int delayMilliseconds = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (!File.Exists(filePath))
                        return;

                    if (!FileHelper.IsFileReady(filePath, _logger))
                        throw new IOException("File is not ready for processing.");

                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        string newHash = ComputeFileHash(stream);

                        if (_hashToPaths.TryGetValue(newHash, out var existingPaths) &&
                            existingPaths.Contains(filePath) == false)
                        {
                            foreach (var duplicatePath in existingPaths)
                            {
                                _logger.Log($"Duplicate file detected: {filePath} and {duplicatePath}");
                            }
                        }

                        _fileHashes[filePath] = newHash;

                        if (!_hashToPaths.ContainsKey(newHash))
                            _hashToPaths[newHash] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        _hashToPaths[newHash].Add(filePath);
                        _logger.Log($"File Created : {filePath}");
                    }

                    break; // success
                }
                catch (IOException ioEx)
                {
                    _logger.Log($"IO error processing file '{filePath}' (Attempt {attempt} of {maxRetries}): {ioEx.Message}");
                    if (attempt == maxRetries)
                        return;

                    Thread.Sleep(delayMilliseconds);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    _logger.Log($"Access denied processing file '{filePath}': {uaEx.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Log($"Unexpected error processing file '{filePath}': {ex.Message}");
                    return;
                }
            }
        }

        /// <summary>
        /// Computes the MD5 hash of the specified file stream.
        /// </summary>
        /// <param name="stream">The file stream to compute the hash for.</param>
        /// <returns>The computed hash as a hexadecimal string.</returns>
        private string ComputeFileHash(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}