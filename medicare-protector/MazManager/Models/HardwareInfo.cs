using System;

namespace MazManager.Models
{
    /// <summary>
    /// Contains hardware identification information
    /// </summary>
    public class HardwareInfo
    {
        public string CpuId { get; set; }
        public string MotherboardSerial { get; set; }
        public string HardDriveSerial { get; set; }
        public string MacAddress { get; set; }
        public string BiosSerial { get; set; }
        public string MachineGuid { get; set; }

        /// <summary>
        /// Gets all hardware identifiers as a combined string for hashing
        /// </summary>
        public string GetCombinedString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                CpuId ?? string.Empty,
                MotherboardSerial ?? string.Empty,
                HardDriveSerial ?? string.Empty,
                MacAddress ?? string.Empty,
                BiosSerial ?? string.Empty,
                MachineGuid ?? string.Empty);
        }
    }
}
