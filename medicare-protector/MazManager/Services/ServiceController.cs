using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace MazManager.Services
{
    /// <summary>
    /// Controls the Philips License Windows Service (start/stop/status)
    /// </summary>
    public class ServiceController
    {
        private const string SERVICE_NAME = "PhilipsLicense";
        private const string SERVICE_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Services\PhilipsLicense";

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
        /// Gets detailed diagnostics about why the service might fail to start
        /// </summary>
        private string GetServiceDiagnostics()
        {
            StringBuilder issues = new StringBuilder();
            
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SERVICE_REGISTRY_KEY))
                {
                    if (key == null)
                    {
                        issues.AppendLine("- Service registry key not found");
                        return issues.ToString();
                    }

                    // Check ImagePath
                    object imagePathObj = key.GetValue("ImagePath");
                    if (imagePathObj == null)
                    {
                        issues.AppendLine("- Service ImagePath not configured");
                    }
                    else
                    {
                        string exePath = imagePathObj.ToString().Trim('"');
                        
                        if (!File.Exists(exePath))
                        {
                            issues.AppendLine(string.Format("- Service executable not found: {0}", exePath));
                        }
                        else
                        {
                            // Check if file is blocked
                            try
                            {
                                using (FileStream fs = File.Open(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    // File can be opened
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                issues.AppendLine(string.Format("- Access denied to service executable: {0}", exePath));
                            }
                            catch (Exception ex)
                            {
                                issues.AppendLine(string.Format("- Cannot access service executable: {0} ({1})", exePath, ex.Message));
                            }
                        }
                    }

                    // Check Start type
                    object startObj = key.GetValue("Start");
                    if (startObj != null)
                    {
                        int startType = Convert.ToInt32(startObj);
                        if (startType == 4) // Disabled
                        {
                            issues.AppendLine("- Service is DISABLED. Change startup type to Manual or Automatic.");
                        }
                    }

                    // Check ObjectName (service account)
                    object objectNameObj = key.GetValue("ObjectName");
                    if (objectNameObj != null)
                    {
                        string account = objectNameObj.ToString();
                        if (account != "LocalSystem")
                        {
                            issues.AppendLine(string.Format("- Service runs as '{0}' (expected LocalSystem)", account));
                        }
                    }
                }

                // Check Event Log for recent errors
                try
                {
                    EventLog appLog = new EventLog("Application");
                    DateTime cutoff = DateTime.Now.AddHours(-1);
                    
                    int errorCount = 0;
                    string lastError = "";
                    
                    for (int i = appLog.Entries.Count - 1; i >= 0 && i >= appLog.Entries.Count - 50; i--)
                    {
                        EventLogEntry entry = appLog.Entries[i];
                        if (entry.TimeGenerated > cutoff &&
                            entry.EntryType == EventLogEntryType.Error &&
                            (entry.Source.Contains("PhilipsLicense") || entry.Source.Contains("Service Control Manager")))
                        {
                            if (entry.Message.Contains("PhilipsLicense") || entry.Message.Contains(SERVICE_NAME))
                            {
                                errorCount++;
                                if (string.IsNullOrEmpty(lastError))
                                {
                                    lastError = entry.Message;
                                }
                            }
                        }
                    }
                    
                    if (errorCount > 0)
                    {
                        issues.AppendLine(string.Format("- {0} recent error(s) in Event Log", errorCount));
                        if (!string.IsNullOrEmpty(lastError))
                        {
                            // Truncate to first 200 chars
                            if (lastError.Length > 200)
                            {
                                lastError = lastError.Substring(0, 200) + "...";
                            }
                            issues.AppendLine(string.Format("  Last error: {0}", lastError.Replace("\r\n", " ").Replace("\n", " ")));
                        }
                    }
                }
                catch
                {
                    // Event log check failed
                }
            }
            catch
            {
                // Diagnostics failed
            }

            return issues.ToString();
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

                // Run diagnostics to identify potential issues
                string diagnostics = GetServiceDiagnostics();
                if (!string.IsNullOrEmpty(diagnostics))
                {
                    throw new Exception("Service startup diagnostics found issues:\n\n" + diagnostics);
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
                        // Exit code 2 = file not found - same as installation error
                        string errorMsg = string.Format("sc.exe start failed with exit code {0}", process.ExitCode);
                        
                        if (process.ExitCode == 2)
                        {
                            // Check the service executable path from registry
                            string exePath = null;
                            try
                            {
                                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SERVICE_REGISTRY_KEY))
                                {
                                    if (key != null)
                                    {
                                        object imagePath = key.GetValue("ImagePath");
                                        if (imagePath != null)
                                        {
                                            exePath = imagePath.ToString().Trim('"');
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                            
                            errorMsg += ". The service executable file cannot be found or accessed.";
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                errorMsg += string.Format("\n\nService executable path: {0}", exePath);
                                
                                if (!File.Exists(exePath))
                                {
                                    errorMsg += "\n\nThe file does not exist at this location. Please reinstall the service.";
                                }
                                else
                                {
                                    // Check if file is accessible
                                    try
                                    {
                                        using (FileStream fs = File.Open(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                        {
                                            // File can be opened
                                        }
                                        
                                        // Check if file is read-only (might prevent service from starting)
                                        FileInfo fileInfo = new FileInfo(exePath);
                                        if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                        {
                                            errorMsg += "\n\nThe file is marked as ReadOnly. This may prevent the service from starting.";
                                        }
                                        else
                                        {
                                            errorMsg += "\n\nThe file exists but the service cannot start it. Check Event Log for details.";
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        errorMsg += "\n\nAccess denied to the service executable. Check file permissions.";
                                    }
                                    catch (Exception ex)
                                    {
                                        errorMsg += string.Format("\n\nCannot access the service executable: {0}", ex.Message);
                                    }
                                }
                            }
                            else
                            {
                                errorMsg += "\n\nService ImagePath is not configured in registry. Please reinstall the service.";
                            }
                        }
                        else if (process.ExitCode == 5)
                        {
                            errorMsg += ". Access denied. Make sure you are running as Administrator.";
                        }
                        else
                        {
                            errorMsg += ". Service may not be installed correctly.";
                        }
                        
                        throw new Exception(errorMsg);
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
