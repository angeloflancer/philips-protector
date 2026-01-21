using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace philips_protector
{
    public static class PasswordManager
    {
        private const string REGISTRY_PATH = @"Software\Microsoft\Zregi Terminator";
        private const string PASSWORD = "terminator";
        private const int MAX_ATTEMPTS = 3;

        /// <summary>
        /// Checks if this copy is marked as invalid in the registry.
        /// If invalid, deletes the executable and exits.
        /// </summary>
        public static void CheckInvalidCopy()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object invalidCopyValue = key.GetValue("InvalidCopy");
                        if (invalidCopyValue != null)
                        {
                            int invalidCopy = Convert.ToInt32(invalidCopyValue);
                            if (invalidCopy == 1)
                            {
                                // This copy is marked as invalid, delete and exit
                                DeleteSelf();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If registry access fails, continue (don't block startup)
            }
        }

        /// <summary>
        /// Gets the current password limit (starts at 3, decreases on wrong password).
        /// Initializes to 3 if not set.
        /// </summary>
        public static int GetPasswordLimit()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object limitValue = key.GetValue("Password Limit");
                        if (limitValue != null)
                        {
                            return Convert.ToInt32(limitValue);
                        }
                    }
                }
                
                // If not set, initialize to 3
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue("Password Limit", MAX_ATTEMPTS, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception)
            {
                // If registry access fails, return default value
            }
            // Default value is 3 if not set
            return MAX_ATTEMPTS;
        }

        /// <summary>
        /// Decrements the password limit and returns true if limit reached (0).
        /// </summary>
        public static bool DecrementPasswordLimit()
        {
            try
            {
                int currentLimit = GetPasswordLimit();
                int newLimit = currentLimit - 1;

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue("Password Limit", newLimit, RegistryValueKind.DWord);
                    }
                }

                if (newLimit <= 0)
                {
                    MarkAsInvalid();
                    return true; // Limit reached
                }

                return false; // Limit not reached
            }
            catch (Exception)
            {
                // If registry access fails, assume limit not reached
                return false;
            }
        }

        /// <summary>
        /// Resets the password limit to MAX_ATTEMPTS (3).
        /// </summary>
        public static void ResetPasswordLimit()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue("Password Limit", MAX_ATTEMPTS, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception)
            {
                // If registry access fails, continue
            }
        }

        /// <summary>
        /// Checks if "Remember me" is active in the registry.
        /// </summary>
        public static bool IsRemembered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object rememberValue = key.GetValue("RememberMe");
                        if (rememberValue != null)
                        {
                            string rememberStr = rememberValue.ToString();
                            return rememberStr == "1" || rememberStr.ToLowerInvariant() == "true";
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return false if registry access fails
            }
            return false;
        }

        /// <summary>
        /// Sets the "Remember me" state in the registry.
        /// </summary>
        public static void SetRememberMe(bool remember)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue("RememberMe", remember ? "1" : "0", RegistryValueKind.String);
                    }
                }
            }
            catch (Exception)
            {
                // If registry access fails, continue
            }
        }

        /// <summary>
        /// Validates the provided password against the hardcoded password.
        /// </summary>
        public static bool ValidatePassword(string password)
        {
            return password == PASSWORD;
        }

        /// <summary>
        /// Marks this copy as invalid in the registry.
        /// </summary>
        public static void MarkAsInvalid()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue("InvalidCopy", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception)
            {
                // If registry access fails, continue
            }
        }

        /// <summary>
        /// Deletes the current executable file using a batch script.
        /// </summary>
        public static void DeleteSelf()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    // If we can't get the path, just exit
                    Environment.Exit(0);
                    return;
                }

                // Create a batch script to delete the executable
                string batchPath = Path.Combine(Path.GetTempPath(), "delete_" + Guid.NewGuid().ToString() + ".bat");
                string exeDir = Path.GetDirectoryName(exePath);
                string exeName = Path.GetFileName(exePath);

                StringBuilder batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                batchContent.AppendLine("timeout /t 2 /nobreak >nul");
                batchContent.AppendLine("del \"" + exePath + "\"");
                batchContent.AppendLine("del \"" + batchPath + "\"");

                File.WriteAllText(batchPath, batchContent.ToString(), Encoding.Default);

                // Execute the batch script
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = batchPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);

                // Exit immediately
                Environment.Exit(0);
            }
            catch (Exception)
            {
                // If deletion fails, just exit
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Gets the remaining password attempts (same as password limit).
        /// </summary>
        public static int GetRemainingAttempts()
        {
            return GetPasswordLimit();
        }
    }
}
