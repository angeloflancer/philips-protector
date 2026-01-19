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
#include <QDir>
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

QByteArray ExecutableEncryptor::xorEncrypt(const QByteArray &data, const QString &key)
{
    QByteArray keyBytes = key.toUtf8();
    if (keyBytes.isEmpty()) {
        return data; // No encryption if key is empty
    }

    QByteArray result;
    result.reserve(data.size());
    
    for (int i = 0; i < data.size(); ++i) {
        result.append(data[i] ^ keyBytes[i % keyBytes.size()]);
    }
    
    return result;
}

QByteArray ExecutableEncryptor::xorDecrypt(const QByteArray &encryptedData, const QString &key)
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
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
        return iv + xorEncrypted; // Prepend IV to maintain format consistency
    }
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed, falling back to XOR encryption";
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
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
        QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
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
    QByteArray xorEncrypted = xorEncrypt(data, QString::fromUtf8(key));
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
        return xorDecrypt(encryptedData, QString::fromUtf8(key)); // encryptedData doesn't include IV for XOR
    }
    
    if (!CryptCreateHash(hProv, CALG_SHA_256, 0, 0, &hHash)) {
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptCreateHash failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, QString::fromUtf8(key));
    }
    
    if (!CryptHashData(hHash, reinterpret_cast<const BYTE*>(key.data()), static_cast<DWORD>(key.size()), 0)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptHashData failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, QString::fromUtf8(key));
    }
    
    if (!CryptDeriveKey(hProv, CALG_AES_256, hHash, CRYPT_EXPORTABLE, &hKey)) {
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDeriveKey failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, QString::fromUtf8(key));
    }
    
    // Set IV for CBC mode
    if (!CryptSetKeyParam(hKey, KP_IV, reinterpret_cast<const BYTE*>(iv.data()), 0)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptSetKeyParam failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, QString::fromUtf8(key));
    }
    
    // Decrypt the data
    DWORD encryptedLen = static_cast<DWORD>(encryptedData.size());
    QByteArray decrypted(encryptedData);
    
    if (!CryptDecrypt(hKey, 0, TRUE, 0, reinterpret_cast<BYTE*>(decrypted.data()), &encryptedLen)) {
        CryptDestroyKey(hKey);
        CryptDestroyHash(hHash);
        CryptReleaseContext(hProv, 0);
        qDebug() << "CryptDecrypt failed, falling back to XOR decryption";
        return xorDecrypt(encryptedData, QString::fromUtf8(key));
    }
    
    decrypted.resize(static_cast<int>(encryptedLen));
    
    CryptDestroyKey(hKey);
    CryptDestroyHash(hHash);
    CryptReleaseContext(hProv, 0);
    
    return decrypted;
#else
    // For non-Windows platforms, use XOR decryption
    qDebug() << "AES decryption not available on this platform, using XOR decryption";
    return xorDecrypt(encryptedData, QString::fromUtf8(key));
#endif
}

bool ExecutableEncryptor::encryptExecutable(const QString &inputFilePath, 
                                            const QString &outputFilePath, 
                                            const QString &encryptionKey)
{
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
    
    // Derive encryption key
    QByteArray key = deriveKey(encryptionKey);
    
    // Generate IV
    QByteArray iv = generateIV();
    
    // Encrypt the data
    QByteArray encryptedData = encryptData(fileData, key, iv);
    
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
    return true;
}

bool ExecutableEncryptor::decryptExecutable(const QString &encryptedFilePath, 
                                            const QString &outputFilePath, 
                                            const QString &decryptionKey)
{
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
    
    // Extract IV (first 16 bytes) and encrypted content
    QByteArray iv = encryptedData.left(16);
    QByteArray encryptedContent = encryptedData.mid(16);
    
    // Derive decryption key
    QByteArray key = deriveKey(decryptionKey);
    
    // Decrypt the data
    QByteArray decryptedData = decryptData(encryptedContent, key, iv);
    
    if (decryptedData.isEmpty()) {
        qDebug() << "Decryption failed or resulted in empty data";
        return false;
    }
    
    // Save decrypted file
    QFile outputFile(outputFilePath);
    if (!outputFile.open(QIODevice::WriteOnly)) {
        qDebug() << "Failed to create output file:" << outputFilePath;
        return false;
    }
    
    qint64 written = outputFile.write(decryptedData);
    outputFile.close();
    
    if (written != static_cast<qint64>(decryptedData.size())) {
        qDebug() << "Failed to write all decrypted data to file";
        return false;
    }
    
    qDebug() << "Successfully decrypted executable:" << encryptedFilePath << "->" << outputFilePath;
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
    // Create a temporary file for the decrypted executable
    QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "decrypted_exec_XXXXXX.exe");
    tempFile.setAutoRemove(false); // Don't auto-remove so we can run it
    if (!tempFile.open()) {
        qDebug() << "Failed to create temporary file";
        return -1;
    }
    
    QString tempFilePath = tempFile.fileName();
    tempFile.close();
    
    // Decrypt the executable
    if (!decryptExecutable(encryptedFilePath, tempFilePath, decryptionKey)) {
        qDebug() << "Failed to decrypt executable";
        return -1;
    }
    
    // Run the decrypted executable
    QProcess process;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted()) {
        qDebug() << "Failed to start decrypted executable";
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
