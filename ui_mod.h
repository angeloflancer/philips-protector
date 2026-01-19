/********************************************************************************
** Form generated from reading UI file 'mod.ui'
**
** Created by: Qt User Interface Compiler version 5.11.3
**
** WARNING! All changes made in this file will be lost when recompiling UI file!
********************************************************************************/

#ifndef UI_MOD_H
#define UI_MOD_H

#include <QtCore/QVariant>
#include <QtWidgets/QApplication>
#include <QtWidgets/QGroupBox>
#include <QtWidgets/QHBoxLayout>
#include <QtWidgets/QLabel>
#include <QtWidgets/QLineEdit>
#include <QtWidgets/QMainWindow>
#include <QtWidgets/QMenuBar>
#include <QtWidgets/QPushButton>
#include <QtWidgets/QSpacerItem>
#include <QtWidgets/QStatusBar>
#include <QtWidgets/QVBoxLayout>
#include <QtWidgets/QWidget>

QT_BEGIN_NAMESPACE

class Ui_MoD
{
public:
    QWidget *centralWidget;
    QVBoxLayout *mainVerticalLayout;
    QGroupBox *hardwareKeyGroup;
    QVBoxLayout *hardwareKeyLayout;
    QLabel *labelKeyDescription;
    QLineEdit *keyLineEdit;
    QPushButton *generateButton;
    QGroupBox *encryptGroup;
    QVBoxLayout *encryptLayout;
    QLabel *labelEncryptDescription;
    QHBoxLayout *encryptFileLayout;
    QLineEdit *encryptFileLineEdit;
    QPushButton *browseEncryptButton;
    QPushButton *encryptButton;
    QGroupBox *runGroup;
    QVBoxLayout *runLayout;
    QLabel *labelRunDescription;
    QHBoxLayout *runFileLayout;
    QLineEdit *runFileLineEdit;
    QPushButton *browseRunButton;
    QPushButton *runButton;
    QGroupBox *memoryExecGroup;
    QVBoxLayout *memoryExecLayout;
    QLabel *labelMemoryExecDescription;
    QHBoxLayout *memoryEncryptFileLayout;
    QLineEdit *memoryEncryptFileLineEdit;
    QPushButton *browseMemoryEncryptButton;
    QPushButton *memoryEncryptButton;
    QHBoxLayout *memoryRunFileLayout;
    QLineEdit *memoryRunFileLineEdit;
    QPushButton *browseMemoryRunButton;
    QPushButton *memoryRunButton;
    QGroupBox *runtimeProtectGroup;
    QVBoxLayout *runtimeProtectLayout;
    QLabel *labelRuntimeProtectDescription;
    QHBoxLayout *runtimeProtectEncryptFileLayout;
    QLineEdit *runtimeProtectEncryptFileLineEdit;
    QPushButton *browseRuntimeProtectEncryptButton;
    QPushButton *runtimeProtectEncryptButton;
    QHBoxLayout *runtimeProtectRunFileLayout;
    QLineEdit *runtimeProtectRunFileLineEdit;
    QPushButton *browseRuntimeProtectRunButton;
    QPushButton *runtimeProtectRunButton;
    QSpacerItem *verticalSpacer;
    QMenuBar *menuBar;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(750, 950);
        centralWidget = new QWidget(MoD);
        centralWidget->setObjectName(QStringLiteral("centralWidget"));
        mainVerticalLayout = new QVBoxLayout(centralWidget);
        mainVerticalLayout->setSpacing(15);
        mainVerticalLayout->setContentsMargins(20, 20, 20, 20);
        mainVerticalLayout->setObjectName(QStringLiteral("mainVerticalLayout"));
        hardwareKeyGroup = new QGroupBox(centralWidget);
        hardwareKeyGroup->setObjectName(QStringLiteral("hardwareKeyGroup"));
        QFont font;
        font.setPointSize(10);
        font.setBold(true);
        font.setWeight(75);
        hardwareKeyGroup->setFont(font);
        hardwareKeyLayout = new QVBoxLayout(hardwareKeyGroup);
        hardwareKeyLayout->setSpacing(10);
        hardwareKeyLayout->setObjectName(QStringLiteral("hardwareKeyLayout"));
        labelKeyDescription = new QLabel(hardwareKeyGroup);
        labelKeyDescription->setObjectName(QStringLiteral("labelKeyDescription"));
        QFont font1;
        font1.setPointSize(9);
        labelKeyDescription->setFont(font1);

