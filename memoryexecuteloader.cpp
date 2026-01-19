#include "memoryexecuteloader.h"
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
#include <cstring>

// For Windows-specific functions
#ifdef Q_OS_WIN
#include <windows.h>
#include <wincrypt.h>
#include <tlhelp32.h>
#include <psapi.h>
#ifdef _MSC_VER
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "psapi.lib")
#endif
#endif

QByteArray MemoryExecuteLoader::deriveKey(const QString &password)
{
    // Use SHA-256 to derive a 256-bit key from the password
    QCryptographicHash hash(QCryptographicHash::Sha256);
    hash.addData(password.toUtf8());
    return hash.result(); // Returns 32 bytes (256 bits)
}

QByteArray MemoryExecuteLoader::generateIV()
{
    // Generate a random 16-byte IV for AES-CBC
    QByteArray iv(16, 0);
    
#ifdef Q_OS_WIN
    // Use Windows CryptGenRandom for better randomness
    HCRYPTPROV hProv = 0;
    if (CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT)) {
        CryptGenRandom(hProv, 16, reinterpret_cast<BYTE*>(iv.data()));
        CryptReleaseContext(hProv, 0);
    } else {
        // Fallback to time-based pseudo-random
        QTime time = QTime::currentTime();
        qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
        for (int i = 0; i < 16; ++i) {
            iv[i] = static_cast<char>(qrand() % 256);
        }
    }
#else
    // For non-Windows, use time-based pseudo-random
    QTime time = QTime::currentTime();
    qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
    for (int i = 0; i < 16; ++i) {
        iv[i] = static_cast<char>(qrand() % 256);
    }
#endif
    
    return iv;
}

QByteArray MemoryExecuteLoader::encryptData(const QByteArray &data, 
                                            const QByteArray &key, 
                                            const QByteArray &iv)
{
#ifdef Q_OS_WIN
    // Use Windows CryptoAPI for AES-256-CBC encryption
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
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed";
        return QByteArray();
    }
    
    // Encrypt the data
    DWORD dataLen = static_cast<DWORD>(data.size());
    QByteArray encrypted(data.size() + 1024, 0); // Extra space for padding
    
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
    
    // Prepend IV to encrypted data
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

QByteArray MemoryExecuteLoader::decryptData(const QByteArray &encryptedData, 
                                            const QByteArray &key)
{
    if (encryptedData.size() < 16) {
        qDebug() << "Encrypted data too small (missing IV)";
        return QByteArray();
    }
    
    // Extract IV (first 16 bytes) and encrypted content
    QByteArray iv = encryptedData.left(16);
    QByteArray encryptedContent = encryptedData.mid(16);
    
#ifdef Q_OS_WIN
    // Use Windows CryptoAPI for AES-256-CBC decryption
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
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed";
        return QByteArray();
    }
    
    // Decrypt the data
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

bool MemoryExecuteLoader::encryptExecutable(const QString &inputFilePath, 
                                           const QString &outputFilePath, 
                                           const QString &password)
{
    qDebug() << "=== ENCRYPTION START (MemoryExecuteLoader) ===";
    
    QFile inputFile(inputFilePath);
    if (!inputFile.open(QIODevice::ReadOnly)) {
        qDebug() << "Failed to open input file:" << inputFilePath;
        return false;
    }
    
    QByteArray fileData = inputFile.readAll();
    inputFile.close();
    
    if (fileData.isEmpty()) {
        qDebug() << "Input file is empty";
        return false;
    }
    
    qDebug() << "Input file size:" << fileData.size() << "bytes";
    
    // Derive encryption key from password
    QByteArray key = deriveKey(password);
    qDebug() << "Derived key size:" << key.size() << "bytes";
    
    // Generate IV
    QByteArray iv = generateIV();
    qDebug() << "Generated IV:" << iv.toHex();
    
    // Encrypt the data
    QByteArray encryptedData = encryptData(fileData, key, iv);
    
    if (encryptedData.isEmpty()) {
        qDebug() << "Encryption failed";
        return false;
    }
    
    qDebug() << "Encrypted data size:" << encryptedData.size() << "bytes";
    
    // Save encrypted file
    QFile outputFile(outputFilePath);
    if (!outputFile.open(QIODevice::WriteOnly)) {
        qDebug() << "Failed to create output file:" << outputFilePath;
        return false;
    }
    
    qint64 written = outputFile.write(encryptedData);
    outputFile.flush();
    outputFile.close();
    
    if (written != static_cast<qint64>(encryptedData.size())) {
        qDebug() << "Failed to write all encrypted data to file";
        return false;
    }
    
    qDebug() << "Successfully encrypted executable:" << inputFilePath << "->" << outputFilePath;
    qDebug() << "=== ENCRYPTION END ===";
    return true;
}

