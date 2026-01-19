#include "mod.h"
#include "executableembedder.h"
#include <QApplication>
#include <QDebug>
#include <QFile>
#include <QMessageBox>

int main(int argc, char *argv[])
{
    QApplication a(argc, argv);
    
    // Check if this executable has embedded data
    // If it does, run the embedded executable instead of showing the UI
    QString executablePath = QApplication::applicationFilePath();
    QByteArray testData;
    QString testHash;
    
    // Try to extract embedded data (this will fail if no embedded data exists)
    if (ExecutableEmbedder::extractEmbeddedData(executablePath, testData, testHash)) {
        // This executable has embedded data - it's a wrapper
        // Get command line arguments (excluding program name)
        QStringList arguments;
        for (int i = 1; i < argc; ++i) {
            arguments << QString::fromLocal8Bit(argv[i]);
        }
        
        // Run the embedded executable
        qDebug() << "[MAIN] Detected embedded executable, running embedded payload...";
        int exitCode = ExecutableEmbedder::runEmbeddedExecutable(executablePath, arguments);
        
        if (exitCode == -1) {
            // Hardware key mismatch or other error
            QMessageBox::critical(nullptr, 
                "Hardware Verification Failed",
                "This application can only run on the authorized machine.\n\n"
                "Hardware key verification failed. The application will now exit.");
            return 1;
        }
        
        return exitCode;
    }
    
    // No embedded data - this is the normal UI application
    MoD w;
    w.show();

    return a.exec();
}
