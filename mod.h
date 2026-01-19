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
     * @brief Slot to handle browse button for original executable
     * Opens file dialog to select original executable file to embed
     */
    void onBrowseEmbedOriginalButtonClicked();
    
    /**
     * @brief Slot to handle browse button for output executable
     * Opens file dialog to select where to save the embedded executable
     */
    void onBrowseEmbedOutputButtonClicked();
    
    /**
     * @brief Slot to handle create embedded executable button click
     * Creates an embedded executable with hardware key protection
     */
    void onCreateEmbeddedButtonClicked();

private:
    Ui::MoD *ui;
    
    /**
     * @brief Update create embedded button enabled state based on file selections
     */
    void updateCreateEmbeddedButtonState();
};

#endif // MOD_H
