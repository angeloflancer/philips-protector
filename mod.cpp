#include "mod.h"
#include "ui_mod.h"
#include "hardwarefingerprint.h"
#include "executableencryptor.h"
#include <QPalette>
#include <QDebug>
#include <QFileDialog>
#include <QMessageBox>
#include <QFileInfo>
#include <QFile>
#include <QDir>

MoD::MoD(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MoD)
{
    ui->setupUi(this);
    this->setPalette(QPalette(Qt::white));
    
    // Connect signals for button state updates
    connect(ui->encryptFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateEncryptButtonState);
    connect(ui->runFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateRunButtonState);
    
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

void MoD::onBrowseEncryptButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Executable File"),
        QString(),
        tr("Executable Files (*.exe);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->encryptFileLineEdit->setText(fileName);
        updateEncryptButtonState();
    }
}

void MoD::onEncryptButtonClicked()
{
    QString inputFile = ui->encryptFileLineEdit->text();
    
    if (inputFile.isEmpty() || !QFile::exists(inputFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid executable file."));
        return;
    }
    
    // Get hardware key
    qDebug() << "[ENCRYPT] Generating hardware key...";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[ENCRYPT] Hardware key:" << hardwareKey;
    qDebug() << "[ENCRYPT] Hardware key length:" << hardwareKey.length();
    
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot encrypt file."));
        return;
    }
    
    // Ask user for output file location
    QFileInfo fileInfo(inputFile);
    QString defaultOutput = fileInfo.path() + QDir::separator() + fileInfo.baseName() + "_encrypted";
    
    QString outputFile = QFileDialog::getSaveFileName(
        this,
        tr("Save Encrypted File"),
        defaultOutput,
        tr("Encrypted Files (*.encrypted);;All Files (*.*)")
    );
    
    if (outputFile.isEmpty()) {
        return; // User cancelled
    }
    
    // Encrypt the file
    ui->statusBar->showMessage(tr("Encrypting file..."), 0);
    ui->encryptButton->setEnabled(false);
    
    bool success = ExecutableEncryptor::encryptExecutable(inputFile, outputFile, hardwareKey);
    
    ui->encryptButton->setEnabled(true);
    
    if (success) {
        ui->statusBar->showMessage(tr("File encrypted successfully: %1").arg(outputFile), 5000);
        QMessageBox::information(this, tr("Success"), 
            tr("File encrypted successfully!\n\nEncrypted file saved to:\n%1").arg(outputFile));
    } else {
        ui->statusBar->showMessage(tr("Encryption failed. Check debug output for details."), 5000);
        QMessageBox::critical(this, tr("Error"), tr("Failed to encrypt file. Check debug output for details."));
    }
}

void MoD::onBrowseRunButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Encrypted Executable File"),
        QString(),
        tr("Encrypted Files (*.encrypted);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->runFileLineEdit->setText(fileName);
        updateRunButtonState();
    }
}

void MoD::onRunButtonClicked()
{
    QString encryptedFile = ui->runFileLineEdit->text();
    
    if (encryptedFile.isEmpty() || !QFile::exists(encryptedFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid encrypted file."));
        return;
    }
    
    // Verify hardware key first
    qDebug() << "[DECRYPT] Generating hardware key...";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[DECRYPT] Hardware key:" << hardwareKey;
    qDebug() << "[DECRYPT] Hardware key length:" << hardwareKey.length();
    
    // Verify key consistency by generating again
    QString hardwareKey2 = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[DECRYPT] Hardware key (2nd generation):" << hardwareKey2;
    qDebug() << "[DECRYPT] Keys match:" << (hardwareKey == hardwareKey2 ? "YES" : "NO");
    
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot decrypt file."));
        return;
    }
    
    // Decrypt and run
    ui->statusBar->showMessage(tr("Decrypting and running executable..."), 0);
    ui->runButton->setEnabled(false);
    
    int exitCode = ExecutableEncryptor::decryptAndRunWithHardwareKey(encryptedFile);
    
    ui->runButton->setEnabled(true);
    
    if (exitCode == -1) {
        ui->statusBar->showMessage(tr("Failed to decrypt or run executable. Hardware key may not match."), 5000);
        QMessageBox::critical(this, tr("Error"), 
            tr("Failed to decrypt or run executable.\n\nPossible reasons:\n"
               "- Hardware key does not match the encryption key\n"
               "- File is corrupted\n"
               "- File is not a valid encrypted executable"));
    } else {
        ui->statusBar->showMessage(tr("Executable ran successfully (exit code: %1)").arg(exitCode), 5000);
    }
}

void MoD::updateEncryptButtonState()
{
    QString filePath = ui->encryptFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->encryptButton->setEnabled(isValid);
}

void MoD::updateRunButtonState()
{
    QString filePath = ui->runFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->runButton->setEnabled(isValid);
}
