#include "executableencryptor.h"
#include "hardwarefingerprint.h"
#include <QFile>
#include <QByteArray>
#include <QCryptographicHash>
#include <QProcess>
#include <QDebug>
#include <QTemporaryFile>
#include <QDataStream>
#include <QIODevice>
#include <QTime>
#include <QThread>
#include <QDir>
#include <QFileInfo>
#include <cstring>

// For Windows-specific functions
#ifdef Q_OS_WIN
#include <windows.h>
#include <wincrypt.h>
// Link advapi32.lib for Windows CryptoAPI
// MSVC-specific pragma, ignored by MinGW/GCC (linker will handle it via .pro file)
#ifdef _MSC_VER
#pragma comment(lib, "advapi32.lib")
#endif
#endif

QByteArray ExecutableEncryptor::deriveKey(const QString &password)
{
    // Use SHA-256 to derive a 256-bit key from the password
    // For multiple iterations, we could use PBKDF2, but SHA-256 is sufficient for this use case
    QCryptographicHash hash(QCryptographicHash::Sha256);
    hash.addData(password.toUtf8());
    return hash.result(); // Returns 32 bytes (256 bits)
}

QByteArray ExecutableEncryptor::generateIV()
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
        // Note: qsrand/qrand deprecated, but kept for Qt 5.11.3 compatibility
        QTime time = QTime::currentTime();
        qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
        for (int i = 0; i < 16; ++i) {
            iv[i] = static_cast<char>(qrand() % 256);
        }
    }
#else
    // For non-Windows, use time-based pseudo-random
    // Note: qsrand/qrand deprecated, but kept for Qt 5.11.3 compatibility
    QTime time = QTime::currentTime();
    qsrand(static_cast<uint>(time.msec() + time.second() * 1000));
    for (int i = 0; i < 16; ++i) {
        iv[i] = static_cast<char>(qrand() % 256);
    }
#endif
    
    return iv;
}

QByteArray ExecutableEncryptor::xorEncrypt(const QByteArray &data, const QByteArray &key)
{
    if (key.isEmpty()) {
        return data; // No encryption if key is empty
    }

    QByteArray result;
    result.reserve(data.size());
    
    for (int i = 0; i < data.size(); ++i) {
        result.append(data[i] ^ key[i % key.size()]);
    }
    
    return result;
}

QByteArray ExecutableEncryptor::xorDecrypt(const QByteArray &encryptedData, const QByteArray &key)
{
    // XOR encryption/decryption is the same operation
    return xorEncrypt(encryptedData, key);
}

QByteArray ExecutableEncryptor::encryptData(const QByteArray &data, 
                                            const QByteArray &key, 
                                            const QByteArray &iv)
{
#ifdef Q_OS_WIN
    // Use Windows CryptoAPI for AES-256-CBC encryption
    HCRYPTPROV hProv = 0;
    HCRYPTKEY hKey = 0;
    HCRYPTHASH hHash = 0;
    
    if (!CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_AES, CRYPT_VERIFYCONTEXT)) {
        qDebug() << "CryptAcquireContext failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
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
        qDebug() << "CryptEncrypt failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, key);
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    encrypted.resize(static_cast<int>(dataLen));
    
    // Prepend IV to encrypted data
    QByteArray result = iv + encrypted;
    
    CryptDestroyKey(hKey);
    CryptDestroyHash(hHash);
    CryptReleaseContext(hProv, 0);
    
    return result;
#else
    // For non-Windows platforms, use XOR encryption
    qDebug() << "AES encryption not available on this platform, using XOR encryption";
    QByteArray xorEncrypted = xorEncrypt(data, key);
    return iv + xorEncrypted; // Prepend IV to maintain format consistency
#endif
}

