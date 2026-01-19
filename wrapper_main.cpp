// This is a template for the wrapper executable
// This file should be compiled separately to create the wrapper executable
// Then use ExecutableEmbedder::createEmbeddedExecutable() to embed the payload

#include "executableembedder.h"
#include <QCoreApplication>
#include <QDebug>
#include <QMessageBox>
#include <QApplication>

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    
    // Get the path to this executable
    QString wrapperPath = QCoreApplication::applicationFilePath();
    
    // Get command line arguments (excluding program name)
    QStringList arguments;
    for (int i = 1; i < argc; ++i) {
        arguments << QString::fromLocal8Bit(argv[i]);
    }
    
    // Run the embedded executable
    int exitCode = ExecutableEmbedder::runEmbeddedExecutable(wrapperPath, arguments);
    
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
