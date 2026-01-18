#include "mod.h"
#include "ui_mod.h"

MoD::MoD(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MoD)
{
    ui->setupUi(this);
}

MoD::~MoD()
{
    delete ui;
}
