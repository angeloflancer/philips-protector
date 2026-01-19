#ifndef MOD_H
#define MOD_H

#include <QMainWindow>
#include <QString>

namespace Ui {
class MoD;
}

/**
 * @brief MoD (Main of Dialog) - Main application window class
 * 
 * This class handles the UI and user interactions for the Philips Protector application.
 * It uses HardwareFingerprint to generate and display hardware keys.
 */
class MoD : public QMainWindow
{
    Q_OBJECT

public:
    explicit MoD(QWidget *parent = nullptr);
    ~MoD();

private slots:
    /**
     * @brief Slot to handle generate button click event
     * Generates hardware key and displays it in the UI
     */
    void onGenerateButtonClicked();
    
    /**
     * @brief Slot to handle browse button for encryption
     * Opens file dialog to select executable file to encrypt
     */
    void onBrowseEncryptButtonClicked();
    
    /**
     * @brief Slot to handle encrypt button click
     * Encrypts the selected executable file with hardware key
     */
    void onEncryptButtonClicked();
    
    /**
     * @brief Slot to handle browse button for running
     * Opens file dialog to select encrypted executable file
     */
    void onBrowseRunButtonClicked();
    
    /**
     * @brief Slot to handle run button click
     * Decrypts and runs the selected encrypted executable
     */
    void onRunButtonClicked();
    
    /**
     * @brief Slot to handle browse button for memory encryption
     * Opens file dialog to select executable file to encrypt with hardware key
     */
    void onBrowseMemoryEncryptButtonClicked();
    
    /**
     * @brief Slot to handle memory encrypt button click
     * Encrypts the selected executable file with hardware key (for memory execution)
     */
    void onMemoryEncryptButtonClicked();
    
    /**
     * @brief Slot to handle browse button for memory execution
     * Opens file dialog to select encrypted executable file for memory execution
     */
    void onBrowseMemoryRunButtonClicked();
    
    /**
     * @brief Slot to handle memory run button click
     * Decrypts and runs the selected encrypted executable directly from memory
     */
    void onMemoryRunButtonClicked();

private:
    Ui::MoD *ui;
    
    /**
     * @brief Update encrypt button enabled state based on file selection
     */
    void updateEncryptButtonState();
    
    /**
     * @brief Update run button enabled state based on file selection
     */
    void updateRunButtonState();
    
    /**
     * @brief Update memory encrypt button enabled state based on file selection
     */
    void updateMemoryEncryptButtonState();
    
    /**
     * @brief Update memory run button enabled state based on file selection
     */
    void updateMemoryRunButtonState();
};

#endif // MOD_H
