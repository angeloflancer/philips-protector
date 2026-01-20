using philips_protector.Models;

namespace philips_protector
{
    /// <summary>
    /// Validates license keys against current hardware fingerprint
    /// </summary>
    public class LicenseValidator
    {
        /// <summary>
        /// Validates a license key against the current hardware
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>True if the license key matches the current hardware, false otherwise</returns>
        public static bool ValidateLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;

            // Normalize the input: remove dashes and convert to uppercase
            string normalizedKey = NormalizeLicenseKey(licenseKey);

            // Get current hardware fingerprint
            HardwareInfo currentHardware = HardwareFingerprint.GetHardwareInfo();

            // Generate expected license key from current hardware
            string expectedKey = LicenseGenerator.GenerateLicenseKey(currentHardware);
            string normalizedExpected = NormalizeLicenseKey(expectedKey);

            // Compare normalized keys
            return normalizedKey == normalizedExpected;
        }

        /// <summary>
        /// Normalizes a Base64 license key by removing whitespace and dashes (if any)
        /// Base64 is case-sensitive, so we preserve the original case
        /// </summary>
        private static string NormalizeLicenseKey(string licenseKey)
        {
            // Remove whitespace, dashes, and spaces that might have been added for readability
            return licenseKey.Replace("-", "").Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Trim();
        }

        /// <summary>
        /// Gets the current hardware fingerprint hash (for display purposes)
        /// </summary>
        public static string GetCurrentFingerprint()
        {
            return LicenseGenerator.GenerateLicenseKey();
        }
    }
}