QByteArray ExecutableEncryptor::decryptData(const QByteArray &encryptedData, 
                                            const QByteArray &key, 
                                            const QByteArray &iv)
{
#ifdef Q_OS_WIN
    // Use Windows CryptoAPI for AES-256-CBC decryption
    HCRYPTPROV hProv = 0;
    HCRYPTKEY hKey = 0;
    HCRYPTHASH hHash = 0;
    
    if (!CryptAcquireContext(&hProv, nullptr, nullptr, PROV_RSA_AES, CRYPT_VERIFYCONTEXT)) {
        qDebug() << "CryptAcquireContext failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, key); // encryptedData doesn't include IV for XOR
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, key);
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, key);
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, key);
    }
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, key);
    }
    
    // Decrypt the data
    DWORD encryptedLen = static_cast<DWORD>(encryptedData.size());
    QByteArray decrypted(encryptedData);
    
    if (!CryptDecrypt(hKey, 0, TRUE, 0, reinterpret_cast<BYTE*>(decrypted.data()), &encryptedLen)) {
        DWORD error = GetLastError();
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDecrypt failed with error:" << error << ", falling back to XOR decryption";
        qDebug() << "Encrypted data size:" << encryptedData.size() << ", encryptedLen:" << encryptedLen;
        return xorDecrypt(encryptedData, key);
    }
    
    decrypted.resize(static_cast<int>(encryptedLen));
    
    CryptDestroyKey(hKey);
    CryptDestroyHash(hHash);
    CryptReleaseContext(hProv, 0);
    
    return decrypted;
#else
    // For non-Windows platforms, use XOR decryption
    qDebug() << "AES decryption not available on this platform, using XOR decryption";
    return xorDecrypt(encryptedData, key);
#endif
}

bool ExecutableEncryptor::encryptExecutable(const QString &inputFilePath, 
                                            const QString &outputFilePath, 
                                            const QString &encryptionKey)
{
    qDebug() << "=== ENCRYPTION START ===";
    qDebug() << "Input file:" << inputFilePath;
    qDebug() << "Output file:" << outputFilePath;
    
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
    qDebug() << "Input starts with:" << (fileData.size() >= 2 ? QString(fileData.left(2)) : "N/A");
    qDebug() << "Encryption key (original):" << encryptionKey.left(16) << "...";
    qDebug() << "Encryption key length:" << encryptionKey.length();
    
    // Derive encryption key
    QByteArray key = deriveKey(encryptionKey);
    qDebug() << "Derived key size:" << key.size() << "bytes";
    qDebug() << "Derived key hex:" << key.toHex().left(32) << "...";
    
    // Generate IV
    QByteArray iv = generateIV();
    qDebug() << "Generated IV:" << iv.toHex();
    
    // Encrypt the data
    qDebug() << "Calling encryptData...";
    QByteArray encryptedData = encryptData(fileData, key, iv);
    qDebug() << "Encrypted data size:" << encryptedData.size() << "bytes";
    
    if (encryptedData.size() < 16) {
        qDebug() << "ERROR: Encrypted data is too small!";
        return false;
    }
    
    // Verify format: IV (16 bytes) + encrypted content
    QByteArray resultIV = encryptedData.left(16);
    QByteArray resultEncrypted = encryptedData.mid(16);
    qDebug() << "Result IV matches generated IV:" << (resultIV == iv ? "YES" : "NO");
    qDebug() << "Result encrypted content size:" << resultEncrypted.size() << "bytes";
    
    // Save encrypted file: IV (16 bytes) + encrypted data
    QFile outputFile(outputFilePath);
    if (!outputFile.open(QIODevice::WriteOnly)) {
        qDebug() << "Failed to create output file:" << outputFilePath;
        return false;
    }
    
    qint64 written = outputFile.write(encryptedData);
    outputFile.close();
    
    if (written != static_cast<qint64>(encryptedData.size())) {
        qDebug() << "Failed to write all encrypted data to file";
        return false;
    }
    
    qDebug() << "Successfully encrypted executable:" << inputFilePath << "->" << outputFilePath;
    qDebug() << "=== ENCRYPTION END ===";
    return true;
}

