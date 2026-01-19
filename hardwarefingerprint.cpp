#include "hardwarefingerprint.h"
#include <QProcess>
#include <QCryptographicHash>
#include <QDebug>

QString HardwareFingerprint::executeWMICommand(const QString &wmiClass, const QString &property)
{
    QProcess process;
    process.setProcessChannelMode(QProcess::MergedChannels);
    
    // Execute wmic command: wmic <class> get <property> /value (returns cleaner format)
    QString command = QString("wmic %1 get %2 /value").arg(wmiClass, property);
    process.start("cmd", QStringList() << "/c" << command);
    
    if (!process.waitForFinished(5000)) {
        qDebug() << "WMI command timeout:" << command;
        return QString();
    }
    
    QString output = QString::fromLocal8Bit(process.readAllStandardOutput());
    
    // Parse /value format: property=value
    QStringList lines = output.split('\n', QString::SkipEmptyParts);
    QString propertyPattern = property + "=";
    
    for (const QString &line : lines) {
        QString trimmed = line.trimmed();
        if (trimmed.startsWith(propertyPattern, Qt::CaseInsensitive)) {
            // Extract value after the '=' sign
            QString value = trimmed.mid(trimmed.indexOf('=') + 1).trimmed();
            if (!value.isEmpty()) {
                return value;
            }
        }
    }
    
    // Fallback: try parsing standard format (header + value)
    lines = output.split('\n', QString::SkipEmptyParts);
    bool skipFirst = true;
    for (const QString &line : lines) {
        QString trimmed = line.trimmed();
        if (trimmed.isEmpty()) continue;
        
        // Skip header line (contains property name)
        if (skipFirst) {
            if (trimmed.contains(property, Qt::CaseInsensitive)) {
                skipFirst = false;
                continue;
            }
        }
        
        // Return first non-empty value after header
        if (!trimmed.isEmpty() && 
            !trimmed.contains(property, Qt::CaseInsensitive)) {
            return trimmed;
        }
    }
    
    return QString();
}

QString HardwareFingerprint::getMotherboardUUID()
{
    // Get System UUID from baseboard
    QString uuid = executeWMICommand("baseboard", "serialnumber");
    
    // If baseboard serial is empty, try csproduct (computer system product)
    if (uuid.isEmpty()) {
        uuid = executeWMICommand("csproduct", "uuid");
    }
    
    if (uuid.isEmpty()) {
        qDebug() << "Failed to get Motherboard UUID";
    }
    
    return uuid.trimmed();
}

QString HardwareFingerprint::getHardDriveSerial()
{
    // Get physical hard drive serial number (first physical disk drive)
    // On Windows 7, we want the first physical disk, not volumes
    QString serial = executeWMICommand("diskdrive", "serialnumber");
    
    // If we get multiple results (multiple drives), take the first non-empty one
    // The WMI command should return first drive by default
    
    if (serial.isEmpty()) {
        qDebug() << "Failed to get Hard Drive Serial Number";
    }
    
    return serial.trimmed();
}

QString HardwareFingerprint::getCPUId()
{
    // Get CPU ProcessorID
    QString cpuId = executeWMICommand("cpu", "processorid");
    
    if (cpuId.isEmpty()) {
        qDebug() << "Failed to get CPU ID";
    }
    
    return cpuId.trimmed();
}

QString HardwareFingerprint::generateHardwareKey()
{
    // Collect the "Big Three" Legacy Identifiers
    QString motherboardUUID = getMotherboardUUID();
    QString hardDriveSerial = getHardDriveSerial();
    QString cpuId = getCPUId();
    
    // Log collected identifiers (for debugging - remove in production)
    qDebug() << "Motherboard UUID:" << motherboardUUID;
    qDebug() << "Hard Drive Serial:" << hardDriveSerial;
    qDebug() << "CPU ID:" << cpuId;
    
    // Concatenate all identifiers together
    QString combinedIds = motherboardUUID + hardDriveSerial + cpuId;
    
    if (combinedIds.isEmpty()) {
        qDebug() << "Warning: All hardware identifiers are empty!";
        return QString();
    }
    
    // Generate SHA-256 hash to create 64-character fingerprint
    QCryptographicHash hash(QCryptographicHash::Sha256);
    hash.addData(combinedIds.toUtf8());
    QByteArray hashResult = hash.result();
    
    // Convert to hex string (64 characters)
    QString fingerprint = hashResult.toHex();
    
    qDebug() << "Generated Hardware Fingerprint:" << fingerprint;
    
    return fingerprint;
}
