#include "mod.h"
#include "ui_mod.h"
#include "hardwarefingerprint.h"
#include "pepatcher.h"
#include <QApplication>
#include <QPalette>
#include <QFileDialog>
#include <QMessageBox>
#include <QFileInfo>
#include <QDir>
#include <QFile>

MoD::MoD(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MoD)
{
    ui->setupUi(this);
    this->setPalette(QPalette(Qt::white));
    
    // Auto-generate hardware key on startup
    onGenerateHardwareKeyButtonClicked();
}

MoD::~MoD()
{
    delete ui;
}

void MoD::onGenerateHardwareKeyButtonClicked()
{
    // Generate the hardware key using HardwareFingerprint class
    QString key = HardwareFingerprint::generateHardwareKey();
    
    if (key.isEmpty()) {
        ui->keyLineEdit->setText("Failed to generate hardware key. Check debug output for details.");
        ui->statusBar->showMessage("Error: Could not generate hardware key", 5000);
    } else {
        ui->keyLineEdit->setText(key);
        ui->statusBar->showMessage("Hardware key generated successfully", 3000);
    }
}

void MoD::onBrowseButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        "Select Executable File",
        QString(),
        "Executable Files (*.exe);;All Files (*.*)");

    if (!fileName.isEmpty()) {
        ui->filePathLineEdit->setText(fileName);
        ui->statusBar->showMessage("File selected: " + QFileInfo(fileName).fileName(), 3000);
    }
}

void MoD::onPatchExecutableButtonClicked()
{
    QString inputPath = ui->filePathLineEdit->text();
    if (inputPath.isEmpty()) {
        QMessageBox::warning(this, "No file selected", "Please select an executable file first.");
        return;
    }
    if (!QFile::exists(inputPath)) {
        QMessageBox::warning(this, "File not found", "The selected file does not exist.");
        return;
    }

    QFileInfo info(inputPath);
    QString suggested = info.path() + QDir::separator() + info.completeBaseName() + "_hello.exe";

    QString outputPath = QFileDialog::getSaveFileName(
        this,
        "Save Patched Executable As",
        suggested,
        "Executable Files (*.exe);;All Files (*.*)");

    if (outputPath.isEmpty()) {
        return;
    }

    ui->statusBar->showMessage("Patching executable...", 0);
    QApplication::processEvents(); // Update UI
    
    bool ok = PEPatcher::injectMessageBox(inputPath, outputPath);
    if (ok) {
        ui->statusBar->showMessage("Patched successfully: " + QFileInfo(outputPath).fileName(), 5000);
        ui->keyLineEdit->setText("Patched: " + outputPath);
        QMessageBox::information(this, "Success",
                                 "Executable patched successfully.\n\n"
                                 "File: " + outputPath +
                                 "\nA 'Hello' MessageBox will show on startup.");
    } else {
        ui->statusBar->showMessage("Patch failed - check debug output", 5000);
        QMessageBox::critical(this, "Error",
                              "Failed to patch the executable.\n\n"
                              "Possible reasons:\n"
                              "- File is not a 32-bit Windows PE executable\n"
                              "- File is 64-bit (only 32-bit supported)\n"
                              "- Missing kernel32.dll imports (LoadLibraryA/GetProcAddress)\n"
                              "- File is corrupted or has an unsupported structure\n\n"
                              "Check the debug output for more details.");
    }
}
