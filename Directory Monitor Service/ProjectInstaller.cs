using DirectoryMonitorService.Logging;
using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.ServiceProcess;

namespace DirectoryMonitorService
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        private ServiceInstaller serviceInstaller;
        private ServiceProcessInstaller processInstaller;
        private readonly FileLogger _logger;
        public ProjectInstaller(FileLogger logger)
        {
            _logger = logger;
            // Configure the service to run under the LocalSystem account
            processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            serviceInstaller = new ServiceInstaller
            {
                ServiceName = "DirectoryMonitorService",
                DisplayName = "Directory Monitor Service",
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);

            // Add event handlers
            this.AfterInstall += OnAfterInstall;
            this.BeforeUninstall += OnBeforeUninstall;
        }
        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            // Get the parameter from the install context
            string watchDir = Context.Parameters["watchdir"];

            if (string.IsNullOrEmpty(watchDir))
            {
                throw new InstallException("You must provide a watch directory using the /watchdir parameter.");
            }

            // Save the watch directory to a config file next to the executable
            string exePath = Context.Parameters["assemblypath"];
            string folder = Path.GetDirectoryName(exePath);
            string configPath = Path.Combine(folder, "watchdir.config");

            File.WriteAllText(configPath, watchDir);
        }

        private void OnAfterInstall(object sender, InstallEventArgs e)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceInstaller.ServiceName))
                {
                    sc.Start();
                    _logger.Log("Service started after installation.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to start service after install: {ex.Message}");
            }
        }

        private void OnBeforeUninstall(object sender, InstallEventArgs e)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceInstaller.ServiceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        _logger.Log("Service stopped before uninstallation.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to stop service before uninstall: {ex.Message}");
            }
        }
    }
}
