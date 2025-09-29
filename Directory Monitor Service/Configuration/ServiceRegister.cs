using Microsoft.Extensions.DependencyInjection;
using DirectoryMonitorService.Logging;
using DirectoryMonitorService.Services;
using System.IO;

namespace DirectoryMonitorService.Configuration
{
    public static class ServiceRegistrar
    {
        public static void Register(IServiceCollection services)
        {
            string logPath = @"C:\\ServiceLogs\\log.txt";
            string watchDir = ReadConfigDirectory() ?? @"C:\\WatchedDirectory";

            services.AddSingleton(new FileLogger(logPath));
            services.AddSingleton(provider =>
                new DirectoryWatcher(watchDir,
                                     provider.GetRequiredService<FileLogger>()));
            services.AddSingleton<DirectoryMonitorService>();
            services.AddHostedService<WindowsServiceAdapter>();
        }

        private static string ReadConfigDirectory()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string configPath = Path.Combine(Path.GetDirectoryName(exePath), "watchdir.config");
                return File.Exists(configPath) ? File.ReadAllText(configPath).Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}