        hardwareKeyLayout->addWidget(labelKeyDescription);

        keyLineEdit = new QLineEdit(hardwareKeyGroup);
        keyLineEdit->setObjectName(QStringLiteral("keyLineEdit"));
        keyLineEdit->setReadOnly(true);
        QFont font2;
        font2.setFamily(QStringLiteral("Consolas"));
        font2.setPointSize(9);
        keyLineEdit->setFont(font2);

        hardwareKeyLayout->addWidget(keyLineEdit);

        generateButton = new QPushButton(hardwareKeyGroup);
        generateButton->setObjectName(QStringLiteral("generateButton"));
        generateButton->setMinimumHeight(35);

        hardwareKeyLayout->addWidget(generateButton);

        mainVerticalLayout->addWidget(hardwareKeyGroup);

        encryptGroup = new QGroupBox(centralWidget);
        encryptGroup->setObjectName(QStringLiteral("encryptGroup"));
        encryptGroup->setFont(font);
        encryptLayout = new QVBoxLayout(encryptGroup);
        encryptLayout->setSpacing(10);
        encryptLayout->setObjectName(QStringLiteral("encryptLayout"));
        labelEncryptDescription = new QLabel(encryptGroup);
        labelEncryptDescription->setObjectName(QStringLiteral("labelEncryptDescription"));
        labelEncryptDescription->setFont(font1);

        encryptLayout->addWidget(labelEncryptDescription);

        encryptFileLayout = new QHBoxLayout();
        encryptFileLayout->setObjectName(QStringLiteral("encryptFileLayout"));
        encryptFileLineEdit = new QLineEdit(encryptGroup);
        encryptFileLineEdit->setObjectName(QStringLiteral("encryptFileLineEdit"));
        encryptFileLineEdit->setReadOnly(true);

        encryptFileLayout->addWidget(encryptFileLineEdit);

        browseEncryptButton = new QPushButton(encryptGroup);
        browseEncryptButton->setObjectName(QStringLiteral("browseEncryptButton"));
        browseEncryptButton->setMinimumWidth(80);

        encryptFileLayout->addWidget(browseEncryptButton);

        encryptLayout->addLayout(encryptFileLayout);

        encryptButton = new QPushButton(encryptGroup);
        encryptButton->setObjectName(QStringLiteral("encryptButton"));
        encryptButton->setMinimumHeight(35);
        encryptButton->setEnabled(false);

        encryptLayout->addWidget(encryptButton);

        mainVerticalLayout->addWidget(encryptGroup);

        runGroup = new QGroupBox(centralWidget);
        runGroup->setObjectName(QStringLiteral("runGroup"));
        runGroup->setFont(font);
        runLayout = new QVBoxLayout(runGroup);
        runLayout->setSpacing(10);
        runLayout->setObjectName(QStringLiteral("runLayout"));
        labelRunDescription = new QLabel(runGroup);
        labelRunDescription->setObjectName(QStringLiteral("labelRunDescription"));
        labelRunDescription->setFont(font1);

        runLayout->addWidget(labelRunDescription);

        runFileLayout = new QHBoxLayout();
        runFileLayout->setObjectName(QStringLiteral("runFileLayout"));
        runFileLineEdit = new QLineEdit(runGroup);
        runFileLineEdit->setObjectName(QStringLiteral("runFileLineEdit"));
        runFileLineEdit->setReadOnly(true);

        runFileLayout->addWidget(runFileLineEdit);

        browseRunButton = new QPushButton(runGroup);
        browseRunButton->setObjectName(QStringLiteral("browseRunButton"));
        browseRunButton->setMinimumWidth(80);

        runFileLayout->addWidget(browseRunButton);

        runLayout->addLayout(runFileLayout);

        runButton = new QPushButton(runGroup);
        runButton->setObjectName(QStringLiteral("runButton"));
        runButton->setMinimumHeight(35);
        runButton->setEnabled(false);

