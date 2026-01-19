#ifndef EXECUTABLEENCRYPTOR_H
#define EXECUTABLEENCRYPTOR_H

#include <QString>
#include <QStringList>
#include <QByteArray>

/**
 * @brief ExecutableEncryptor class for encrypting and decrypting executable files
 * 
 * This class provides functionality to:
 * - Encrypt an executable file using a hardware key
 * - Decrypt and run an encrypted executable
 * - Verify the hardware key matches before execution
 * 
 * The encryption uses AES-256 in CBC mode with the hardware fingerprint as the key.
 * The encrypted file can only be decrypted and run on the machine that matches the hardware fingerprint.
 */
class ExecutableEncryptor
{
public:
    /**
     * @brief Encrypt an executable file with a hardware key
     * @param inputFilePath Path to the executable file to encrypt
     * @param outputFilePath Path where the encrypted file will be saved
     * @param encryptionKey The key to use for encryption (typically hardware fingerprint)
     * @return true if encryption succeeded, false otherwise
     */
    static bool encryptExecutable(const QString &inputFilePath, 
                                  const QString &outputFilePath, 
                                  const QString &encryptionKey);

    /**
     * @brief Decrypt an encrypted executable file
     * @param encryptedFilePath Path to the encrypted executable file
     * @param outputFilePath Path where the decrypted file will be saved (can be temporary)
     * @param decryptionKey The key to use for decryption (must match encryption key)
     * @return true if decryption succeeded, false otherwise
     */
    static bool decryptExecutable(const QString &encryptedFilePath, 
                                  const QString &outputFilePath, 
                                  const QString &decryptionKey);

    /**
     * @brief Decrypt and run an encrypted executable in memory
     * @param encryptedFilePath Path to the encrypted executable file
     * @param decryptionKey The key to use for decryption (must match encryption key)
     * @param arguments Command line arguments to pass to the executable
     * @return Process exit code, or -1 if failed to start
     */
    static int decryptAndRun(const QString &encryptedFilePath, 
                            const QString &decryptionKey,
                            const QStringList &arguments = QStringList());

    /**
     * @brief Verify if a key matches the hardware fingerprint
     * @param keyToVerify The key to verify
     * @return true if the key matches the current hardware fingerprint
     */
    static bool verifyHardwareKey(const QString &keyToVerify);

    /**
     * @brief Decrypt and run with automatic hardware key verification
     * This method automatically generates the hardware key and uses it for decryption
     * @param encryptedFilePath Path to the encrypted executable file
     * @param arguments Command line arguments to pass to the executable
     * @return Process exit code, or -1 if failed to start or key verification failed
     */
    static int decryptAndRunWithHardwareKey(const QString &encryptedFilePath,
                                           const QStringList &arguments = QStringList());

private:
    /**
     * @brief Derive encryption key from string (creates 256-bit key)
     * @param password Input password/key string
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
     * @return Encrypted data
     */
    static QByteArray encryptData(const QByteArray &data, 
                                 const QByteArray &key, 
                                 const QByteArray &iv);

    /**
     * @brief Decrypt data using AES-256-CBC
     * @param encryptedData Encrypted data
     * @param key Decryption key (32 bytes, must match encryption key)
     * @param iv Initialization vector (16 bytes, must match encryption IV)
     * @return Decrypted data
     */
    static QByteArray decryptData(const QByteArray &encryptedData, 
                                 const QByteArray &key, 
                                 const QByteArray &iv);

    /**
     * @brief Simple XOR encryption (fallback method, less secure but faster)
     * @param data Data to encrypt
     * @param key Encryption key (QByteArray, can be derived key)
     * @return Encrypted data
     */
    static QByteArray xorEncrypt(const QByteArray &data, const QByteArray &key);

    /**
     * @brief Simple XOR decryption (fallback method)
     * @param encryptedData Encrypted data
     * @param key Decryption key (QByteArray, must match encryption key)
     * @return Decrypted data
     */
    static QByteArray xorDecrypt(const QByteArray &encryptedData, const QByteArray &key);
};

#endif // EXECUTABLEENCRYPTOR_H