#ifdef Q_OS_WIN
// Windows-specific function to execute from memory
int MemoryExecuteLoader::executeFromMemory(const QByteArray &executableData,
                                          const QStringList &arguments)
{
    QString tempFilePath;
    
    // Create a temporary file for execution (Windows doesn't support true in-memory execution easily)
    // Use a scope block to ensure file handle is fully released
    {
        QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "mem_exec_XXXXXX.exe");
        tempFile.setAutoRemove(false); // Don't auto-remove, we'll delete manually after process finishes
        
        if (!tempFile.open()) {
            qDebug() << "Failed to create temporary file for execution";
            return -1;
        }
        
        tempFilePath = tempFile.fileName();
        
        // Write executable data
        qint64 written = tempFile.write(executableData);
        
        // Flush the data to disk before closing (important on Windows)
        tempFile.flush();
        
        // Close the file explicitly
        tempFile.close();
        
        if (written != static_cast<qint64>(executableData.size())) {
            qDebug() << "Failed to write executable data to temp file";
            QFile::remove(tempFilePath);
            return -1;
        }
    } // Scope ends here, ensuring QTemporaryFile is fully destroyed and file handle released
    
    // On Windows, wait a moment to ensure file handle is fully released
    // This is necessary because Windows file handles can take a moment to release
    QThread::msleep(100); // Small delay to let Windows release the file handle
    
    // Verify the file exists and has content
    QFileInfo tempFileInfo(tempFilePath);
    tempFileInfo.refresh(); // Force refresh to ensure we have latest info
    if (!tempFileInfo.exists() || tempFileInfo.size() == 0) {
        qDebug() << "Temporary file does not exist or is empty";
        qDebug() << "File path:" << tempFilePath;
        qDebug() << "File exists:" << tempFileInfo.exists();
        qDebug() << "File size:" << tempFileInfo.size();
        QFile::remove(tempFilePath);
        return -1;
    }
    
    // Verify we can actually access the file before trying to execute it
    QFile testAccess(tempFilePath);
    if (!testAccess.open(QIODevice::ReadOnly)) {
        qDebug() << "Cannot open temporary file for reading - file may still be locked";
        qDebug() << "File error:" << testAccess.errorString();
        QFile::remove(tempFilePath);
        return -1;
    }
    testAccess.close(); // Close immediately after verifying access
    
    // Execute using QProcess
    // Start detached so the launcher can exit immediately
    qint64 pid = 0;
    qDebug() << "Starting process from temp file (detached):" << tempFilePath;
    bool started = QProcess::startDetached(tempFilePath, arguments, QDir::tempPath(), &pid);
    
    // Best-effort clean up: on Windows, deleting an executable in use will
    // remove the directory entry while the image stays mapped, so it cannot be copied.
    QFile::remove(tempFilePath);
    
    if (!started) {
        qDebug() << "Failed to start process";
        return -1;
    }
    
    qDebug() << "Process started (PID:" << pid << "), launcher exiting";
    // We don't wait; caller should exit so only the payload remains visible.
    return 0;
}
#else
int MemoryExecuteLoader::executeFromMemory(const QByteArray &executableData,
                                          const QStringList &arguments)
{
    QString tempFilePath;
    
    // For non-Windows, use similar approach
    {
        QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "mem_exec_XXXXXX");
        tempFile.setAutoRemove(false); // Don't auto-remove, we'll delete manually
        
        if (!tempFile.open()) {
            qDebug() << "Failed to create temporary file for execution";
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
    } // Scope ends here, ensuring file handle is released
    
    // Make executable
    QFile::setPermissions(tempFilePath, QFile::ReadUser | QFile::WriteUser | QFile::ExeUser);
    
    QThread::msleep(100);
    
    qint64 pid = 0;
    bool started = QProcess::startDetached(tempFilePath, arguments, QDir::tempPath(), &pid);
    
    QFile::remove(tempFilePath);
    
    if (!started) {
        qDebug() << "Failed to start process";
        return -1;
    }
    
    qDebug() << "Process started (PID:" << pid << "), launcher exiting";
    return 0;
}
#endif

