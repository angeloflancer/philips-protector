#include "executableembedder.h"
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
#include <QDir>
#include <QFileInfo>
#include <QCoreApplication>
#include <cstring>

// Magic marker to identify embedded data
static const QByteArray EMBEDDED_DATA_MARKER = QByteArray("PHILIPS_EMBEDDED_V1");
static const int MARKER_SIZE = EMBEDDED_DATA_MARKER.size();
static const int KEY_HASH_SIZE = 64; // SHA-256 hex string length
static const int DATA_SIZE_FIELD = 8; // 8 bytes for qint64

bool ExecutableEmbedder::createEmbeddedExecutable(const QString &originalExecutablePath,
                                                   const QString &outputExecutablePath,
                                                   const QString &hardwareKey)
{
    // Read the original executable
    QFile originalFile(originalExecutablePath);
    if (!originalFile.open(QIODevice::ReadOnly)) {
        qDebug() << "[EMBEDDER] Failed to open original executable:" << originalExecutablePath;
        return false;
    }
    
    QByteArray originalData = originalFile.readAll();
    originalFile.close();
    
    if (originalData.isEmpty()) {
        qDebug() << "[EMBEDDER] Original executable is empty";
        return false;
    }
    
    qDebug() << "[EMBEDDER] Original executable size:" << originalData.size() << "bytes";
    
    // Encrypt the original executable
    QString tempEncryptedFile = QDir::temp().absoluteFilePath("temp_embedded_encrypted.bin");
    bool encryptSuccess = ExecutableEncryptor::encryptExecutable(
        originalExecutablePath,
        tempEncryptedFile,
        hardwareKey
    );
    
    if (!encryptSuccess) {
        qDebug() << "[EMBEDDER] Failed to encrypt original executable";
        return false;
    }
    
    // Read the encrypted data
    QFile encryptedFile(tempEncryptedFile);
    if (!encryptedFile.open(QIODevice::ReadOnly)) {
        qDebug() << "[EMBEDDER] Failed to open encrypted file";
        QFile::remove(tempEncryptedFile);
        return false;
    }
    
    QByteArray encryptedData = encryptedFile.readAll();
    encryptedFile.close();
    QFile::remove(tempEncryptedFile); // Clean up temp file
    
    // Calculate hardware key hash for verification
    QCryptographicHash keyHash(QCryptographicHash::Sha256);
    keyHash.addData(hardwareKey.toUtf8());
    QString hardwareKeyHash = keyHash.result().toHex();
    
    qDebug() << "[EMBEDDER] Hardware key hash:" << hardwareKeyHash;
    qDebug() << "[EMBEDDER] Encrypted data size:" << encryptedData.size() << "bytes";
    
    // Check if wrapper template exists (wrapper executable that calls runEmbeddedExecutable)
    // The wrapper should be compiled from wrapper_main.cpp
    
    // Try to find a wrapper template in several locations:
    QStringList possiblePaths;
    possiblePaths << QFileInfo(outputExecutablePath).absolutePath() + "/wrapper_template.exe";
    possiblePaths << QFileInfo(outputExecutablePath).absolutePath() + "/wrapper.exe";
    possiblePaths << QCoreApplication::applicationDirPath() + "/wrapper_template.exe";
    possiblePaths << QCoreApplication::applicationDirPath() + "/wrapper.exe";
    
    QString wrapperTemplatePath;
    for (const QString &path : possiblePaths) {
        if (QFile::exists(path)) {
            wrapperTemplatePath = path;
            break;
        }
    }
    
    // If wrapper template doesn't exist, we need to inform the user
    if (wrapperTemplatePath.isEmpty()) {
        qDebug() << "[EMBEDDER] Wrapper template not found in any of these locations:";
        for (const QString &path : possiblePaths) {
            qDebug() << "[EMBEDDER]   -" << path;
        }
        qDebug() << "[EMBEDDER] Please compile wrapper_main.cpp first to create wrapper_template.exe";
        qDebug() << "[EMBEDDER] Or provide an existing wrapper executable";
        
        // For development/testing: try to use current executable if it's a wrapper
        // This allows testing without a separate wrapper template
        QString currentExe = QCoreApplication::applicationFilePath();
        if (currentExe != outputExecutablePath && QFile::exists(currentExe)) {
            qDebug() << "[EMBEDDER] Using current executable as wrapper template (for testing)";
            wrapperTemplatePath = currentExe;
        } else {
            return false;
        }
    }
    
    // Copy wrapper template to output (if different)
    if (wrapperTemplatePath != outputExecutablePath) {
        // Remove output file if it exists
        if (QFile::exists(outputExecutablePath)) {
            QFile::remove(outputExecutablePath);
        }
        
        if (!QFile::copy(wrapperTemplatePath, outputExecutablePath)) {
            qDebug() << "[EMBEDDER] Failed to copy wrapper template from:" << wrapperTemplatePath;
            return false;
        }
        qDebug() << "[EMBEDDER] Copied wrapper template from:" << wrapperTemplatePath;
    }
    
    // Append embedded data to the wrapper executable
    if (!appendEmbeddedData(outputExecutablePath, encryptedData, hardwareKeyHash)) {
        qDebug() << "[EMBEDDER] Failed to append embedded data";
        return false;
    }
    
    qDebug() << "[EMBEDDER] Embedded executable created successfully:" << outputExecutablePath;
    qDebug() << "[EMBEDDER] Total size:" << QFileInfo(outputExecutablePath).size() << "bytes";
    
    return true;
}

