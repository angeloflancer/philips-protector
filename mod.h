#ifndef MOD_H
#define MOD_H

#include <QMainWindow>
#include <QTableWidget>
#include <QString>

namespace Ui {
class MoD;
}

struct USBDeviceInfo {
    QString deviceName;
    QString vendorID;
    QString productID;
    QString serialNumber;
    QString manufacturer;
    QString description;
    QString deviceType;
};

class MoD : public QMainWindow
{
    Q_OBJECT

public:
    explicit MoD(QWidget *parent = nullptr);
    ~MoD();

private slots:
    void onRefreshClicked();

private:
    void enumerateUSBDevices();
    void populateDeviceTable(const QList<USBDeviceInfo>& devices);
    Ui::MoD *ui;
};

#endif // MOD_H
