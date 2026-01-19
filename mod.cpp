#include "mod.h"
#include "ui_mod.h"
#include "hardwarefingerprint.h"
#include <QPalette>

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