        runLayout->addWidget(runButton);

        mainVerticalLayout->addWidget(runGroup);

        memoryExecGroup = new QGroupBox(centralWidget);
        memoryExecGroup->setObjectName(QStringLiteral("memoryExecGroup"));
        memoryExecGroup->setFont(font);
        memoryExecLayout = new QVBoxLayout(memoryExecGroup);
        memoryExecLayout->setSpacing(10);
        memoryExecLayout->setObjectName(QStringLiteral("memoryExecLayout"));
        labelMemoryExecDescription = new QLabel(memoryExecGroup);
        labelMemoryExecDescription->setObjectName(QStringLiteral("labelMemoryExecDescription"));
        labelMemoryExecDescription->setFont(font1);

        memoryExecLayout->addWidget(labelMemoryExecDescription);

        memoryEncryptFileLayout = new QHBoxLayout();
        memoryEncryptFileLayout->setObjectName(QStringLiteral("memoryEncryptFileLayout"));
        memoryEncryptFileLineEdit = new QLineEdit(memoryExecGroup);
        memoryEncryptFileLineEdit->setObjectName(QStringLiteral("memoryEncryptFileLineEdit"));
        memoryEncryptFileLineEdit->setReadOnly(true);

        memoryEncryptFileLayout->addWidget(memoryEncryptFileLineEdit);

        browseMemoryEncryptButton = new QPushButton(memoryExecGroup);
        browseMemoryEncryptButton->setObjectName(QStringLiteral("browseMemoryEncryptButton"));
        browseMemoryEncryptButton->setMinimumWidth(80);

        memoryEncryptFileLayout->addWidget(browseMemoryEncryptButton);

        memoryExecLayout->addLayout(memoryEncryptFileLayout);

        memoryEncryptButton = new QPushButton(memoryExecGroup);
        memoryEncryptButton->setObjectName(QStringLiteral("memoryEncryptButton"));
        memoryEncryptButton->setMinimumHeight(35);
        memoryEncryptButton->setEnabled(false);

        memoryExecLayout->addWidget(memoryEncryptButton);

        memoryRunFileLayout = new QHBoxLayout();
        memoryRunFileLayout->setObjectName(QStringLiteral("memoryRunFileLayout"));
        memoryRunFileLineEdit = new QLineEdit(memoryExecGroup);
        memoryRunFileLineEdit->setObjectName(QStringLiteral("memoryRunFileLineEdit"));
        memoryRunFileLineEdit->setReadOnly(true);

        memoryRunFileLayout->addWidget(memoryRunFileLineEdit);

        browseMemoryRunButton = new QPushButton(memoryExecGroup);
        browseMemoryRunButton->setObjectName(QStringLiteral("browseMemoryRunButton"));
        browseMemoryRunButton->setMinimumWidth(80);

        memoryRunFileLayout->addWidget(browseMemoryRunButton);

        memoryExecLayout->addLayout(memoryRunFileLayout);

        memoryRunButton = new QPushButton(memoryExecGroup);
        memoryRunButton->setObjectName(QStringLiteral("memoryRunButton"));
        memoryRunButton->setMinimumHeight(35);
        memoryRunButton->setEnabled(false);

        memoryExecLayout->addWidget(memoryRunButton);

        mainVerticalLayout->addWidget(memoryExecGroup);

        runtimeProtectGroup = new QGroupBox(centralWidget);
        runtimeProtectGroup->setObjectName(QStringLiteral("runtimeProtectGroup"));
        runtimeProtectGroup->setFont(font);
        runtimeProtectLayout = new QVBoxLayout(runtimeProtectGroup);
        runtimeProtectLayout->setSpacing(10);
        runtimeProtectLayout->setObjectName(QStringLiteral("runtimeProtectLayout"));
        labelRuntimeProtectDescription = new QLabel(runtimeProtectGroup);
        labelRuntimeProtectDescription->setObjectName(QStringLiteral("labelRuntimeProtectDescription"));
        labelRuntimeProtectDescription->setFont(font1);
        labelRuntimeProtectDescription->setWordWrap(true);

        runtimeProtectLayout->addWidget(labelRuntimeProtectDescription);

