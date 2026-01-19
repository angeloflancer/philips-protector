#ifndef HARDWAREFINGERPRINT_H
#define HARDWAREFINGERPRINT_H

#include <QString>

/**
 * @brief HardwareFingerprint class for generating unique hardware-based keys
 * 
 * This class collects hardware identifiers from Windows 7+ machines using WMI
 * and generates a unique SHA-256 hash fingerprint. It combines:
 * - Motherboard UUID
 * - Hard Drive Serial Number
 * - CPU Processor ID
 */
class HardwareFingerprint
{
public:
    /**
     * @brief Generate a unique hardware fingerprint key
     * @return 64-character hexadecimal SHA-256 hash string, or empty string on failure
     */
    static QString generateHardwareKey();

private:
    /**
     * @brief Get motherboard UUID from system
     * @return Motherboard UUID string
     */
    static QString getMotherboardUUID();

    /**
     * @brief Get physical hard drive serial number
     * @return Hard drive serial number string
     */
    static QString getHardDriveSerial();

    /**
     * @brief Get CPU Processor ID
     * @return CPU Processor ID string
     */
    static QString getCPUId();

    /**
     * @brief Execute a WMI command and parse the result
     * @param wmiClass WMI class name (e.g., "baseboard", "diskdrive", "cpu")
     * @param property Property name to retrieve (e.g., "serialnumber", "uuid", "processorid")
     * @return Property value, or empty string on failure
     */
    static QString executeWMICommand(const QString &wmiClass, const QString &property);
};

#endif // HARDWAREFINGERPRINT_H