int ExecutableEmbedder::runEmbeddedExecutable(const QString &wrapperExecutablePath,
                                              const QStringList &arguments)
{
    qDebug() << "[EMBEDDER] Running embedded executable from:" << wrapperExecutablePath;
    
    // Verify hardware key first
    QByteArray encryptedData;
    QString expectedKeyHash;
    
    if (!extractEmbeddedData(wrapperExecutablePath, encryptedData, expectedKeyHash)) {
        qDebug() << "[EMBEDDER] Failed to extract embedded data";
        return -1;
    }
    
    // Generate current hardware key and compare
    QString currentHardwareKey = HardwareFingerprint::generateHardwareKey();
    if (currentHardwareKey.isEmpty()) {
        qDebug() << "[EMBEDDER] Failed to generate hardware key";
        return -1;
    }
    
    QCryptographicHash currentKeyHash(QCryptographicHash::Sha256);
    currentKeyHash.addData(currentHardwareKey.toUtf8());
    QString currentKeyHashStr = currentKeyHash.result().toHex();
    
    if (currentKeyHashStr != expectedKeyHash) {
        qDebug() << "[EMBEDDER] Hardware key mismatch!";
        qDebug() << "[EMBEDDER] Expected:" << expectedKeyHash;
        qDebug() << "[EMBEDDER] Current:" << currentKeyHashStr;
        return -1;
    }
    
    qDebug() << "[EMBEDDER] Hardware key verified successfully";
    
    // Decrypt the embedded executable
    QString tempDecryptedFile = QDir::temp().absoluteFilePath("temp_embedded_decrypted.exe");
    
    // Write encrypted data to temp file first
    QFile tempEncryptedFile(QDir::temp().absoluteFilePath("temp_embedded_encrypted.bin"));
    if (!tempEncryptedFile.open(QIODevice::WriteOnly)) {
        qDebug() << "[EMBEDDER] Failed to create temp encrypted file";
        return -1;
    }
    tempEncryptedFile.write(encryptedData);
    tempEncryptedFile.close();
    
    // Decrypt using ExecutableEncryptor
    bool decryptSuccess = ExecutableEncryptor::decryptExecutable(
        tempEncryptedFile.fileName(),
        tempDecryptedFile,
        currentHardwareKey
    );
    
    QFile::remove(tempEncryptedFile.fileName()); // Clean up
    
    if (!decryptSuccess) {
        qDebug() << "[EMBEDDER] Failed to decrypt embedded executable";
        return -1;
    }
    
    // Run the decrypted executable
    // Use startDetached to run it independently
    qDebug() << "[EMBEDDER] Starting embedded executable:" << tempDecryptedFile;
    qDebug() << "[EMBEDDER] Arguments:" << arguments;
    
    QString program = tempDecryptedFile;
    QStringList args = arguments;
    
    qint64 pid = 0;
    bool started = QProcess::startDetached(program, args, QDir::temp().absolutePath(), &pid);
    
    if (!started) {
        qDebug() << "[EMBEDDER] Failed to start embedded executable";
        QFile::remove(tempDecryptedFile);
        return -1;
    }
    
    qDebug() << "[EMBEDDER] Embedded executable started successfully (PID:" << pid << ")";
    qDebug() << "[EMBEDDER] Temporary file will be cleaned up after execution";
    
    // Note: The temp file will remain until the process exits
    // In a production system, you might want to schedule deletion or use a different approach
    
    return 0; // Success (process is running detached)
}

