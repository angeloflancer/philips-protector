#ifndef MEMORYEXECUTELOADER_H
#define MEMORYEXECUTELOADER_H

#include <QString>
#include <QStringList>
#include <QByteArray>

/**
 * @brief MemoryExecuteLoader class for running encrypted executables directly from memory
 * 
 * This class provides functionality to:
 * - Encrypt an executable file with a password/key string
 * - Create a self-decrypting loader that decrypts and runs the executable in memory
 * - Execute encrypted executables without ever writing the decrypted version to disk
 * 
 * The encryption uses AES-256 in CBC mode. The encrypted executable can only be run
 * if you have the correct password/key string. The decryption happens entirely in memory.
 */
class MemoryExecuteLoader
{
public:
    /**
     * @brief Encrypt an executable file with a password/key string
     * @param inputFilePath Path to the executable file to encrypt
     * @param outputFilePath Path where the encrypted file will be saved
     * @param password The password/key string to use for encryption
     * @return true if encryption succeeded, false otherwise
     */
    static bool encryptExecutable(const QString &inputFilePath, 
                                  const QString &outputFilePath, 
                                  const QString &password);

    /**
     * @brief Decrypt and execute an encrypted executable directly from memory
     * This method decrypts the executable in memory and executes it without
     * ever writing the decrypted version to disk
     * @param encryptedFilePath Path to the encrypted executable file
     * @param password The password/key string used for encryption
     * @param arguments Command line arguments to pass to the executable
     * @return Process exit code, or -1 if failed to start
     */
    static int decryptAndExecuteFromMemory(const QString &encryptedFilePath,
                                          const QString &password,
                                          const QStringList &arguments = QStringList());

    /**
     * @brief Create a self-decrypting loader executable
     * This creates a new executable that contains the encrypted payload and
     * automatically decrypts and runs it in memory when executed
     * @param encryptedFilePath Path to the encrypted executable file
     * @param loaderOutputPath Path where the loader executable will be saved
     * @param password The password/key string (will be embedded in loader)
     * @return true if loader creation succeeded, false otherwise
     */
    static bool createSelfDecryptingLoader(const QString &encryptedFilePath,
                                          const QString &loaderOutputPath,
                                          const QString &password);

private:
    /**
     * @brief Derive encryption key from password string
     * @param password Input password string
     * @return Derived key as QByteArray (32 bytes for AES-256)
     */
    static QByteArray deriveKey(const QString &password);

    /**
     * @brief Generate random IV (Initialization Vector) for AES-CBC
     * @return 16-byte IV
     */
    static QByteArray generateIV();

    /**
     * @brief Encrypt data using AES-256-CBC
     * @param data Data to encrypt
     * @param key Encryption key (32 bytes)
     * @param iv Initialization vector (16 bytes)
     * @return Encrypted data with IV prepended
     */
    static QByteArray encryptData(const QByteArray &data, 
                                 const QByteArray &key, 
                                 const QByteArray &iv);

    /**
     * @brief Decrypt data using AES-256-CBC
     * @param encryptedData Encrypted data with IV prepended (first 16 bytes are IV)
     * @param key Decryption key (32 bytes, must match encryption key)
     * @return Decrypted data
     */
    static QByteArray decryptData(const QByteArray &encryptedData, 
                                 const QByteArray &key);

    /**
     * @brief Execute a process from memory buffer
     * On Windows, this uses CreateProcess with the executable data in memory
     * @param executableData The executable file data in memory
     * @param arguments Command line arguments
     * @return Process exit code, or -1 if failed
     */
    static int executeFromMemory(const QByteArray &executableData,
                                const QStringList &arguments);
};

#endif // MEMORYEXECUTELOADER_H
