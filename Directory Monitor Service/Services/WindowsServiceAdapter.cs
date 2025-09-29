using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace DirectoryMonitorService.Services
{
    /// <summary>
    /// Adapter that bridges the DirectoryMonitorService with the IHostedService interface for .NET Core hosting.
    /// Handles starting and stopping the service and saving/loading file hashes.
    /// </summary>
    public class WindowsServiceAdapter : IHostedService
    {
        private readonly DirectoryMonitorService _service;
        private readonly string _hashFilePath = "fileHashes.json";

        public WindowsServiceAdapter(DirectoryMonitorService service)
        {
            _service = service;
        }

        /// <summary>
        /// Called when the service starts. Loads file hashes and starts the directory watcher.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                _service.LoadFileHashes(_hashFilePath);
                _service.StartService();
            }, cancellationToken);
        }

        /// <summary>
        /// Called when the service stops. Saves file hashes and stops the directory watcher.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                _service.SaveFileHashes(_hashFilePath);
                _service.StopService();
            }, cancellationToken);
        }
    }
}