bool ExecutableEncryptor::decryptExecutable(const QString &encryptedFilePath, 
                                            const QString &outputFilePath, 
                                            const QString &decryptionKey)
{
    qDebug() << "=== DECRYPTION START ===";
    qDebug() << "Encrypted file:" << encryptedFilePath;
    qDebug() << "Output file:" << outputFilePath;
    
    QFile encryptedFile(encryptedFilePath);
    if (!encryptedFile.open(QIODevice::ReadOnly)) {
        qDebug() << "Failed to open encrypted file:" << encryptedFilePath;
        return false;
    }
    
    QByteArray encryptedData = encryptedFile.readAll();
    encryptedFile.close();
    
    if (encryptedData.size() < 16) {
        qDebug() << "Encrypted file is too small (missing IV)";
        return false;
    }
    
    qDebug() << "Encrypted file size:" << encryptedData.size() << "bytes";
    
    // Extract IV (first 16 bytes) and encrypted content
    QByteArray iv = encryptedData.left(16);
    QByteArray encryptedContent = encryptedData.mid(16);
    
    qDebug() << "Extracted IV:" << iv.toHex();
    qDebug() << "Encrypted content size:" << encryptedContent.size() << "bytes";
    
    qDebug() << "Decryption key (original):" << decryptionKey.left(16) << "...";
    qDebug() << "Decryption key length:" << decryptionKey.length();
    
    // Derive decryption key
    QByteArray key = deriveKey(decryptionKey);
    qDebug() << "Derived key size:" << key.size() << "bytes";
    qDebug() << "Derived key hex:" << key.toHex().left(32) << "...";
    
    // Debug: Show if keys match (for troubleshooting)
    qDebug() << "Key derivation verification complete";
    
    // Decrypt the data - try AES first
    qDebug() << "Attempting AES decryption...";
    QByteArray decryptedData = decryptData(encryptedContent, key, iv);
    qDebug() << "AES decryption result size:" << decryptedData.size() << "bytes";
    
    // Verify decrypted data looks valid (check PE header for Windows executables)
    bool isValidDecryption = false;
#ifdef Q_OS_WIN
    if (decryptedData.size() >= 2) {
        // Check for MZ header (PE executable signature)
        if (decryptedData[0] == 'M' && decryptedData[1] == 'Z') {
            isValidDecryption = true;
            qDebug() << "Decryption successful - valid PE header found";
        } else {
            qDebug() << "AES decryption result does not appear valid (no PE header)";
            qDebug() << "First bytes:" << decryptedData.left(16).toHex();
        }
    } else if (!decryptedData.isEmpty()) {
        // If we got some data but it's less than 2 bytes, it's probably wrong
        isValidDecryption = false;
    }
#else
    // For non-Windows, just check if we got data
    isValidDecryption = !decryptedData.isEmpty();
#endif
    
    // If AES decryption didn't produce valid results, try XOR decryption
    // This handles the case where encryption used XOR (AES failed) but we tried AES first
    if (!isValidDecryption && !encryptedContent.isEmpty()) {
        qDebug() << "Attempting XOR decryption as fallback...";
        QByteArray xorDecrypted = xorDecrypt(encryptedContent, key);
        
        // Verify XOR result
        bool xorValid = false;
#ifdef Q_OS_WIN
        if (xorDecrypted.size() >= 2 && xorDecrypted[0] == 'M' && xorDecrypted[1] == 'Z') {
            xorValid = true;
            qDebug() << "XOR decryption successful - valid PE header found";
        }
#else
        xorValid = !xorDecrypted.isEmpty();
#endif
        
        if (xorValid) {
            decryptedData = xorDecrypted;
            isValidDecryption = true;
        } else {
            qDebug() << "XOR decryption also failed";
        }
    }
    
    if (decryptedData.isEmpty() || !isValidDecryption) {
        qDebug() << "ERROR: Decryption failed - neither AES nor XOR produced valid results";
        qDebug() << "Encrypted content size:" << encryptedContent.size();
        qDebug() << "Key size:" << key.size();
        qDebug() << "IV size:" << iv.size();
        qDebug() << "Decrypted data empty:" << decryptedData.isEmpty();
        if (!decryptedData.isEmpty()) {
            qDebug() << "Decrypted data first 32 bytes:" << decryptedData.left(32).toHex();
        }
        qDebug() << "=== DECRYPTION FAILED ===";
        return false;
    }
    
    qDebug() << "Decryption successful!";
    qDebug() << "Decrypted data size:" << decryptedData.size() << "bytes";
    qDebug() << "Decrypted data starts with:" << (decryptedData.size() >= 2 ? QString(decryptedData.left(2)) : "N/A");
    
    // Save decrypted file
    {
        QFile outputFile(outputFilePath);
        if (!outputFile.open(QIODevice::WriteOnly)) {
            qDebug() << "Failed to create output file:" << outputFilePath;
            return false;
        }
        
        qint64 written = outputFile.write(decryptedData);
        
        // Flush the data to disk before closing (important on Windows)
        outputFile.flush();
        
        // Close the file explicitly
        outputFile.close();
        
        if (written != static_cast<qint64>(decryptedData.size())) {
            qDebug() << "Failed to write all decrypted data to file";
            return false;
        }
    } // Scope ends here, ensuring QFile is fully destroyed and file handle released
    
    // On Windows, we need to ensure the file handle is fully released
    // Force filesystem sync by reading file info (this triggers OS to release locks)
    QFileInfo verifyInfo(outputFilePath);
    verifyInfo.refresh();
    
    qDebug() << "Successfully decrypted executable:" << encryptedFilePath << "->" << outputFilePath;
    qDebug() << "=== DECRYPTION END ===";
    return true;
}

