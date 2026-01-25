using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace MazSvc
{
    /// <summary>
    /// Service installer for Philips License
    /// </summary>
    [RunInstaller(true)]
    public class MazServiceInstaller : Installer
    {
        private ServiceProcessInstaller _serviceProcessInstaller;
        private ServiceInstaller _serviceInstaller;

        public MazServiceInstaller()
        {
            _serviceProcessInstaller = new ServiceProcessInstaller();
            _serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            _serviceInstaller = new ServiceInstaller();
            _serviceInstaller.ServiceName = "PhilipsLicense";
            _serviceInstaller.DisplayName = "Philips License Protection Service";
            _serviceInstaller.Description = "Monitors and protects system resources";
            _serviceInstaller.StartType = ServiceStartMode.Automatic;

            this.Installers.Add(_serviceProcessInstaller);
            this.Installers.Add(_serviceInstaller);
        }
    }
}
