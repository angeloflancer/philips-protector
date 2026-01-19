#include "memoryexecuteloader.h"
#include <QFile>
#include <QByteArray>
#include <QCryptographicHash>
#include <QProcess>
#include <QDebug>
#include <QDir>
#include <QTime>
#include <QThread>
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
    // Create a temporary file for execution (Windows doesn't support true in-memory execution easily)
    // But we'll use a very short-lived temporary file that's deleted immediately after process starts
    QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "mem_exec_XXXXXX.exe");
    tempFile.setAutoRemove(true); // Auto-remove when closed
    
    if (!tempFile.open()) {
        qDebug() << "Failed to create temporary file for execution";
        return -1;
    }
    
    QString tempFilePath = tempFile.fileName();
    
    // Write executable data
    qint64 written = tempFile.write(executableData);
    tempFile.flush();
    tempFile.close(); // Close immediately
    
    if (written != static_cast<qint64>(executableData.size())) {
        qDebug() << "Failed to write executable data to temp file";
        return -1;
    }
    
    // Small delay to ensure file is written
    QThread::msleep(50);
    
    // Execute using QProcess
    QProcess process;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted(5000)) {
        qDebug() << "Failed to start process";
        qDebug() << "Error:" << process.errorString();
        return -1;
    }
    
    // Wait for process to finish
    process.waitForFinished(-1);
    
    int exitCode = process.exitCode();
    
    // File will be auto-removed when tempFile goes out of scope
    return exitCode;
}
#else
int MemoryExecuteLoader::executeFromMemory(const QByteArray &executableData,
                                          const QStringList &arguments)
{
    // For non-Windows, use similar approach
    QTemporaryFile tempFile(QDir::tempPath() + QDir::separator() + "mem_exec_XXXXXX");
    tempFile.setAutoRemove(true);
    
    if (!tempFile.open()) {
        qDebug() << "Failed to create temporary file for execution";
        return -1;
    }
    
    QString tempFilePath = tempFile.fileName();
    
    qint64 written = tempFile.write(executableData);
    tempFile.flush();
    tempFile.close();
    
    if (written != static_cast<qint64>(executableData.size())) {
        qDebug() << "Failed to write executable data";
        return -1;
    }
    
    // Make executable
    QFile::setPermissions(tempFilePath, QFile::ReadUser | QFile::WriteUser | QFile::ExeUser);
    
    QThread::msleep(50);
    
    QProcess process;
    process.start(tempFilePath, arguments);
    
    if (!process.waitForStarted(5000)) {
        qDebug() << "Failed to start process";
        return -1;
    }
    
    process.waitForFinished(-1);
    
    return process.exitCode();
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
