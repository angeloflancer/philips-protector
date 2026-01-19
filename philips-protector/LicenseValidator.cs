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

            // Get current hardware fingerprint
            HardwareInfo currentHardware = HardwareFingerprint.GetHardwareInfo();

            // Generate expected license key from current hardware
            string expectedKey = LicenseGenerator.GenerateLicenseKey(currentHardware);

            // Compare (case-sensitive)
            return licenseKey.Trim() == expectedKey.Trim();
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
