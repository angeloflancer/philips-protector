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
    QSpacerItem *verticalSpacer;
    QMenuBar *menuBar;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(750, 550);
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
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
