#include "mod.h"
#include "ui_mod.h"
#include "hardwarefingerprint.h"
#include "executableencryptor.h"
#include "memoryexecuteloader.h"
#include "runtimeprotector.h"
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
    connect(ui->memoryEncryptFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateMemoryEncryptButtonState);
    connect(ui->memoryRunFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateMemoryRunButtonState);
    connect(ui->runtimeProtectEncryptFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateRuntimeProtectEncryptButtonState);
    connect(ui->runtimeProtectRunFileLineEdit, &QLineEdit::textChanged, this, &MoD::updateRuntimeProtectRunButtonState);
    
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

void MoD::onBrowseMemoryEncryptButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Executable File"),
        QString(),
        tr("Executable Files (*.exe);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->memoryEncryptFileLineEdit->setText(fileName);
        updateMemoryEncryptButtonState();
    }
}

void MoD::onMemoryEncryptButtonClicked()
{
    QString inputFile = ui->memoryEncryptFileLineEdit->text();
    
    if (inputFile.isEmpty() || !QFile::exists(inputFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid executable file."));
        return;
    }
    
    // Get hardware key
    qDebug() << "[MEMORY ENCRYPT] Generating hardware key...";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[MEMORY ENCRYPT] Hardware key:" << hardwareKey;
    
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot encrypt file."));
        return;
    }
    
    // Ask user for output file location
    QFileInfo fileInfo(inputFile);
    QString defaultOutput = fileInfo.path() + QDir::separator() + fileInfo.baseName() + "_memory_encrypted";
    
    QString outputFile = QFileDialog::getSaveFileName(
        this,
        tr("Save Encrypted File (Memory Mode)"),
        defaultOutput,
        tr("Encrypted Files (*.encrypted);;All Files (*.*)")
    );
    
    if (outputFile.isEmpty()) {
        return; // User cancelled
    }
    
    // Encrypt the file using MemoryExecuteLoader (with hardware key as password)
    ui->statusBar->showMessage(tr("Encrypting file with hardware key (memory mode)..."), 0);
    ui->memoryEncryptButton->setEnabled(false);
    
    bool success = MemoryExecuteLoader::encryptExecutable(inputFile, outputFile, hardwareKey);
    
    ui->memoryEncryptButton->setEnabled(true);
    
    if (success) {
        ui->statusBar->showMessage(tr("File encrypted successfully: %1").arg(outputFile), 5000);
        QMessageBox::information(this, tr("Success"), 
            tr("File encrypted successfully with hardware key!\n\n"
               "Encrypted file saved to:\n%1\n\n"
               "This file can only be run using 'Run from Memory' button.").arg(outputFile));
    } else {
        ui->statusBar->showMessage(tr("Encryption failed. Check debug output for details."), 5000);
        QMessageBox::critical(this, tr("Error"), tr("Failed to encrypt file. Check debug output for details."));
    }
}

void MoD::onBrowseMemoryRunButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Encrypted Executable File (Memory Mode)"),
        QString(),
        tr("Encrypted Files (*.encrypted);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->memoryRunFileLineEdit->setText(fileName);
        updateMemoryRunButtonState();
    }
}

void MoD::onMemoryRunButtonClicked()
{
    QString encryptedFile = ui->memoryRunFileLineEdit->text();
    
    if (encryptedFile.isEmpty() || !QFile::exists(encryptedFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid encrypted file."));
        return;
    }
    
    // Get hardware key
    qDebug() << "[MEMORY RUN] Generating hardware key...";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[MEMORY RUN] Hardware key:" << hardwareKey;
    
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot decrypt file."));
        return;
    }
    
    // Decrypt and run from memory
    ui->statusBar->showMessage(tr("Decrypting and running from memory..."), 0);
    ui->memoryRunButton->setEnabled(false);
    
    int exitCode = MemoryExecuteLoader::decryptAndExecuteFromMemory(encryptedFile, hardwareKey);
    
    ui->memoryRunButton->setEnabled(true);
    
    if (exitCode == -1) {
        ui->statusBar->showMessage(tr("Failed to decrypt or run executable from memory. Hardware key may not match."), 5000);
        QMessageBox::critical(this, tr("Error"), 
            tr("Failed to decrypt or run executable from memory.\n\nPossible reasons:\n"
               "- Hardware key does not match the encryption key\n"
               "- File is corrupted\n"
               "- File is not a valid encrypted executable"));
    } else {
        ui->statusBar->showMessage(tr("Executable ran successfully from memory (exit code: %1)").arg(exitCode), 5000);
        QMessageBox::information(this, tr("Success"), 
            tr("Executable executed successfully from memory!\n\n"
               "Exit code: %1\n\n"
               "Note: The decrypted executable was never written to disk.").arg(exitCode));
    }
}

void MoD::updateMemoryEncryptButtonState()
{
    QString filePath = ui->memoryEncryptFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->memoryEncryptButton->setEnabled(isValid);
}

void MoD::updateMemoryRunButtonState()
{
    QString filePath = ui->memoryRunFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->memoryRunButton->setEnabled(isValid);
}