bool ExecutableEncryptor::verifyHardwareKey(const QString &keyToVerify)
{
    QString currentHardwareKey = HardwareFingerprint::generateHardwareKey();
    return (keyToVerify == currentHardwareKey && !currentHardwareKey.isEmpty());
}

int ExecutableEncryptor::decryptAndRun(const QString &encryptedFilePath, 
                                       const QString &decryptionKey,
                                       const QStringList &arguments)
{
    QString tempFilePath;
    
    // Create a temporary file for the decrypted executable
    {
        QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "decrypted_exec_XXXXXX.exe");
        tempFile.setAutoRemove(false); // Don't auto-remove so we can run it
        if (!tempFile.open()) {
            qDebug() << "Failed to create temporary file";
            return -1;
        }
        
        tempFilePath = tempFile.fileName();
        tempFile.flush();
        tempFile.close();
    } // Ensure tempFile is fully destroyed and file handle released
    
    // Decrypt the executable
    if (!decryptExecutable(encryptedFilePath, tempFilePath, decryptionKey)) {
        qDebug() << "Failed to decrypt executable";
        qDebug() << "Encrypted file:" << encryptedFilePath;
        qDebug() << "Temp file:" << tempFilePath;
        QFile::remove(tempFilePath);
        return -1;
    }
    
    // On Windows, wait a moment to ensure file handle is fully released
    // This is necessary because Windows file handles can take a moment to release
#ifdef Q_OS_WIN
    QThread::msleep(100); // Small delay to let Windows release the file handle
#endif
    
    // Verify the decrypted file exists and has content
    QFileInfo tempFileInfo(tempFilePath);
    tempFileInfo.refresh(); // Force refresh to ensure we have latest info
    if (!tempFileInfo.exists() || tempFileInfo.size() == 0) {
        qDebug() << "Decrypted file does not exist or is empty";
        qDebug() << "Temp file path:" << tempFilePath;
        qDebug() << "File exists:" << tempFileInfo.exists();
        qDebug() << "File size:" << tempFileInfo.size();
        QFile::remove(tempFilePath);
        return -1;
    }
    
    // Verify we can actually access the file before trying to execute it
    QFile testAccess(tempFilePath);
    if (!testAccess.open(QIODevice::ReadOnly)) {
        qDebug() << "Cannot open decrypted file for reading - file may still be locked";
        qDebug() << "File error:" << testAccess.errorString();
        QFile::remove(tempFilePath);
        return -1;
    }
    testAccess.close(); // Close immediately after verifying access
    
    // Run the decrypted executable
    QProcess process;
    qDebug() << "Starting process:" << tempFilePath;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted(5000)) {
        qDebug() << "Failed to start decrypted executable";
        qDebug() << "Process error:" << process.errorString();
        qDebug() << "Temp file path:" << tempFilePath;
        QFile::remove(tempFilePath);
        return -1;
    }
    
    // Wait for the process to finish
    process.waitForFinished(-1);
    
    int exitCode = process.exitCode();
    
    // Clean up temporary file
    QFile::remove(tempFilePath);
    
    return exitCode;
}

int ExecutableEncryptor::decryptAndRunWithHardwareKey(const QString &encryptedFilePath,
                                                      const QStringList &arguments)
{
    // Generate hardware key
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    
    if (hardwareKey.isEmpty()) {
        qDebug() << "Failed to generate hardware key";
        return -1;
    }
    
    // Decrypt and run using the hardware key
    return decryptAndRun(encryptedFilePath, hardwareKey, arguments);
}
