using System.Management;
using philips_protector_spy.Models;

namespace philips_protector_spy
{
    /// <summary>
    /// Collects unique hardware identifiers from the system
    /// </summary>
    public static class HardwareFingerprint
    {
        /// <summary>
        /// Retrieves all hardware identifiers from the system
        /// </summary>
        public static HardwareInfo GetHardwareInfo()
        {
            return new HardwareInfo
            {
                CpuId = GetCpuId(),
                MotherboardSerial = GetMotherboardSerial(),
                HardDriveSerial = GetHardDriveSerial(),
                MacAddress = GetMacAddress(),
                BiosSerial = GetBiosSerial(),
                MachineGuid = GetMachineGuid()
            };
        }

        /// <summary>
        /// Gets the CPU Processor ID
        /// </summary>
        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var cpuId = obj["ProcessorId"] != null ? obj["ProcessorId"].ToString() : null;
                        if (!string.IsNullOrEmpty(cpuId))
                            return cpuId;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the Motherboard Serial Number
        /// </summary>
        private static string GetMotherboardSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var serial = obj["SerialNumber"] != null ? obj["SerialNumber"].ToString() : null;
                        if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                            return serial;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the Hard Drive Serial Number (primary disk)
        /// </summary>
        private static string GetHardDriveSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var serial = obj["SerialNumber"] != null ? obj["SerialNumber"].ToString() : null;
                        if (!string.IsNullOrEmpty(serial))
                            return serial.Trim();
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the MAC Address of the first active network adapter
        /// </summary>
        private static string GetMacAddress()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL AND PhysicalAdapter=TRUE"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var mac = obj["MACAddress"] != null ? obj["MACAddress"].ToString() : null;
                        if (!string.IsNullOrEmpty(mac))
                            return mac;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the BIOS Serial Number
        /// </summary>
        private static string GetBiosSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var serial = obj["SerialNumber"] != null ? obj["SerialNumber"].ToString() : null;
                        if (!string.IsNullOrEmpty(serial))
                            return serial;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the Windows Machine GUID
        /// </summary>
        private static string GetMachineGuid()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var guid = obj["UUID"] != null ? obj["UUID"].ToString() : null;
                        if (!string.IsNullOrEmpty(guid))
                            return guid;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }
    }
}
