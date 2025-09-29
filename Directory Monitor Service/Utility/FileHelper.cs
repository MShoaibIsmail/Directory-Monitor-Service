using DirectoryMonitorService.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DirectoryMonitorService.Utility
{
    public static class FileHelper
    {
        /// <summary>
        /// Checks if the file is ready for processing by attempting to open it exclusively.
        /// </summary>
        /// <param name="filePath">The full path of the file to check.</param>
        /// <returns>True if the file is ready; otherwise, false.</returns>
        public static bool IsFileReady(string filePath, FileLogger logger = null)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                long size1 = fileInfo.Length;
                Thread.Sleep(500); // wait for writing to possibly finish
                fileInfo.Refresh();
                long size2 = fileInfo.Length;

                return size1 > 0 && size1 == size2;
            }
            catch (IOException ioEx)
            {
                logger?.Log($"IO error while checking readiness of '{filePath}': {ioEx.Message}");
                return false;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                logger?.Log($"Access denied while checking readiness of '{filePath}': {uaEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logger?.Log($"Unexpected error while checking readiness of '{filePath}': {ex.Message}");
                return false;
            }
        }
    }
}