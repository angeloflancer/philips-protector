using System;
using System.Security.Cryptography;
using System.Text;
using philips_protector.Models;

namespace philips_protector
{
    /// <summary>
    /// Generates license keys from hardware fingerprints using one-way SHA-256 hashing
    /// </summary>
    public static class LicenseGenerator
    {
        // Salt for additional security (prevents rainbow table attacks)
        private const string SALT = "PHILIPS_PROTECTOR_2024_SALT_KEY";

        /// <summary>
        /// Generates a license key from hardware information as a Base64 SHA-256 fingerprint
        /// The key is one-way encrypted and cannot be decrypted
        /// </summary>
        public static string GenerateLicenseKey(HardwareInfo hardwareInfo)
        {
            // Combine hardware info with salt
            string combinedData = hardwareInfo.GetCombinedString() + SALT;

            // Generate SHA-256 hash (one-way, cannot be decrypted)
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedData));

                // Return Base64 string of the raw fingerprint
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
