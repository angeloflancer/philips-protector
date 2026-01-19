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
     * @brief Slot to handle hardware key generation button click event
     * Generates hardware key and displays it in the UI
     */
    void onGenerateHardwareKeyButtonClicked();
    
    /**
     * @brief Slot to handle browse button click event
     * Opens file dialog to select executable file
     */
    void onBrowseButtonClicked();

    /**
     * @brief Slot to handle patch executable button click event
     * Patches the selected executable to show Hello on startup
     */
    void onPatchExecutableButtonClicked();

private:
    Ui::MoD *ui;
};

#endif // MOD_H
