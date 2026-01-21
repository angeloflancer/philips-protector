using System;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Validates license keys against current hardware fingerprint
    /// </summary>
    public static class LicenseValidator
    {
        /// <summary>
        /// Validates a license key against the current system's hardware fingerprint
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>True if the license key matches the current hardware</returns>
        public static bool ValidateLicenseKey(string licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
            {
                return false;
            }

            try
            {
                // Normalize the license key (remove any whitespace or dashes)
                string normalizedInput = NormalizeLicenseKey(licenseKey);

                // Generate expected license key from current hardware
                string expectedKey = GetCurrentFingerprint();
                string normalizedExpected = NormalizeLicenseKey(expectedKey);

                // Compare the keys
                return string.Equals(normalizedInput, normalizedExpected, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the license fingerprint for the current system
        /// </summary>
        public static string GetCurrentFingerprint()
        {
            return LicenseGenerator.GenerateLicenseKey();
        }

        /// <summary>
        /// Normalizes a license key by removing whitespace and dashes
        /// </summary>
        private static string NormalizeLicenseKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return key.Replace("-", "").Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim();
        }
    }
}