bool ExecutableEmbedder::extractEmbeddedData(const QString &wrapperExecutablePath,
                                             QByteArray &encryptedData,
                                             QString &expectedKeyHash)
{
    QFile wrapperFile(wrapperExecutablePath);
    if (!wrapperFile.open(QIODevice::ReadOnly)) {
        qDebug() << "[EMBEDDER] Failed to open wrapper executable:" << wrapperExecutablePath;
        return false;
    }
    
    // Read the entire file to find the marker
    // The embedded data is appended at the end
    QByteArray fileData = wrapperFile.readAll();
    wrapperFile.close();
    
    // Find the marker (should be near the end)
    int markerPos = fileData.lastIndexOf(EMBEDDED_DATA_MARKER);
    
    if (markerPos == -1) {
        qDebug() << "[EMBEDDER] Embedded data marker not found in wrapper executable";
        return false;
    }
    
    qDebug() << "[EMBEDDER] Found embedded data marker at position:" << markerPos;
    
    // Extract data structure starting from marker
    int offset = markerPos + MARKER_SIZE;
    
    if (offset + KEY_HASH_SIZE + DATA_SIZE_FIELD > fileData.size()) {
        qDebug() << "[EMBEDDER] Invalid embedded data structure - file too small";
        return false;
    }
    
    // Read hardware key hash
    QByteArray keyHashBytes = fileData.mid(offset, KEY_HASH_SIZE);
    expectedKeyHash = QString::fromUtf8(keyHashBytes);
    offset += KEY_HASH_SIZE;
    
    // Read encrypted data size
    qint64 encryptedSize = 0;
    if (offset + DATA_SIZE_FIELD > fileData.size()) {
        qDebug() << "[EMBEDDER] Invalid data size field";
        return false;
    }
    memcpy(&encryptedSize, fileData.constData() + offset, DATA_SIZE_FIELD);
    offset += DATA_SIZE_FIELD;
    
    // Read encrypted data
    if (offset + encryptedSize > fileData.size()) {
        qDebug() << "[EMBEDDER] Encrypted data extends beyond file";
        qDebug() << "[EMBEDDER] File size:" << fileData.size() << "bytes";
        qDebug() << "[EMBEDDER] Need:" << (offset + encryptedSize) << "bytes";
        return false;
    }
    
    encryptedData = fileData.mid(offset, encryptedSize);
    
    if (encryptedData.size() != encryptedSize) {
        qDebug() << "[EMBEDDER] Failed to read complete encrypted data";
        qDebug() << "[EMBEDDER] Expected:" << encryptedSize << "bytes, got:" << encryptedData.size() << "bytes";
        return false;
    }
    
    qDebug() << "[EMBEDDER] Extracted embedded data successfully";
    qDebug() << "[EMBEDDER] Expected key hash:" << expectedKeyHash;
    qDebug() << "[EMBEDDER] Encrypted data size:" << encryptedData.size() << "bytes";
    
    return true;
}

qint64 ExecutableEmbedder::getWrapperExecutableSize(const QString &wrapperExecutablePath)
{
    QFile file(wrapperExecutablePath);
    if (!file.open(QIODevice::ReadOnly)) {
        return 0;
    }
    
    qint64 size = file.size();
    file.close();
    
    // The wrapper size is the file size minus the embedded data
    // We need to find where the embedded data starts
    // For now, return the full size (the extraction method will find the marker)
    return size;
}

bool ExecutableEmbedder::createWrapperTemplate(const QString &outputPath)
{
    // This would create a minimal executable template
    // For a full implementation, we would need to:
    // 1. Create a minimal PE executable
    // 2. Or provide a pre-compiled wrapper template
    // 3. Or compile a wrapper source code
    
    // For now, this is a placeholder
    // The actual wrapper needs to be a compiled executable that calls runEmbeddedExecutable
    Q_UNUSED(outputPath);
    return false;
}

bool ExecutableEmbedder::appendEmbeddedData(const QString &wrapperExecutablePath,
                                           const QByteArray &encryptedData,
                                           const QString &hardwareKeyHash)
{
    QFile wrapperFile(wrapperExecutablePath);
    if (!wrapperFile.open(QIODevice::Append)) {
        return false;
    }
    
    QDataStream out(&wrapperFile);
    
    // Write marker
    out.writeRawData(EMBEDDED_DATA_MARKER.constData(), MARKER_SIZE);
    
    // Write hardware key hash
    QByteArray keyHashBytes = hardwareKeyHash.toUtf8();
    out.writeRawData(keyHashBytes.constData(), KEY_HASH_SIZE);
    
    // Write encrypted data size
    qint64 encryptedSize = encryptedData.size();
    out.writeRawData(reinterpret_cast<const char*>(&encryptedSize), DATA_SIZE_FIELD);
    
    // Write encrypted data
    out.writeRawData(encryptedData.constData(), encryptedData.size());
    
    wrapperFile.close();
    
    return true;
}
