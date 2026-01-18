#include "mod.h"
#include "ui_mod.h"
#include <QPushButton>
#include <QTableWidget>
#include <QTableWidgetItem>
#include <QHeaderView>
#include <QStatusBar>
#include <QList>
#include <QStringList>
#include <windows.h>
#include <setupapi.h>
#include <initguid.h>
#include <devguid.h>
#include <regstr.h>
#include <QDebug>

#ifdef _MSC_VER
#pragma comment(lib, "setupapi.lib")
#endif

// Helper function to detect device type
static QString detectDeviceType(HDEVINFO deviceInfoSet, SP_DEVINFO_DATA* deviceInfoData, const QString& description)
{
    wchar_t buffer[512];
    DWORD requiredSize = 0;
    QString upperDesc = description.toUpper();
    
    // First, check description for common keywords
    if (upperDesc.contains("KEYBOARD")) {
        return "Keyboard";
    }
    if (upperDesc.contains("MOUSE")) {
        return "Mouse";
    }
    if (upperDesc.contains("STORAGE") || upperDesc.contains("DISK") || 
        upperDesc.contains("FLASH") || upperDesc.contains("DRIVE") ||
        upperDesc.contains("USB STOR")) {
        return "Storage Device";
    }
    if (upperDesc.contains("AUDIO") || upperDesc.contains("SPEAKER") || 
        upperDesc.contains("MICROPHONE") || upperDesc.contains("HEADSET")) {
        return "Audio Device";
    }
    if (upperDesc.contains("CAMERA") || upperDesc.contains("WEBCAM") || upperDesc.contains("VIDEO")) {
        return "Camera";
    }
    if (upperDesc.contains("PRINTER")) {
        return "Printer";
    }
    if (upperDesc.contains("NETWORK") || upperDesc.contains("ETHERNET") || upperDesc.contains("WLAN")) {
        return "Network Adapter";
    }
    if (upperDesc.contains("GAMEPAD") || upperDesc.contains("JOYSTICK") || upperDesc.contains("CONTROLLER")) {
        return "Game Controller";
    }
    
    // Get device class GUID
    if (SetupDiGetDeviceRegistryPropertyW(
            deviceInfoSet,
            deviceInfoData,
            SPDRP_CLASSGUID,
            nullptr,
            (PBYTE)buffer,
            sizeof(buffer),
            &requiredSize)) {
        QString classGuid = QString::fromWCharArray(buffer).toUpper();
        
        // Check for HID devices (Human Interface Devices)
        if (classGuid == "{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}" || 
            classGuid == "745A17A0-74D3-11D0-B6FE-00A0C90F57DA") {
            // It's a HID device, check for keyboard/mouse by examining interface
            // Most keyboards and mice are HID devices
            // Try to get more specific info from device class
            if (SetupDiGetDeviceRegistryPropertyW(
                    deviceInfoSet,
                    deviceInfoData,
                    SPDRP_CLASS,
                    nullptr,
                    (PBYTE)buffer,
                    sizeof(buffer),
                    &requiredSize)) {
                QString deviceClass = QString::fromWCharArray(buffer).toUpper();
                if (deviceClass.contains("KEYBOARD")) {
                    return "Keyboard";
                }
                if (deviceClass.contains("MOUSE")) {
                    return "Mouse";
                }
            }
            
            // For HID devices without specific class, check description more carefully
            if (upperDesc.contains("HID")) {
                return "HID Device";
            }
        }
        
        // Check other device class GUIDs
        if (classGuid == "{36FC9E60-C465-11CF-8056-444553540000}" || 
            classGuid == "36FC9E60-C465-11CF-8056-444553540000") {
            return "USB Mass Storage";
        }
        if (classGuid == "{4D36E96C-E325-11CE-BFC1-08002BE10318}" || 
            classGuid == "4D36E96C-E325-11CE-BFC1-08002BE10318") {
            return "Sound/Video Device";
        }
        if (classGuid == "{EEC5AD98-8080-425F-922A-DABF3DE3F69A}" || 
            classGuid == "EEC5AD98-8080-425F-922A-DABF3DE3F69A") {
            return "Camera";
        }
    }
    
    // Get device class name
    if (SetupDiGetDeviceRegistryPropertyW(
            deviceInfoSet,
            deviceInfoData,
            SPDRP_CLASS,
            nullptr,
            (PBYTE)buffer,
            sizeof(buffer),
            &requiredSize)) {
        QString deviceClass = QString::fromWCharArray(buffer);
        if (!deviceClass.isEmpty()) {
            return deviceClass;
        }
    }
    
    return "Unknown";
}

