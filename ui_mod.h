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
    QGroupBox *embedExecGroup;
    QVBoxLayout *embedExecLayout;
    QLabel *labelEmbedDescription;
    QHBoxLayout *embedOriginalFileLayout;
    QLabel *labelOriginalFile;
    QLineEdit *embedOriginalFileLineEdit;
    QPushButton *browseEmbedOriginalButton;
    QHBoxLayout *embedOutputFileLayout;
    QLabel *labelOutputFile;
    QLineEdit *embedOutputFileLineEdit;
    QPushButton *browseEmbedOutputButton;
    QPushButton *createEmbeddedButton;
    QSpacerItem *verticalSpacer;
    QMenuBar *menuBar;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(750, 400);
        centralWidget = new QWidget(MoD);
        centralWidget->setObjectName(QStringLiteral("centralWidget"));
        mainVerticalLayout = new QVBoxLayout(centralWidget);
        mainVerticalLayout->setSpacing(15);
        mainVerticalLayout->setObjectName(QStringLiteral("mainVerticalLayout"));
        mainVerticalLayout->setContentsMargins(20, 20, 20, 20);
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

        embedExecGroup = new QGroupBox(centralWidget);
        embedExecGroup->setObjectName(QStringLiteral("embedExecGroup"));
        embedExecGroup->setFont(font);
        embedExecLayout = new QVBoxLayout(embedExecGroup);
        embedExecLayout->setSpacing(10);
        embedExecLayout->setObjectName(QStringLiteral("embedExecLayout"));
        labelEmbedDescription = new QLabel(embedExecGroup);
        labelEmbedDescription->setObjectName(QStringLiteral("labelEmbedDescription"));
        labelEmbedDescription->setFont(font1);
        labelEmbedDescription->setWordWrap(true);

        embedExecLayout->addWidget(labelEmbedDescription);

        embedOriginalFileLayout = new QHBoxLayout();
        embedOriginalFileLayout->setObjectName(QStringLiteral("embedOriginalFileLayout"));
        labelOriginalFile = new QLabel(embedExecGroup);
        labelOriginalFile->setObjectName(QStringLiteral("labelOriginalFile"));
        labelOriginalFile->setFont(font1);

        embedOriginalFileLayout->addWidget(labelOriginalFile);

        embedOriginalFileLineEdit = new QLineEdit(embedExecGroup);
        embedOriginalFileLineEdit->setObjectName(QStringLiteral("embedOriginalFileLineEdit"));
        embedOriginalFileLineEdit->setReadOnly(true);

        embedOriginalFileLayout->addWidget(embedOriginalFileLineEdit);

        browseEmbedOriginalButton = new QPushButton(embedExecGroup);
        browseEmbedOriginalButton->setObjectName(QStringLiteral("browseEmbedOriginalButton"));
        browseEmbedOriginalButton->setMinimumWidth(80);

        embedOriginalFileLayout->addWidget(browseEmbedOriginalButton);


        embedExecLayout->addLayout(embedOriginalFileLayout);

        embedOutputFileLayout = new QHBoxLayout();
        embedOutputFileLayout->setObjectName(QStringLiteral("embedOutputFileLayout"));
        labelOutputFile = new QLabel(embedExecGroup);
        labelOutputFile->setObjectName(QStringLiteral("labelOutputFile"));
        labelOutputFile->setFont(font1);

        embedOutputFileLayout->addWidget(labelOutputFile);

        embedOutputFileLineEdit = new QLineEdit(embedExecGroup);
        embedOutputFileLineEdit->setObjectName(QStringLiteral("embedOutputFileLineEdit"));
        embedOutputFileLineEdit->setReadOnly(true);

        embedOutputFileLayout->addWidget(embedOutputFileLineEdit);

        browseEmbedOutputButton = new QPushButton(embedExecGroup);
        browseEmbedOutputButton->setObjectName(QStringLiteral("browseEmbedOutputButton"));
        browseEmbedOutputButton->setMinimumWidth(80);

        embedOutputFileLayout->addWidget(browseEmbedOutputButton);


        embedExecLayout->addLayout(embedOutputFileLayout);

        createEmbeddedButton = new QPushButton(embedExecGroup);
        createEmbeddedButton->setObjectName(QStringLiteral("createEmbeddedButton"));
        createEmbeddedButton->setMinimumHeight(35);
        createEmbeddedButton->setEnabled(false);

        embedExecLayout->addWidget(createEmbeddedButton);


        mainVerticalLayout->addWidget(embedExecGroup);

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
        QObject::connect(browseEmbedOriginalButton, SIGNAL(clicked()), MoD, SLOT(onBrowseEmbedOriginalButtonClicked()));
        QObject::connect(browseEmbedOutputButton, SIGNAL(clicked()), MoD, SLOT(onBrowseEmbedOutputButtonClicked()));
        QObject::connect(createEmbeddedButton, SIGNAL(clicked()), MoD, SLOT(onCreateEmbeddedButtonClicked()));

        QMetaObject::connectSlotsByName(MoD);
    } // setupUi

    void retranslateUi(QMainWindow *MoD)
    {
        MoD->setWindowTitle(QApplication::translate("MoD", "Philips Protector", nullptr));
        hardwareKeyGroup->setTitle(QApplication::translate("MoD", "Hardware Fingerprint", nullptr));
        labelKeyDescription->setText(QApplication::translate("MoD", "Unique hardware identifier for this machine:", nullptr));
        keyLineEdit->setPlaceholderText(QApplication::translate("MoD", "Hardware fingerprint will appear here...", nullptr));
        generateButton->setText(QApplication::translate("MoD", "Generate Hardware Key", nullptr));
        embedExecGroup->setTitle(QApplication::translate("MoD", "Create Embedded Executable", nullptr));
        labelEmbedDescription->setText(QApplication::translate("MoD", "Embed an executable file with hardware key protection. The created file will check hardware key before running:", nullptr));
        labelOriginalFile->setText(QApplication::translate("MoD", "Original Executable:", nullptr));
        embedOriginalFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseEmbedOriginalButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        labelOutputFile->setText(QApplication::translate("MoD", "Output Executable:", nullptr));
        embedOutputFileLineEdit->setPlaceholderText(QApplication::translate("MoD", "No file selected...", nullptr));
        browseEmbedOutputButton->setText(QApplication::translate("MoD", "Browse...", nullptr));
        createEmbeddedButton->setText(QApplication::translate("MoD", "Create Embedded Executable", nullptr));
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
