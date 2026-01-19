namespace philips_protector.Models
{
    /// <summary>
    /// Represents hardware identifiers collected from the system
    /// </summary>
    public class HardwareInfo
    {
        public string CpuId { get; set; } = string.Empty;
        public string MotherboardSerial { get; set; } = string.Empty;
        public string HardDriveSerial { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string BiosSerial { get; set; } = string.Empty;
        public string MachineGuid { get; set; } = string.Empty;

        /// <summary>
        /// Combines all hardware identifiers into a single string for hashing
        /// </summary>
        public string GetCombinedString()
        {
            return $"{CpuId}|{MotherboardSerial}|{HardDriveSerial}|{MacAddress}|{BiosSerial}|{MachineGuid}";
        }
    }
}
