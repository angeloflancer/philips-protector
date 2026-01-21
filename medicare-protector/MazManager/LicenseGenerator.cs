using System;
using System.Security.Cryptography;
using System.Text;
using MazManager.Models;

namespace MazManager
{
    /// <summary>
    /// Generates license keys from hardware fingerprints using one-way SHA-256 hashing
    /// </summary>
    public static class LicenseGenerator
    {
        // Salt for additional security (must match MazSvc)
        private const string SALT = "MEDICARE_PROTECTOR_2024_SALT_KEY";

        /// <summary>
        /// Generates a license key from hardware information as a Base64 SHA-256 fingerprint
        /// </summary>
        public static string GenerateLicenseKey(HardwareInfo hardwareInfo)
        {
            // Combine hardware info with salt
            string combinedData = hardwareInfo.GetCombinedString() + SALT;

            // Generate SHA-256 hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedData));

                // Return Base64 string
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Generates a license key from the current system's hardware
        /// </summary>
        public static string GenerateLicenseKey()
        {
            HardwareInfo hardwareInfo = HardwareFingerprint.GetHardwareInfo();
            return GenerateLicenseKey(hardwareInfo);
        }
    }
}
