#ifndef EXECUTABLEEMBEDDER_H
#define EXECUTABLEEMBEDDER_H

#include <QString>
#include <QStringList>

/**
 * @brief ExecutableEmbedder class for creating self-contained executables with embedded payloads
 * 
 * This class provides functionality to:
 * - Embed an executable file (encrypted) into a wrapper executable
 * - The wrapper executable checks hardware key before running the embedded executable
 * - If hardware key matches, decrypts and runs the embedded executable
 * - If hardware key doesn't match, cancels execution
 * 
 * The embedded executable is encrypted using AES-256 with the hardware key.
 * The wrapper executable is a standalone application that contains everything needed.
 */
class ExecutableEmbedder
{
public:
    /**
     * @brief Create a wrapper executable with embedded payload
     * @param originalExecutablePath Path to the original executable file to embed
     * @param outputExecutablePath Path where the wrapper executable will be created
     * @param hardwareKey The hardware key to use for encryption and verification
     * @return true if creation succeeded, false otherwise
     */
    static bool createEmbeddedExecutable(const QString &originalExecutablePath,
                                         const QString &outputExecutablePath,
                                         const QString &hardwareKey);

    /**
     * @brief Run the embedded executable from the wrapper (called by wrapper itself)
     * This method should be called from within the wrapper executable
     * @param wrapperExecutablePath Path to the wrapper executable (usually argv[0])
     * @param arguments Command line arguments to pass to the embedded executable
     * @return Process exit code, or -1 if failed to start or hardware key mismatch
     */
    static int runEmbeddedExecutable(const QString &wrapperExecutablePath,
                                     const QStringList &arguments = QStringList());

    /**
     * @brief Extract embedded data from wrapper executable
     * @param wrapperExecutablePath Path to the wrapper executable
     * @param encryptedData Output parameter for the extracted encrypted data
     * @param expectedKeyHash Output parameter for the expected hardware key hash
     * @return true if extraction succeeded, false otherwise
     */
    static bool extractEmbeddedData(const QString &wrapperExecutablePath,
                                    QByteArray &encryptedData,
                                    QString &expectedKeyHash);

private:

    /**
     * @brief Get the size of the wrapper executable (without embedded data)
     * This is stored at the end of the file as a 8-byte value
     * @param wrapperExecutablePath Path to the wrapper executable
     * @return Size of wrapper executable, or 0 on error
     */
    static qint64 getWrapperExecutableSize(const QString &wrapperExecutablePath);

    /**
     * @brief Create a minimal wrapper executable template
     * This creates a simple executable that calls runEmbeddedExecutable
     * @param outputPath Path where the wrapper template will be written
     * @return true if creation succeeded, false otherwise
     */
    static bool createWrapperTemplate(const QString &outputPath);

    /**
     * @brief Append embedded data to wrapper executable
     * @param wrapperExecutablePath Path to the wrapper executable
     * @param encryptedData The encrypted executable data to append
     * @param hardwareKeyHash The hardware key hash to embed for verification
     * @return true if appending succeeded, false otherwise
     */
    static bool appendEmbeddedData(const QString &wrapperExecutablePath,
                                   const QByteArray &encryptedData,
                                   const QString &hardwareKeyHash);
};

#endif // EXECUTABLEEMBEDDER_H
