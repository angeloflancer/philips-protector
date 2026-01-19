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
    QPushButton *generateHardwareKeyButton;
    QSpacerItem *verticalSpacer;
    QMenuBar *menuBar;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(750, 200);
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

        generateHardwareKeyButton = new QPushButton(hardwareKeyGroup);
        generateHardwareKeyButton->setObjectName(QStringLiteral("generateHardwareKeyButton"));
        generateHardwareKeyButton->setMinimumHeight(35);

        hardwareKeyLayout->addWidget(generateHardwareKeyButton);


        mainVerticalLayout->addWidget(hardwareKeyGroup);

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
        QObject::connect(generateHardwareKeyButton, SIGNAL(clicked()), MoD, SLOT(onGenerateHardwareKeyButtonClicked()));

        QMetaObject::connectSlotsByName(MoD);
    } // setupUi

    void retranslateUi(QMainWindow *MoD)
    {
        MoD->setWindowTitle(QApplication::translate("MoD", "Philips Protector", nullptr));
        hardwareKeyGroup->setTitle(QApplication::translate("MoD", "Hardware Fingerprint", nullptr));
        labelKeyDescription->setText(QApplication::translate("MoD", "Unique hardware identifier for this machine:", nullptr));
        keyLineEdit->setPlaceholderText(QApplication::translate("MoD", "Hardware fingerprint will appear here...", nullptr));
        generateHardwareKeyButton->setText(QApplication::translate("MoD", "Generate Hardware Key", nullptr));
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
