#include "executableencryptor.h"
#include "hardwarefingerprint.h"
#include <QDebug>
#include <QByteArray>
#include <QFile>
#include <QDir>
#include <iostream>

/**
 * @brief Comprehensive test function to debug encryption/decryption issues
 * This function tests each step individually and reports results
 */
void testEncryptionDecryption()
{
    qDebug() << "========================================";
    qDebug() << "ENCRYPTION/DECRYPTION DIAGNOSTIC TEST";
    qDebug() << "========================================";
    
    // Test 1: Hardware Key Generation
    qDebug() << "\n[TEST 1] Hardware Key Generation";
    qDebug() << "----------------------------------------";
    QString key1 = HardwareFingerprint::generateHardwareKey();
    qDebug() << "Generated Key #1:" << key1;
    qDebug() << "Key #1 Length:" << key1.length();
    qDebug() << "Key #1 Empty:" << (key1.isEmpty() ? "YES (ERROR!)" : "NO");
    
    // Generate again to check consistency
    QString key2 = HardwareFingerprint::generateHardwareKey();
    qDebug() << "Generated Key #2:" << key2;
    qDebug() << "Key #2 Length:" << key2.length();
    qDebug() << "Keys Match:" << (key1 == key2 ? "YES" : "NO (ERROR!)");
    
    if (key1.isEmpty() || key1 != key2) {
        qDebug() << "ERROR: Hardware key generation failed or inconsistent!";
        return;
    }
    
    // Test 2: Key Derivation
    qDebug() << "\n[TEST 2] Key Derivation";
    qDebug() << "----------------------------------------";
    QByteArray derivedKey1 = ExecutableEncryptor::deriveKey(key1);
    QByteArray derivedKey2 = ExecutableEncryptor::deriveKey(key2);
    qDebug() << "Derived Key #1 Size:" << derivedKey1.size() << "bytes";
    qDebug() << "Derived Key #1 Hex:" << derivedKey1.toHex().left(32) << "...";
    qDebug() << "Derived Key #2 Size:" << derivedKey2.size() << "bytes";
    qDebug() << "Derived Keys Match:" << (derivedKey1 == derivedKey2 ? "YES" : "NO (ERROR!)");
    
    if (derivedKey1 != derivedKey2 || derivedKey1.size() != 32) {
        qDebug() << "ERROR: Key derivation failed or inconsistent!";
        return;
    }
    
    // Test 3: Simple Data Encryption/Decryption Test
    qDebug() << "\n[TEST 3] Simple Data Encryption/Decryption";
    qDebug() << "----------------------------------------";
    QByteArray testData = "MZTest executable data here";
    qDebug() << "Original Data:" << testData.toHex().left(32) << "...";
    qDebug() << "Original Data Size:" << testData.size() << "bytes";
    qDebug() << "Original starts with 'MZ':" << (testData.size() >= 2 && testData[0] == 'M' && testData[1] == 'Z' ? "YES" : "NO");
    
    // Generate IV
    QByteArray iv = ExecutableEncryptor::generateIV();
    qDebug() << "Generated IV Size:" << iv.size() << "bytes";
    qDebug() << "IV Hex:" << iv.toHex();
    
    // Encrypt
    QByteArray encrypted = ExecutableEncryptor::encryptData(testData, derivedKey1, iv);
    qDebug() << "Encrypted Data Size:" << encrypted.size() << "bytes";
    qDebug() << "Encrypted Data Hex:" << encrypted.toHex().left(32) << "...";
    
    // Check if IV is prepended
    if (encrypted.size() >= 16) {
        QByteArray encryptedIV = encrypted.left(16);
        QByteArray encryptedContent = encrypted.mid(16);
        qDebug() << "Encrypted IV matches original:" << (encryptedIV == iv ? "YES" : "NO");
        qDebug() << "Encrypted Content Size:" << encryptedContent.size() << "bytes";
        
        // Decrypt
        QByteArray decrypted = ExecutableEncryptor::decryptData(encryptedContent, derivedKey1, iv);
        qDebug() << "Decrypted Data Size:" << decrypted.size() << "bytes";
        qDebug() << "Decrypted Data Hex:" << decrypted.toHex().left(32) << "...";
        qDebug() << "Decrypted matches original:" << (decrypted == testData ? "YES" : "NO (ERROR!)");
        
        if (decrypted != testData) {
            qDebug() << "ERROR: Encryption/Decryption roundtrip failed!";
            qDebug() << "Original:" << testData;
            qDebug() << "Decrypted:" << decrypted;
            return;
        }
    } else {
        qDebug() << "ERROR: Encrypted data too small!";
        return;
    }
    
    // Test 4: File-based Encryption/Decryption Test
    qDebug() << "\n[TEST 4] File-based Encryption/Decryption";
    qDebug() << "----------------------------------------";
    
    // Create a test executable file (just with MZ header)
    QString testExePath = QDir::tempPath() + QDir::separator() + "test_exe.exe";
    QFile testExeFile(testExePath);
    if (testExeFile.open(QIODevice::WriteOnly)) {
        // Write MZ header + some test data
        QByteArray testExeData;
        testExeData.append("MZ");  // PE signature
        testExeData.append(QByteArray(58, 0));  // Fill to 60 bytes (min PE header)
        testExeData.append("This is test executable data");
        testExeFile.write(testExeData);
        testExeFile.close();
        qDebug() << "Created test executable:" << testExePath;
        qDebug() << "Test executable size:" << testExeData.size() << "bytes";
    } else {
        qDebug() << "ERROR: Failed to create test executable file!";
        return;
    }
    
    // Encrypt the file
    QString encryptedPath = QDir::tempPath() + QDir::separator() + "test_encrypted.encrypted";
    qDebug() << "Encrypting to:" << encryptedPath;
    bool encryptSuccess = ExecutableEncryptor::encryptExecutable(testExePath, encryptedPath, key1);
    qDebug() << "Encryption Result:" << (encryptSuccess ? "SUCCESS" : "FAILED");
    
    if (!encryptSuccess) {
        qDebug() << "ERROR: File encryption failed!";
        QFile::remove(testExePath);
        return;
    }
    
    // Check encrypted file
    QFile encryptedFile(encryptedPath);
    if (encryptedFile.open(QIODevice::ReadOnly)) {
        QByteArray encryptedFileData = encryptedFile.readAll();
        encryptedFile.close();
        qDebug() << "Encrypted file size:" << encryptedFileData.size() << "bytes";
        
        if (encryptedFileData.size() >= 16) {
            QByteArray fileIV = encryptedFileData.left(16);
            QByteArray fileEncrypted = encryptedFileData.mid(16);
            qDebug() << "File IV Size:" << fileIV.size() << "bytes";
            qDebug() << "File Encrypted Content Size:" << fileEncrypted.size() << "bytes";
        }
    }
    
    // Decrypt the file
    QString decryptedPath = QDir::tempPath() + QDir::separator() + "test_decrypted.exe";
    qDebug() << "Decrypting to:" << decryptedPath;
    
    // Generate key again to test consistency
    QString key3 = HardwareFingerprint::generateHardwareKey();
    qDebug() << "Key for decryption:" << key3;
    qDebug() << "Decryption key matches encryption key:" << (key3 == key1 ? "YES" : "NO (ERROR!)");
    
    bool decryptSuccess = ExecutableEncryptor::decryptExecutable(encryptedPath, decryptedPath, key3);
    qDebug() << "Decryption Result:" << (decryptSuccess ? "SUCCESS" : "FAILED");
    
    if (!decryptSuccess) {
        qDebug() << "ERROR: File decryption failed!";
        QFile::remove(testExePath);
        QFile::remove(encryptedPath);
        return;
    }
    
    // Verify decrypted file
    QFile decryptedFile(decryptedPath);
    if (decryptedFile.open(QIODevice::ReadOnly)) {
        QByteArray decryptedFileData = decryptedFile.readAll();
        decryptedFile.close();
        qDebug() << "Decrypted file size:" << decryptedFileData.size() << "bytes";
        qDebug() << "Decrypted starts with 'MZ':" << (decryptedFileData.size() >= 2 && decryptedFileData[0] == 'M' && decryptedFileData[1] == 'Z' ? "YES" : "NO (ERROR!)");
        
        // Compare with original
        QFile originalFile(testExePath);
        if (originalFile.open(QIODevice::ReadOnly)) {
            QByteArray originalFileData = originalFile.readAll();
            originalFile.close();
            qDebug() << "Original file size:" << originalFileData.size() << "bytes";
            qDebug() << "Files match:" << (decryptedFileData == originalFileData ? "YES" : "NO (ERROR!)");
            
            if (decryptedFileData != originalFileData) {
                qDebug() << "ERROR: Decrypted file does not match original!";
                qDebug() << "Original first 32 bytes:" << originalFileData.left(32).toHex();
                qDebug() << "Decrypted first 32 bytes:" << decryptedFileData.left(32).toHex();
            }
        }
    }
    
    // Cleanup
    QFile::remove(testExePath);
    QFile::remove(encryptedPath);
    QFile::remove(decryptedPath);
    
    qDebug() << "\n========================================";
    qDebug() << "TEST COMPLETE";
    qDebug() << "========================================";
}
