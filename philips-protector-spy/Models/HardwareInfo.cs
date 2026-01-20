namespace philips_protector_spy.Models
{
    /// <summary>
    /// Represents hardware identifiers collected from the system
    /// </summary>
    public class HardwareInfo
    {
        public string CpuId { get; set; }
        public string MotherboardSerial { get; set; }
        public string HardDriveSerial { get; set; }
        public string MacAddress { get; set; }
        public string BiosSerial { get; set; }
        public string MachineGuid { get; set; }

        public HardwareInfo()
        {
            CpuId = string.Empty;
            MotherboardSerial = string.Empty;
            HardDriveSerial = string.Empty;
            MacAddress = string.Empty;
            BiosSerial = string.Empty;
            MachineGuid = string.Empty;
        }

        /// <summary>
        /// Combines all hardware identifiers into a single string for hashing
        /// </summary>
        public string GetCombinedString()
        {
            return CpuId + "|" + MotherboardSerial + "|" + HardDriveSerial + "|" + MacAddress + "|" + BiosSerial + "|" + MachineGuid;
        }
    }
}
