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
#include <QtWidgets/QLabel>
#include <QtWidgets/QLineEdit>
#include <QtWidgets/QMainWindow>
#include <QtWidgets/QMenuBar>
#include <QtWidgets/QPushButton>
#include <QtWidgets/QStatusBar>
#include <QtWidgets/QToolBar>
#include <QtWidgets/QVBoxLayout>
#include <QtWidgets/QWidget>

QT_BEGIN_NAMESPACE

class Ui_MoD
{
public:
    QMenuBar *menuBar;
    QToolBar *mainToolBar;
    QWidget *centralWidget;
    QVBoxLayout *verticalLayout;
    QLabel *labelTitle;
    QLineEdit *keyLineEdit;
    QPushButton *generateButton;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(600, 200);
        menuBar = new QMenuBar(MoD);
        menuBar->setObjectName(QStringLiteral("menuBar"));
        MoD->setMenuBar(menuBar);
        mainToolBar = new QToolBar(MoD);
        mainToolBar->setObjectName(QStringLiteral("mainToolBar"));
        MoD->addToolBar(mainToolBar);
        centralWidget = new QWidget(MoD);
        centralWidget->setObjectName(QStringLiteral("centralWidget"));
        verticalLayout = new QVBoxLayout(centralWidget);
        verticalLayout->setSpacing(6);
        verticalLayout->setContentsMargins(11, 11, 11, 11);
        verticalLayout->setObjectName(QStringLiteral("verticalLayout"));
        labelTitle = new QLabel(centralWidget);
        labelTitle->setObjectName(QStringLiteral("labelTitle"));
        QFont font;
        font.setPointSize(12);
        font.setBold(true);
        font.setWeight(75);
        labelTitle->setFont(font);

        verticalLayout->addWidget(labelTitle);

        keyLineEdit = new QLineEdit(centralWidget);
        keyLineEdit->setObjectName(QStringLiteral("keyLineEdit"));
        keyLineEdit->setReadOnly(true);
        QFont font1;
        font1.setFamily(QStringLiteral("Consolas"));
        font1.setPointSize(9);
        keyLineEdit->setFont(font1);

        verticalLayout->addWidget(keyLineEdit);

        generateButton = new QPushButton(centralWidget);
        generateButton->setObjectName(QStringLiteral("generateButton"));

        verticalLayout->addWidget(generateButton);

        MoD->setCentralWidget(centralWidget);
        statusBar = new QStatusBar(MoD);
        statusBar->setObjectName(QStringLiteral("statusBar"));
        MoD->setStatusBar(statusBar);

        retranslateUi(MoD);
        QObject::connect(generateButton, SIGNAL(clicked()), MoD, SLOT(onGenerateButtonClicked()));

        QMetaObject::connectSlotsByName(MoD);
    } // setupUi

    void retranslateUi(QMainWindow *MoD)
    {
        MoD->setWindowTitle(QApplication::translate("MoD", "Philips Protector", nullptr));
        labelTitle->setText(QApplication::translate("MoD", "Hardware Fingerprint Key", nullptr));
        generateButton->setText(QApplication::translate("MoD", "Generate Hardware Key", nullptr));
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
