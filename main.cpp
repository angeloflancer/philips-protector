#include "mod.h"
#include <QApplication>

int main(int argc, char *argv[])
{
    QApplication a(argc, argv);
    MoD w;
    w.show();

    return a.exec();
}