void MoD::onBrowseRuntimeProtectEncryptButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Executable File"),
        QString(),
        tr("Executable Files (*.exe);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->runtimeProtectEncryptFileLineEdit->setText(fileName);
        updateRuntimeProtectEncryptButtonState();
    }
}

void MoD::onRuntimeProtectEncryptButtonClicked()
{
    QString inputFile = ui->runtimeProtectEncryptFileLineEdit->text();
    
    if (inputFile.isEmpty() || !QFile::exists(inputFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid executable file."));
        return;
    }
    
    // Get hardware key
    qDebug() << "[RUNTIME PROTECT ENCRYPT] Generating hardware key...";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    qDebug() << "[RUNTIME PROTECT ENCRYPT] Hardware key:" << hardwareKey;
    
    if (hardwareKey.isEmpty()) {
        QMessageBox::critical(this, tr("Error"), tr("Failed to generate hardware key. Cannot encrypt file."));
        return;
    }
    
    // Ask user for output file location
    QFileInfo fileInfo(inputFile);
    QString defaultOutput = fileInfo.path() + QDir::separator() + fileInfo.baseName() + "_protected";
    
    QString outputFile = QFileDialog::getSaveFileName(
        this,
        tr("Save Protected Executable File"),
        defaultOutput,
        tr("Protected Files (*.protected);;All Files (*.*)")
    );
    
    if (outputFile.isEmpty()) {
        return; // User cancelled
    }
    
    // Encrypt with runtime protection
    ui->statusBar->showMessage(tr("Encrypting with runtime protection..."), 0);
    ui->runtimeProtectEncryptButton->setEnabled(false);
    
    bool success = RuntimeProtector::encryptWithRuntimeProtection(inputFile, outputFile, hardwareKey);
    
    ui->runtimeProtectEncryptButton->setEnabled(true);
    
    if (success) {
        ui->statusBar->showMessage(tr("File encrypted with runtime protection: %1").arg(outputFile), 5000);
        QMessageBox::information(this, tr("Success"), 
            tr("File encrypted successfully with runtime protection!\n\n"
               "Protected file saved to:\n%1\n\n"
               "This file:\n"
               "- Is bound to this machine's hardware\n"
               "- Will verify hardware at runtime\n"
               "- Includes anti-debugging protection\n"
               "- Will NOT run on other machines, even if decrypted").arg(outputFile));
    } else {
        ui->statusBar->showMessage(tr("Encryption failed. Check debug output for details."), 5000);
        QMessageBox::critical(this, tr("Error"), tr("Failed to encrypt file with runtime protection. Check debug output for details."));
    }
}

void MoD::onBrowseRuntimeProtectRunButtonClicked()
{
    QString fileName = QFileDialog::getOpenFileName(
        this,
        tr("Select Protected Executable File"),
        QString(),
        tr("Protected Files (*.protected);;All Files (*.*)")
    );
    
    if (!fileName.isEmpty()) {
        ui->runtimeProtectRunFileLineEdit->setText(fileName);
        updateRuntimeProtectRunButtonState();
    }
}

void MoD::onRuntimeProtectRunButtonClicked()
{
    QString protectedFile = ui->runtimeProtectRunFileLineEdit->text();
    
    if (protectedFile.isEmpty() || !QFile::exists(protectedFile)) {
        QMessageBox::warning(this, tr("Error"), tr("Please select a valid protected file."));
        return;
    }
    
    // Decrypt and run with runtime protection
    ui->statusBar->showMessage(tr("Verifying hardware and running protected executable..."), 0);
    ui->runtimeProtectRunButton->setEnabled(false);
    
    int exitCode = RuntimeProtector::decryptAndExecuteProtected(protectedFile);
    
    ui->runtimeProtectRunButton->setEnabled(true);
    
    if (exitCode == -1) {
        ui->statusBar->showMessage(tr("Failed to run protected executable. Hardware may not match or debugger detected."), 5000);
        QMessageBox::critical(this, tr("Error"), 
            tr("Failed to run protected executable.\n\nPossible reasons:\n"
               "- Hardware fingerprint does not match (file was encrypted for a different machine)\n"
               "- Debugger detected (anti-debugging protection)\n"
               "- File is corrupted\n"
               "- File is not a valid protected executable"));
    } else {
        ui->statusBar->showMessage(tr("Protected executable ran successfully (exit code: %1)").arg(exitCode), 5000);
        QMessageBox::information(this, tr("Success"), 
            tr("Protected executable executed successfully!\n\n"
               "Exit code: %1\n\n"
               "Hardware verification: PASSED\n"
               "Anti-debugging checks: PASSED").arg(exitCode));
    }
}

void MoD::updateRuntimeProtectEncryptButtonState()
{
    QString filePath = ui->runtimeProtectEncryptFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->runtimeProtectEncryptButton->setEnabled(isValid);
}

void MoD::updateRuntimeProtectRunButtonState()
{
    QString filePath = ui->runtimeProtectRunFileLineEdit->text();
    bool isValid = !filePath.isEmpty() && QFile::exists(filePath);
    ui->runtimeProtectRunButton->setEnabled(isValid);
}
