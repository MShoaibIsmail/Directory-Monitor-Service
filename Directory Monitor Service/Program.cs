using DirectoryMonitorService.Configuration;
using Microsoft.Extensions.Hosting;

namespace DirectoryMonitorService
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main()
        {
            Host.CreateDefaultBuilder()
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    ServiceRegistrar.Register(services);
                })
                .Build()
                .Run();
        }
    }
}