int MemoryExecuteLoader::decryptAndExecuteFromMemory(const QString &encryptedFilePath,
                                                     const QString &password,
                                                     const QStringList &arguments)
{
    qDebug() << "=== MEMORY EXECUTION START ===";
    qDebug() << "Encrypted file:" << encryptedFilePath;
    
    // Read encrypted file
    QFile encryptedFile(encryptedFilePath);
    if (!encryptedFile.open(QIODevice::ReadOnly)) {
        qDebug() << "Failed to open encrypted file";
        return -1;
    }
    
    QByteArray encryptedData = encryptedFile.readAll();
    encryptedFile.close();
    
    if (encryptedData.size() < 16) {
        qDebug() << "Encrypted file too small";
        return -1;
    }
    
    qDebug() << "Encrypted data size:" << encryptedData.size() << "bytes";
    
    // Derive decryption key from password
    QByteArray key = deriveKey(password);
    qDebug() << "Derived key size:" << key.size() << "bytes";
    
    // Decrypt in memory
    qDebug() << "Decrypting in memory...";
    QByteArray decryptedData = decryptData(encryptedData, key);
    
    if (decryptedData.isEmpty()) {
        qDebug() << "Decryption failed - wrong password or corrupted file";
        return -1;
    }
    
    qDebug() << "Decrypted data size:" << decryptedData.size() << "bytes";
    
    // Verify it's a valid PE executable (Windows)
#ifdef Q_OS_WIN
    if (decryptedData.size() >= 2) {
        if (decryptedData[0] != 'M' || decryptedData[1] != 'Z') {
            qDebug() << "Decrypted data is not a valid PE executable";
            return -1;
        }
    }
#endif
    
    // Execute from memory (actually uses very short-lived temp file)
    qDebug() << "Executing from memory...";
    int exitCode = executeFromMemory(decryptedData, arguments);
    
    qDebug() << "Process exit code:" << exitCode;
    qDebug() << "=== MEMORY EXECUTION END ===";
    
    return exitCode;
}

bool MemoryExecuteLoader::createSelfDecryptingLoader(const QString &encryptedFilePath,
                                                     const QString &loaderOutputPath,
                                                     const QString &password)
{
    Q_UNUSED(encryptedFilePath);
    Q_UNUSED(loaderOutputPath);
    Q_UNUSED(password);
    
    qDebug() << "=== CREATING SELF-DECRYPTING LOADER ===";
    qDebug() << "This feature requires a separate loader executable template";
    qDebug() << "For now, use decryptAndExecuteFromMemory() instead";
    
    // Note: Creating a true self-decrypting loader requires:
    // 1. A loader stub executable template
    // 2. Embedding the encrypted payload into the loader
    // 3. Embedding the password (or prompting for it)
    // 4. The loader decrypts and executes the payload in memory
    
    // This is a placeholder - full implementation would require
    // a loader stub executable that we inject the encrypted payload into
    
    return false;
}
