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
        /// Generates a license key from hardware information
        /// The key is one-way encrypted and cannot be decrypted
        /// Format: XXXXX-XXXXX-XXXXX-... (13 groups of 5 uppercase alphanumeric characters)
        /// </summary>
        public static string GenerateLicenseKey(HardwareInfo hardwareInfo)
        {
            // Combine hardware info with salt
            string combinedData = hardwareInfo.GetCombinedString() + SALT;

            // Generate SHA-256 hash (one-way, cannot be decrypted)
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
                
                // Convert hash bytes to uppercase alphanumeric string
                string alphanumericHash = ConvertHashToAlphanumeric(hashBytes);
                
                // Format as 13 groups of 5 characters: XXXXX-XXXXX-XXXXX-...
                return FormatLicenseKey(alphanumericHash);
            }
        }

        /// <summary>
        /// Converts hash bytes to uppercase alphanumeric string
        /// </summary>
        private static string ConvertHashToAlphanumeric(byte[] hashBytes)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var result = new StringBuilder();
            
            // Use hash bytes to generate alphanumeric characters
            // We need at least 65 characters (13 groups * 5 chars)
            int neededChars = 65;
            int byteIndex = 0;
            
            while (result.Length < neededChars)
            {
                // Use multiple bytes to ensure good distribution
                int value = 0;
                for (int i = 0; i < 4 && byteIndex < hashBytes.Length; i++)
                {
                    value = (value << 8) | hashBytes[byteIndex];
                    byteIndex++;
                }
                
                // If we've used all bytes, re-hash with a counter
                if (byteIndex >= hashBytes.Length)
                {
                    byteIndex = 0;
                    // Add a counter to vary the result
                    value = (value << 8) | (result.Length % 256);
                }
                
                // Convert to alphanumeric (36 possible characters: 0-9, A-Z)
                result.Append(chars[Math.Abs(value) % chars.Length]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Formats alphanumeric string into license key format: XXXXX-XXXXX-XXXXX-...
        /// 13 groups of 5 characters each
        /// </summary>
        private static string FormatLicenseKey(string alphanumericString)
        {
            const int groups = 13;
            const int charsPerGroup = 5;
            
            var result = new StringBuilder();
            
            for (int i = 0; i < groups; i++)
            {
                if (i > 0)
                    result.Append('-');
                
                int startIndex = i * charsPerGroup;
                int length = Math.Min(charsPerGroup, alphanumericString.Length - startIndex);
                
                if (length > 0)
                {
                    result.Append(alphanumericString.Substring(startIndex, length));
                }
                else
                {
                    // If we run out of characters, pad with zeros
                    result.Append(new string('0', charsPerGroup - length));
                }
            }
            
            return result.ToString();
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
