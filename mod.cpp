#include "mod.h"
#include "ui_mod.h"
#include "hardwarefingerprint.h"
#include "executableembedder.h"
#include <QPalette>
#include <QFileDialog>
#include <QMessageBox>
#include <QFileInfo>
#include <QFile>

MoD::MoD(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MoD)
{
    ui->setupUi(this);
    this->setPalette(QPalette(Qt::white));
    
    // Connect signals for button state updates
    connect(ui->embedOriginalFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateCreateEmbeddedButtonState);
    connect(ui->embedOutputFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateCreateEmbeddedButtonState);
    
    // Auto-generate key on startup
    onGenerateButtonClicked();
}

MoD::~MoD()
{
    delete ui;
}

void MoD::onGenerateButtonClicked()
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

void MoD::onBrowseEmbedOriginalButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Original Executable File"),
        QString(),
        tr("Executable Files (*.exe);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->embedOriginalFileLineEdit->setText(fileName);
        updateCreateEmbeddedButtonState();
    }
}

void MoD::onBrowseEmbedOutputButtonClicked()
{
    QString fileName = QFileDialog::getSaveFileName(
        this,
        tr("Save Embedded Executable"),
        QString(),
        tr("Executable Files (*.exe);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->embedOutputFileLineEdit->setText(fileName);
        updateCreateEmbeddedButtonState();
    }
}

void MoD::onCreateEmbeddedButtonClicked()
{
    QString originalFile = ui->embedOriginalFileLineEdit->text();
    QString outputFile = ui->embedOutputFileLineEdit->text();
    
    if (originalFile.isEmpty() || !QFile::exists(originalFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid original executable file."));
        return;
    }
    
    if (outputFile.isEmpty()) {
        QMessageBox::warning(this, tr("Error"), tr("Please specify an output file path."));
        return;
    }
    
    // Get hardware key
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot create embedded executable."));
        return;
    }
    
    // Check if output file already exists
    if (QFile::exists(outputFile)) {
        int ret = QMessageBox::question(this, tr("File Exists"),
            tr("The output file already exists. Do you want to overwrite it?"),
            QMessageBox::Yes | QMessageBox::No,
            QMessageBox::No);
        
        if (ret == QMessageBox::No) {
            return;
        }
        
        // Remove existing file
        if (!QFile::remove(outputFile)) {
            QMessageBox::critical(this, tr("Error"), tr("Failed to remove existing file. Please check file permissions."));
            return;
        }
    }
    
    // Create embedded executable
    ui->statusBar->showMessage(tr("Creating embedded executable..."), 0);
    ui->createEmbeddedButton->setEnabled(false);
    
    bool success = ExecutableEmbedder::createEmbeddedExecutable(
        originalFile,
        outputFile,
        hardwareKey
    );
    
    ui->createEmbeddedButton->setEnabled(true);
    
    if (success) {
        ui->statusBar->showMessage(tr("Embedded executable created successfully: %1").arg(outputFile), 5000);
        QMessageBox::information(this, tr("Success"),
            tr("Embedded executable created successfully!\n\n"
               "Output file: %1\n\n"
               "This executable will:\n"
               "- Check hardware key before running\n"
               "- Only run on the machine with matching hardware key\n"
               "- Decrypt and execute the embedded executable if authorized").arg(outputFile));
    } else {
        ui->statusBar->showMessage(tr("Failed to create embedded executable. Check debug output for details."), 5000);
        QMessageBox::critical(this, tr("Error"),
            tr("Failed to create embedded executable.\n\n"
               "Possible reasons:\n"
               "- Wrapper template not found (compile wrapper_main.cpp first)\n"
               "- File access permissions\n"
               "- Insufficient disk space\n"
               "Check debug output for more details."));
    }
}

void MoD::updateCreateEmbeddedButtonState()
{
    QString originalFile = ui->embedOriginalFileLineEdit->text();
    QString outputFile = ui->embedOutputFileLineEdit->text();
    
    bool isValid = !originalFile.isEmpty() && 
                   QFile::exists(originalFile) && 
                   !outputFile.isEmpty();
    
    ui->createEmbeddedButton->setEnabled(isValid);
}
