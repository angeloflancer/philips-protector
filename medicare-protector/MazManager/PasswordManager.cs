using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MazManager
{
    /// <summary>
    /// Manages password validation and copy protection
    /// </summary>
    public static class PasswordManager
    {
        // Password for access (should be changed for production)
        private const string PASSWORD = "terminator";

        // Registry path for storing protection data
        private const string REGISTRY_PATH = @"Software\Microsoft\Zregi MazManager";

        // Registry value names
        private const string INVALID_COPY_VALUE = "InvalidCopy";
        private const string PASSWORD_LIMIT_VALUE = "PasswordLimit";
        private const string REMEMBER_ME_VALUE = "RememberMe";

        // Default password attempt limit
        private const int DEFAULT_PASSWORD_LIMIT = 3;

        /// <summary>
        /// Checks if this copy has been marked as invalid and deletes if so
        /// </summary>
        /// <returns>True if copy is invalid and should exit</returns>
        public static bool CheckInvalidCopy()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(INVALID_COPY_VALUE);
                        if (value != null && value.ToString() == "1")
                        {
                            // Delete self
                            DeleteSelf();
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Gets the remaining password attempts
        /// </summary>
        public static int GetRemainingAttempts()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(PASSWORD_LIMIT_VALUE);
                        if (value != null)
                        {
                            int limit;
                            if (int.TryParse(value.ToString(), out limit))
                            {
                                return limit;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return DEFAULT_PASSWORD_LIMIT;
        }

        /// <summary>
        /// Decrements the password attempt limit
        /// </summary>
        public static void DecrementPasswordLimit()
        {
            try
            {
                int remaining = GetRemainingAttempts();
                remaining--;

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue(PASSWORD_LIMIT_VALUE, remaining.ToString());
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Resets the password attempt limit to default
        /// </summary>
        public static void ResetPasswordLimit()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue(PASSWORD_LIMIT_VALUE, DEFAULT_PASSWORD_LIMIT.ToString());
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Checks if "Remember Me" is enabled
        /// </summary>
        public static bool IsRemembered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(REMEMBER_ME_VALUE);
                        if (value != null)
                        {
                            return value.ToString() == "1";
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Sets the "Remember Me" status
        /// </summary>
        public static void SetRememberMe(bool remember)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue(REMEMBER_ME_VALUE, remember ? "1" : "0");
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Validates the provided password
        /// </summary>
        public static bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            return string.Equals(password, PASSWORD, StringComparison.Ordinal);
        }

        /// <summary>
        /// Marks this copy as invalid
        /// </summary>
        public static void MarkAsInvalid()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue(INVALID_COPY_VALUE, "1");
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Deletes the executable using a batch script
        /// </summary>
        public static void DeleteSelf()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string tempPath = Path.GetTempPath();
                string batchPath = Path.Combine(tempPath, "cleanup_" + Guid.NewGuid().ToString("N") + ".bat");

                // Create batch script that waits and then deletes
                string batchContent = string.Format(
                    "@echo off\r\n" +
                    "ping 127.0.0.1 -n 3 > nul\r\n" +
                    "del /f /q \"{0}\"\r\n" +
                    "del /f /q \"%~f0\"\r\n",
                    exePath);

                File.WriteAllText(batchPath, batchContent);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = batchPath;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = true;

                Process.Start(psi);
            }
            catch
            {
            }
        }
    }
}