        runtimeProtectEncryptFileLayout = new QHBoxLayout();
        runtimeProtectEncryptFileLayout->setObjectName(QStringLiteral("runtimeProtectEncryptFileLayout"));
        runtimeProtectEncryptFileLineEdit = new QLineEdit(runtimeProtectGroup);
        runtimeProtectEncryptFileLineEdit->setObjectName(QStringLiteral("runtimeProtectEncryptFileLineEdit"));
        runtimeProtectEncryptFileLineEdit->setReadOnly(true);

        runtimeProtectEncryptFileLayout->addWidget(runtimeProtectEncryptFileLineEdit);

        browseRuntimeProtectEncryptButton = new QPushButton(runtimeProtectGroup);
        browseRuntimeProtectEncryptButton->setObjectName(QStringLiteral("browseRuntimeProtectEncryptButton"));
        browseRuntimeProtectEncryptButton->setMinimumWidth(80);

        runtimeProtectEncryptFileLayout->addWidget(browseRuntimeProtectEncryptButton);

        runtimeProtectLayout->addLayout(runtimeProtectEncryptFileLayout);

        runtimeProtectEncryptButton = new QPushButton(runtimeProtectGroup);
        runtimeProtectEncryptButton->setObjectName(QStringLiteral("runtimeProtectEncryptButton"));
        runtimeProtectEncryptButton->setMinimumHeight(35);
        runtimeProtectEncryptButton->setEnabled(false);

        runtimeProtectLayout->addWidget(runtimeProtectEncryptButton);

        runtimeProtectRunFileLayout = new QHBoxLayout();
        runtimeProtectRunFileLayout->setObjectName(QStringLiteral("runtimeProtectRunFileLayout"));
        runtimeProtectRunFileLineEdit = new QLineEdit(runtimeProtectGroup);
        runtimeProtectRunFileLineEdit->setObjectName(QStringLiteral("runtimeProtectRunFileLineEdit"));
        runtimeProtectRunFileLineEdit->setReadOnly(true);

        runtimeProtectRunFileLayout->addWidget(runtimeProtectRunFileLineEdit);

        browseRuntimeProtectRunButton = new QPushButton(runtimeProtectGroup);
        browseRuntimeProtectRunButton->setObjectName(QStringLiteral("browseRuntimeProtectRunButton"));
        browseRuntimeProtectRunButton->setMinimumWidth(80);

        runtimeProtectRunFileLayout->addWidget(browseRuntimeProtectRunButton);

        runtimeProtectLayout->addLayout(runtimeProtectRunFileLayout);

        runtimeProtectRunButton = new QPushButton(runtimeProtectGroup);
        runtimeProtectRunButton->setObjectName(QStringLiteral("runtimeProtectRunButton"));
        runtimeProtectRunButton->setMinimumHeight(35);
        runtimeProtectRunButton->setEnabled(false);

        runtimeProtectLayout->addWidget(runtimeProtectRunButton);

        mainVerticalLayout->addWidget(runtimeProtectGroup);

        verticalSpacer = new QSpacerItem(20, 40, QSizePolicy::Minimum, QSizePolicy::Expanding);

        mainVerticalLayout->addItem(verticalSpacer);

        MoD->setCentralWidget(centralWidget);
        menuBar = new QMenuBar(MoD);
        menuBar->setObjectName(QStringLiteral("menuBar"));
        MoD->setMenuBar(menuBar);
        statusBar = new QStatusBar(MoD);
        statusBar->setObjectName(QStringLiteral("statusBar"));
        MoD->setStatusBar(statusBar);

