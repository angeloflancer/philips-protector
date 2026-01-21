using System;
using System.Diagnostics;
using System.IO;
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
                    if (key == null)
                    {
                        return false;
                    }
                    
                    // Also verify the executable path exists
                    object imagePath = key.GetValue("ImagePath");
                    if (imagePath != null)
                    {
                        string exePath = imagePath.ToString().Trim('"');
                        if (!File.Exists(exePath))
                        {
                            return false; // Service registered but executable missing
                        }
                    }
                    
                    return true;
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
            string lastError = "";
            
            try
            {
                // First check if the service is installed
                if (!IsServiceInstalled())
                {
                    // Check if service is registered but executable is missing
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SERVICE_REGISTRY_KEY))
                        {
                            if (key != null)
                            {
                                object imagePath = key.GetValue("ImagePath");
                                if (imagePath != null)
                                {
                                    string exePath = imagePath.ToString().Trim('"');
                                    if (!File.Exists(exePath))
                                    {
                                        throw new Exception(string.Format("Service is registered but executable is missing: {0}\n\nPlease reinstall the service.", exePath));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("executable is missing"))
                        {
                            throw; // Re-throw the missing executable error
                        }
                    }
                    
                    throw new Exception("Service is not installed. Please install the service first.");
                }

                // Try using ServiceController first (doesn't require elevation prompt)
                try
                {
                    using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController(SERVICE_NAME))
                    {
                        ServiceControllerStatus currentStatus = sc.Status;
                        
                        if (currentStatus == ServiceControllerStatus.Running)
                        {
                            return true; // Already running
                        }
                        
                        // Check if service is in a transitional state
                        if (currentStatus == ServiceControllerStatus.StartPending)
                        {
                            // Wait for it to finish starting
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                            return sc.Status == ServiceControllerStatus.Running;
                        }
                        
                        if (currentStatus == ServiceControllerStatus.StopPending || 
                            currentStatus == ServiceControllerStatus.PausePending ||
                            currentStatus == ServiceControllerStatus.ContinuePending)
                        {
                            throw new Exception(string.Format("Service is in transitional state: {0}. Please wait and try again.", currentStatus));
                        }
                        
                        if (currentStatus == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                            
                            if (sc.Status == ServiceControllerStatus.Running)
                            {
                                return true;
                            }
                            else
                            {
                                throw new Exception(string.Format("Service failed to start. Current status: {0}", sc.Status));
                            }
                        }
                        
                        throw new Exception(string.Format("Service cannot be started from current status: {0}", currentStatus));
                    }
                }
                catch (InvalidOperationException ioEx)
                {
                    // ServiceController failed - likely permission issue or service doesn't exist
                    lastError = ioEx.Message;
                    // Fall through to try sc.exe
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // Service didn't start in time
                    throw new Exception("Service start timed out. The service may be taking too long to initialize. Check Event Log for details.");
                }
                catch (Exception ex)
                {
                    // Other ServiceController errors
                    lastError = ex.Message;
                    // Fall through to try sc.exe
                }

                // Fallback: Use sc.exe with elevation
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "sc.exe";
                    psi.Arguments = string.Format("start {0}", SERVICE_NAME);
                    psi.UseShellExecute = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.CreateNoWindow = true;
                    
                    // Only use runas on Vista and later (XP doesn't have UAC)
                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        psi.Verb = "runas";
                    }

                    Process process = Process.Start(psi);
                    if (process == null)
                    {
                        throw new Exception("Failed to start sc.exe process. Insufficient permissions or system error.");
                    }
                    
                    process.WaitForExit(30000);
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception(string.Format("sc.exe start failed with exit code {0}. Service may not be installed correctly.", process.ExitCode));
                    }

                    // Wait for service to start and verify
                    for (int i = 0; i < 30; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        if (IsServiceRunning())
                        {
                            return true;
                        }
                    }
                    
                    throw new Exception("Service did not start after sc.exe command. Check Event Log for service errors.");
                }
                catch (Exception scEx)
                {
                    string errorMsg = "Failed to start service using sc.exe.";
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        errorMsg += " ServiceController error: " + lastError + ". ";
                    }
                    errorMsg += " sc.exe error: " + scEx.Message;
                    throw new Exception(errorMsg);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to start service: " + ex.Message);
            }
        }

        /// <summary>
        /// Stops the service
        /// </summary>
        public bool StopService()
        {
            try
            {
                // Try using ServiceController first (doesn't require elevation prompt)
                try
                {
                    using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController(SERVICE_NAME))
                    {
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                            return sc.Status == ServiceControllerStatus.Stopped;
                        }
                        else if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            return true; // Already stopped
                        }
                    }
                }
                catch
                {
                    // ServiceController failed, try sc.exe
                }

                // Fallback: Use sc.exe with elevation
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("stop {0}", SERVICE_NAME);
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                
                // Only use runas on Vista and later (XP doesn't have UAC)
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    psi.Verb = "runas";
                }

                Process process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(30000);
                }

                // Wait for service to stop
                System.Threading.Thread.Sleep(2000);

                return !IsServiceRunning();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to stop service: " + ex.Message);
            }
        }
    }
}
