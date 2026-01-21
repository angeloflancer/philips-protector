using System;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace MazManager.Services
{
    /// <summary>
    /// Controls the MazSvc Windows Service (start/stop/status)
    /// </summary>
    public class ServiceController
    {
        private const string SERVICE_NAME = "MazSvc";
        private const string SERVICE_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Services\MazSvc";

        /// <summary>
        /// Checks if the service is installed
        /// </summary>
        public bool IsServiceInstalled()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SERVICE_REGISTRY_KEY))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the service is running
        /// </summary>
        public bool IsServiceRunning()
        {
            try
            {
                using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController(SERVICE_NAME))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current status of the service
        /// </summary>
        public ServiceControllerStatus GetServiceStatus()
        {
            try
            {
                using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController(SERVICE_NAME))
                {
                    return sc.Status;
                }
            }
            catch
            {
                return ServiceControllerStatus.Stopped;
            }
        }

        /// <summary>
        /// Starts the service
        /// </summary>
        public bool StartService()
        {
            try
            {
                // Use UseShellExecute = true to allow elevation
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("start {0}", SERVICE_NAME);
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.Verb = "runas";

                Process process = Process.Start(psi);
                process.WaitForExit(30000);

                // Wait for service to start
                System.Threading.Thread.Sleep(2000);

                return IsServiceRunning();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stops the service
        /// </summary>
        public bool StopService()
        {
            try
            {
                // Use UseShellExecute = true to allow elevation
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("stop {0}", SERVICE_NAME);
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.Verb = "runas";

                Process process = Process.Start(psi);
                process.WaitForExit(30000);

                // Wait for service to stop
                System.Threading.Thread.Sleep(2000);

                return !IsServiceRunning();
            }
            catch
            {
                return false;
            }
        }
    }
}
