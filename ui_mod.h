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
#include <QtWidgets/QMainWindow>
#include <QtWidgets/QMenuBar>
#include <QtWidgets/QStatusBar>
#include <QtWidgets/QToolBar>
#include <QtWidgets/QWidget>

QT_BEGIN_NAMESPACE

class Ui_MoD
{
public:
    QMenuBar *menuBar;
    QToolBar *mainToolBar;
    QWidget *centralWidget;
    QStatusBar *statusBar;

    void setupUi(QMainWindow *MoD)
    {
        if (MoD->objectName().isEmpty())
            MoD->setObjectName(QStringLiteral("MoD"));
        MoD->resize(400, 300);
        menuBar = new QMenuBar(MoD);
        menuBar->setObjectName(QStringLiteral("menuBar"));
        MoD->setMenuBar(menuBar);
        mainToolBar = new QToolBar(MoD);
        mainToolBar->setObjectName(QStringLiteral("mainToolBar"));
        MoD->addToolBar(mainToolBar);
        centralWidget = new QWidget(MoD);
        centralWidget->setObjectName(QStringLiteral("centralWidget"));
        MoD->setCentralWidget(centralWidget);
        statusBar = new QStatusBar(MoD);
        statusBar->setObjectName(QStringLiteral("statusBar"));
        MoD->setStatusBar(statusBar);

        retranslateUi(MoD);

        QMetaObject::connectSlotsByName(MoD);
    } // setupUi

    void retranslateUi(QMainWindow *MoD)
    {
        MoD->setWindowTitle(QApplication::translate("MoD", "MoD", nullptr));
    } // retranslateUi

};

namespace Ui {
    class MoD: public Ui_MoD {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MOD_H
