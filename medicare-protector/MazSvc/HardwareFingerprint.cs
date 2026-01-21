using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Collects hardware identification information for license generation
    /// </summary>
    public static class HardwareFingerprint
    {
        /// <summary>
        /// Gets all hardware information as a HardwareInfo object
        /// </summary>
        public static HardwareInfo GetHardwareInfo()
        {
            HardwareInfo info = new HardwareInfo();
            info.CpuId = GetCpuId();
            info.MotherboardSerial = GetMotherboardSerial();
            info.HardDriveSerial = GetHardDriveSerial();
            info.MacAddress = GetMacAddress();
            info.BiosSerial = GetBiosSerial();
            info.MachineGuid = GetMachineGuid();
            return info;
        }

        /// <summary>
        /// Gets the CPU Processor ID
        /// </summary>
        private static string GetCpuId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj["ProcessorId"];
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the motherboard serial number
        /// </summary>
        private static string GetMotherboardSerial()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj["SerialNumber"];
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the primary hard drive serial number
        /// </summary>
        private static string GetHardDriveSerial()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj["SerialNumber"];
                        if (value != null)
                        {
                            return value.ToString().Trim();
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the MAC address of the first active network adapter
        /// </summary>
        private static string GetMacAddress()
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in interfaces)
                {
                    // Skip loopback and tunnel adapters
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    // Get first active adapter
                    if (adapter.OperationalStatus == OperationalStatus.Up)
                    {
                        PhysicalAddress address = adapter.GetPhysicalAddress();
                        byte[] bytes = address.GetAddressBytes();
                        if (bytes.Length > 0)
                        {
                            string[] hexParts = new string[bytes.Length];
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                hexParts[i] = bytes[i].ToString("X2");
                            }
                            return string.Join("-", hexParts);
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the BIOS serial number
        /// </summary>
        private static string GetBiosSerial()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj["SerialNumber"];
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the Windows Machine GUID
        /// </summary>
        private static string GetMachineGuid()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("MachineGuid");
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }
    }
}