        retranslateUi(MoD);
        QObject::connect(generateButton, SIGNAL(clicked()), MoD, SLOT(onGenerateButtonClicked()));
        QObject::connect(browseEncryptButton, SIGNAL(clicked()), MoD, SLOT(onBrowseEncryptButtonClicked()));
        QObject::connect(encryptButton, SIGNAL(clicked()), MoD, SLOT(onEncryptButtonClicked()));
        QObject::connect(browseRunButton, SIGNAL(clicked()), MoD, SLOT(onBrowseRunButtonClicked()));
        QObject::connect(runButton, SIGNAL(clicked()), MoD, SLOT(onRunButtonClicked()));
        QObject::connect(browseMemoryEncryptButton, SIGNAL(clicked()), MoD, SLOT(onBrowseMemoryEncryptButtonClicked()));
        QObject::connect(memoryEncryptButton, SIGNAL(clicked()), MoD, SLOT(onMemoryEncryptButtonClicked()));
        QObject::connect(browseMemoryRunButton, SIGNAL(clicked()), MoD, SLOT(onBrowseMemoryRunButtonClicked()));
        QObject::connect(memoryRunButton, SIGNAL(clicked()), MoD, SLOT(onMemoryRunButtonClicked()));
        QObject::connect(browseRuntimeProtectEncryptButton, SIGNAL(clicked()), MoD, SLOT(onBrowseRuntimeProtectEncryptButtonClicked()));
        QObject::connect(runtimeProtectEncryptButton, SIGNAL(clicked()), MoD, SLOT(onRuntimeProtectEncryptButtonClicked()));
        QObject::connect(browseRuntimeProtectRunButton, SIGNAL(clicked()), MoD, SLOT(onBrowseRuntimeProtectRunButtonClicked()));
        QObject::connect(runtimeProtectRunButton, SIGNAL(clicked()), MoD, SLOT(onRuntimeProtectRunButtonClicked()));

        QMetaObject::connectSlotsByName(MoD);
    } // setupUi

    void retranslateUi(QMainWindow *MoD)
    {
        MoD->setWindowTitle(QApplication::translate("MoD", "Philips Protector", nullptr));
        hardwareKeyGroup->setTitle(QApplication::translate("MoD", "Hardware Fingerprint", nullptr));
        labelKeyDescription->setText(QApplication::translate("MoD", "Unique hardware identifier for this machine:", nullptr));
        keyLineEdit->setPlaceholderText(QApplication::translate("MoD", "Hardware fingerprint will appear here...", nullptr));
        generateButton->setText(QApplication::translate("MoD", "Generate Hardware Key", nullptr));
        encryptGroup->setTitle(QApplication::translate("MoD", "Encrypt Executable", nullptr));
        labelEncryptDescription->setText(QApplication::translate("MoD", "Select an executable file to encrypt with hardware key:", nullptr));
        encryptFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseEncryptButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        encryptButton->setText(QApplication::translate("MoD", "Encrypt Executable", nullptr));
        runGroup->setTitle(QApplication::translate("MoD", "Run Encrypted Executable", nullptr));
        labelRunDescription->setText(QApplication::translate("MoD", "Select an encrypted executable file to decrypt and run:", nullptr));
        runFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseRunButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        runButton->setText(QApplication::translate("MoD", "Run Encrypted Executable", nullptr));
        memoryExecGroup->setTitle(QApplication::translate("MoD", "Memory Execution (No Disk Decryption)", nullptr));
        labelMemoryExecDescription->setText(QApplication::translate("MoD", "Encrypt with hardware key and run directly from memory (no decrypted file on disk):", nullptr));
        memoryEncryptFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseMemoryEncryptButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        memoryEncryptButton->setText(QApplication::translate("MoD", "Encrypt with Hardware Key (Memory Mode)", nullptr));
        memoryRunFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No encrypted file selected...", nullptr));
        browseMemoryRunButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        memoryRunButton->setText(QApplication::translate("MoD", "Run from Memory (No Decrypt to Disk)", nullptr));
        runtimeProtectGroup->setTitle(QApplication::translate("MoD", "Runtime Protection (Hardware-Bound + Anti-Debugging)", nullptr));
        labelRuntimeProtectDescription->setText(QApplication::translate("MoD", "Advanced protection: Embeds hardware fingerprint, verifies at runtime, and includes anti-debugging. Even if decrypted, won't run on other machines:", nullptr));
        runtimeProtectEncryptFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseRuntimeProtectEncryptButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        runtimeProtectEncryptButton->setText(QApplication::translate("MoD", "Encrypt with Runtime Protection", nullptr));
        runtimeProtectRunFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No protected file selected...", nullptr));
        browseRuntimeProtectRunButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        runtimeProtectRunButton->setText(QApplication::translate("MoD", "Run Protected Executable", nullptr));
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
