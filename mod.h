#ifndef MOD_H
#define MOD_H

#include <QMainWindow>

namespace Ui {
class MoD;
}

class MoD : public QMainWindow
{
    Q_OBJECT

public:
    explicit MoD(QWidget *parent = nullptr);
    ~MoD();

private:
    Ui::MoD *ui;
};

#endif // MOD_H
