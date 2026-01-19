#ifndef RUNTIMEPROTECTOR_H
#define RUNTIMEPROTECTOR_H

#include <QString>
#include <QStringList>
#include <QByteArray>

/**
 * @brief RuntimeProtector class for hardware-bound encryption with runtime verification
 * 
 * This class provides advanced protection by:
 * - Embedding hardware fingerprint in encrypted file header
 * - Injecting hardware verification code into the executable
 * - Performing runtime hardware checks during execution
 * - Including anti-debugging and anti-tampering measures
 * 
 * The protected executable will only run on the machine it was encrypted for,
 * even if someone copies the decrypted file.
 */
class RuntimeProtector
{
public:
    /**
     * @brief Encrypt an executable with hardware binding and runtime protection
     * @param inputFilePath Path to the executable file to encrypt
     * @param outputFilePath Path where the protected encrypted file will be saved
     * @param hardwareKey The hardware fingerprint key to bind to
     * @return true if encryption succeeded, false otherwise
     */
    static bool encryptWithRuntimeProtection(const QString &inputFilePath,
                                            const QString &outputFilePath,
                                            const QString &hardwareKey);

    /**
     * @brief Decrypt and execute a protected executable with runtime verification
     * This method verifies hardware, performs anti-debugging checks, and executes from memory
     * @param protectedFilePath Path to the protected encrypted executable file
     * @param arguments Command line arguments to pass to the executable
     * @return Process exit code, or -1 if failed to start or verification failed
     */
    static int decryptAndExecuteProtected(const QString &protectedFilePath,
                                         const QStringList &arguments = QStringList());

    /**
     * @brief Verify hardware fingerprint matches embedded fingerprint
     * @param embeddedFingerprint The hardware fingerprint embedded in the file
     * @return true if hardware matches, false otherwise
     */
    static bool verifyHardwareFingerprint(const QString &embeddedFingerprint);

    /**
     * @brief Perform anti-debugging checks
     * @return true if no debugger detected, false if debugger found
     */
    static bool performAntiDebuggingChecks();

private:
    /**
     * @brief Embed hardware fingerprint in encrypted file header
     * @param encryptedData The encrypted executable data
     * @param hardwareKey The hardware fingerprint to embed
     * @return Data with embedded fingerprint header
     */
    static QByteArray embedFingerprintHeader(const QByteArray &encryptedData,
                                           const QString &hardwareKey);

    /**
     * @brief Extract embedded hardware fingerprint from file header
     * @param protectedData The protected file data
     * @param encryptedData Output parameter for the encrypted data (without header)
     * @return The embedded hardware fingerprint, or empty string if invalid
     */
    static QString extractFingerprintHeader(const QByteArray &protectedData,
                                           QByteArray &encryptedData);

    /**
     * @brief Inject hardware verification stub into PE executable
     * This modifies the executable to check hardware at startup
     * @param executableData The PE executable data
     * @param hardwareKey The hardware fingerprint to verify against
     * @return Modified executable data with verification code injected
     */
    static QByteArray injectHardwareVerificationStub(const QByteArray &executableData,
                                                     const QString &hardwareKey);

    /**
     * @brief Derive encryption key from hardware fingerprint
     * @param hardwareKey Input hardware fingerprint string
     * @return Derived key as QByteArray (32 bytes for AES-256)
     */
    static QByteArray deriveKey(const QString &hardwareKey);

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
     * @param executableData The executable file data in memory
     * @param arguments Command line arguments
     * @return Process exit code, or -1 if failed
     */
    static int executeFromMemory(const QByteArray &executableData,
                                const QStringList &arguments);

    /**
     * @brief Check if debugger is present (Windows)
     * @return true if debugger detected, false otherwise
     */
    static bool isDebuggerPresent();

    /**
     * @brief Check if running in virtual machine
     * @return true if VM detected, false otherwise
     */
    static bool isVirtualMachine();
};

#endif // RUNTIMEPROTECTOR_H