MoD::MoD(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MoD)
{
    ui->setupUi(this);
    
    // Setup table widget
    ui->deviceTable->setColumnCount(7);
    QStringList headers;
    headers << "Device Type" << "Device Name" << "Vendor ID" << "Product ID" << "Serial Number" << "Manufacturer" << "Description";
    ui->deviceTable->setHorizontalHeaderLabels(headers);
    ui->deviceTable->horizontalHeader()->setStretchLastSection(true);
    ui->deviceTable->setSelectionBehavior(QAbstractItemView::SelectRows);
    ui->deviceTable->setEditTriggers(QAbstractItemView::NoEditTriggers);
    
    // Connect refresh button
    connect(ui->refreshButton, &QPushButton::clicked, this, &MoD::onRefreshClicked);
    
    // Initial scan
    onRefreshClicked();
}

MoD::~MoD()
{
    delete ui;
}

void MoD::onRefreshClicked()
{
    ui->statusBar->showMessage("Scanning USB devices...");
    enumerateUSBDevices();
}

void MoD::enumerateUSBDevices()
{
    QList<USBDeviceInfo> devices;
    
    // Get all present devices (not just USB class to avoid infrastructure)
    HDEVINFO deviceInfoSet = SetupDiGetClassDevs(
        nullptr,
        nullptr,
        nullptr,
        DIGCF_PRESENT | DIGCF_ALLCLASSES
    );
    
    if (deviceInfoSet == INVALID_HANDLE_VALUE) {
        ui->statusBar->showMessage("Error: Failed to get device information set");
        return;
    }
    
    SP_DEVINFO_DATA deviceInfoData;
    deviceInfoData.cbSize = sizeof(SP_DEVINFO_DATA);
    
    DWORD deviceIndex = 0;
    while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, &deviceInfoData)) {
        USBDeviceInfo info;
        
        // Get device description - use wide character buffers for Windows API
        wchar_t buffer[512];
        DWORD dataType;
        DWORD requiredSize = 0;
        bool isUSBDevice = false;
        
        // Get hardware ID to check if it's a USB device
        if (SetupDiGetDeviceRegistryPropertyW(
                deviceInfoSet,
                &deviceInfoData,
                SPDRP_HARDWAREID,
                &dataType,
                (PBYTE)buffer,
                sizeof(buffer),
                &requiredSize)) {
            QString hardwareId = QString::fromWCharArray(buffer);
            
            // Check if device is connected via USB (not USB controller/hub)
            if (hardwareId.toUpper().startsWith("USB\\") && 
                hardwareId.contains("VID_") && 
                hardwareId.contains("PID_")) {
                
                // Filter out USB infrastructure devices
                QString upperDesc = hardwareId.toUpper();
                if (upperDesc.contains("ROOTHUB") || 
                    upperDesc.contains("HUB\\") ||
                    upperDesc.contains("HCD\\") ||
                    upperDesc.contains("EHCI\\") ||
                    upperDesc.contains("UHCI\\") ||
                    upperDesc.contains("XHCI\\") ||
                    upperDesc.contains("OHCI\\")) {
                    deviceIndex++;
                    continue; // Skip USB controllers and hubs
                }
                
                isUSBDevice = true;
                info.deviceName = hardwareId;
                
                // Parse VID and PID from hardware ID (format: USB\VID_xxxx&PID_xxxx)
                int vidPos = hardwareId.indexOf("VID_", 0, Qt::CaseInsensitive);
                int pidPos = hardwareId.indexOf("PID_", 0, Qt::CaseInsensitive);
                
                if (vidPos >= 0) {
                    info.vendorID = hardwareId.mid(vidPos + 4, 4);
                }
                
                if (pidPos >= 0) {
                    info.productID = hardwareId.mid(pidPos + 4, 4);
                }
            } else {
                deviceIndex++;
                continue; // Not a USB device, skip
            }
        } else {
            deviceIndex++;
            continue; // Can't get hardware ID, skip
        }
        
        // Only process if it's a USB peripheral device
        if (!isUSBDevice) {
            deviceIndex++;
            continue;
        }
        
        // Get device description - Windows API uses wide characters
        if (SetupDiGetDeviceRegistryPropertyW(
                deviceInfoSet,
                &deviceInfoData,
                SPDRP_DEVICEDESC,
                &dataType,
                (PBYTE)buffer,
                sizeof(buffer),
                &requiredSize)) {
            QString description = QString::fromWCharArray(buffer);
            
            // Additional filter: skip USB controllers and root hubs (infrastructure)
            QString upperDesc = description.toUpper();
            if (upperDesc.contains("USB ROOT HUB") ||
                upperDesc.contains("ROOT HUB") ||
                upperDesc.contains("USB HOST CONTROLLER") ||
                upperDesc.contains("HOST CONTROLLER") ||
                (upperDesc.contains("USB HUB") && !upperDesc.contains("COMPOSITE"))) {
                deviceIndex++;
                continue;
            }
            
            info.description = description;
        }
        
        // Detect device type
        info.deviceType = detectDeviceType(deviceInfoSet, &deviceInfoData, info.description);
        
        // Get manufacturer
        if (SetupDiGetDeviceRegistryPropertyW(
                deviceInfoSet,
                &deviceInfoData,
                SPDRP_MFG,
                &dataType,
                (PBYTE)buffer,
                sizeof(buffer),
                &requiredSize)) {
            info.manufacturer = QString::fromWCharArray(buffer);
        }
        
        // Get device instance ID (contains more info including serial) - uses wide characters
        if (SetupDiGetDeviceInstanceIdW(
                deviceInfoSet,
                &deviceInfoData,
                buffer,
                sizeof(buffer) / sizeof(wchar_t),
                &requiredSize)) {
            QString instanceId = QString::fromWCharArray(buffer);
            
            // Try to extract serial number from instance ID
            // Format might be like: USB\VID_xxxx&PID_xxxx\XXXXXXXX
            int lastSlash = instanceId.lastIndexOf("\\");
            if (lastSlash >= 0 && lastSlash < instanceId.length() - 1) {
                info.serialNumber = instanceId.mid(lastSlash + 1);
            }
        }
        
        deviceIndex++;
        
        // Only add devices that have VID/PID (actual USB peripherals)
        if (!info.vendorID.isEmpty() && !info.productID.isEmpty()) {
            devices.append(info);
        }
    }
    
    SetupDiDestroyDeviceInfoList(deviceInfoSet);
    
    populateDeviceTable(devices);
    ui->statusBar->showMessage(QString("Found %1 USB peripheral device(s)").arg(devices.size()));
}

void MoD::populateDeviceTable(const QList<USBDeviceInfo>& devices)
{
    ui->deviceTable->setRowCount(devices.size());
    
    for (int i = 0; i < devices.size(); i++) {
        const USBDeviceInfo& info = devices[i];
        
        ui->deviceTable->setItem(i, 0, new QTableWidgetItem(info.deviceType));
        ui->deviceTable->setItem(i, 1, new QTableWidgetItem(info.deviceName));
        ui->deviceTable->setItem(i, 2, new QTableWidgetItem(info.vendorID));
        ui->deviceTable->setItem(i, 3, new QTableWidgetItem(info.productID));
        ui->deviceTable->setItem(i, 4, new QTableWidgetItem(info.serialNumber));
        ui->deviceTable->setItem(i, 5, new QTableWidgetItem(info.manufacturer));
        ui->deviceTable->setItem(i, 6, new QTableWidgetItem(info.description));
    }
    
    ui->deviceTable->resizeColumnsToContents();
}
