using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;
using MazManager.Services;
using MessageBox = System.Windows.MessageBox;

namespace MazManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServiceController _serviceController;
        private ServiceInstaller _serviceInstaller;
        private NotifyIcon _notifyIcon;
        private System.Windows.Threading.DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            _serviceController = new ServiceController();
            _serviceInstaller = new ServiceInstaller();

            // Initialize notification icon
            InitializeNotifyIcon();

            // Load hardware fingerprint
            LoadHardwareFingerprint();

            // Start status monitoring
            StartStatusMonitoring();
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = "Medicare Protection Manager";
            _notifyIcon.Visible = false;
        }

        private void ShowNotification(string title, string message)
        {
            try
            {
                _notifyIcon.Visible = true;
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(3000);

                // Hide icon after notification
                System.Threading.Timer hideTimer = null;
                hideTimer = new System.Threading.Timer((state) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _notifyIcon.Visible = false;
                    });
                    hideTimer.Dispose();
                }, null, 5000, System.Threading.Timeout.Infinite);
            }
            catch
            {
                // Fallback to message box
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadHardwareFingerprint()
        {
            try
            {
                string fingerprint = LicenseGenerator.GenerateLicenseKey();
                FingerprintText.Text = fingerprint;
            }
            catch (Exception ex)
            {
                FingerprintText.Text = "Error loading fingerprint: " + ex.Message;
            }
        }

        private void StartStatusMonitoring()
        {
            UpdateServiceStatus();

            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(5);
            _statusTimer.Tick += (s, e) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        private void UpdateServiceStatus()
        {
            try
            {
                bool isInstalled = _serviceController.IsServiceInstalled();
                bool isRunning = false;

                if (isInstalled)
                {
                    isRunning = _serviceController.IsServiceRunning();
                }

                if (isInstalled && isRunning)
                {
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("SuccessBrush");
                    StatusText.Text = "Running";
                    StatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
                    
                    StartButton.IsEnabled = false;
                    StopButton.IsEnabled = true;
                    InstallButton.IsEnabled = false;
                    UninstallButton.IsEnabled = false;
                }
                else if (isInstalled && !isRunning)
                {
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("WarningBrush");
                    StatusText.Text = "Stopped";
                    StatusText.Foreground = (SolidColorBrush)FindResource("WarningBrush");
                    
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    InstallButton.IsEnabled = false;
                    UninstallButton.IsEnabled = true;
                }
                else
                {
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("TextSecondaryBrush");
                    StatusText.Text = "Not Installed";
                    StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
                    
                    StartButton.IsEnabled = false;
                    StopButton.IsEnabled = false;
                    InstallButton.IsEnabled = true;
                    UninstallButton.IsEnabled = false;
                }

                ManageExeButton.IsEnabled = isInstalled;
            }
            catch (Exception ex)
            {
                StatusIndicator.Fill = (SolidColorBrush)FindResource("ErrorBrush");
                StatusText.Text = "Error: " + ex.Message;
                StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateServiceStatus();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InstallButton.IsEnabled = false;
                InstallButton.Content = "Installing...";

                bool success = _serviceInstaller.InstallService();

                if (success)
                {
                    ShowNotification("Service Installed", "MazSvc has been installed and started successfully.");
                    UpdateServiceStatus();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to install the service. Make sure you are running as Administrator.",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error installing service:\n" + ex.Message,
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                InstallButton.Content = "Install Service";
                UpdateServiceStatus();
            }
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBoxResult result = MessageBox.Show(
                    "Are you sure you want to uninstall the MazSvc service?",
                    "Confirm Uninstall",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                UninstallButton.IsEnabled = false;
                UninstallButton.Content = "Uninstalling...";

                bool success = _serviceInstaller.UninstallService();

                if (success)
                {
                    ShowNotification("Service Uninstalled", "MazSvc has been uninstalled successfully.");
                    UpdateServiceStatus();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to uninstall the service. Make sure you are running as Administrator.",
                        "Uninstall Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error uninstalling service:\n" + ex.Message,
                    "Uninstall Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UninstallButton.Content = "Uninstall Service";
                UpdateServiceStatus();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartButton.IsEnabled = false;
                StartButton.Content = "Starting...";

                bool success = _serviceController.StartService();

                if (success)
                {
                    ShowNotification("Service Started", "MazSvc has been started successfully.");
                }
                else
                {
                    MessageBox.Show(
                        "Failed to start the service.",
                        "Start Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error starting service:\n" + ex.Message,
                    "Start Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                StartButton.Content = "Start Service";
                UpdateServiceStatus();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopButton.IsEnabled = false;
                StopButton.Content = "Stopping...";

                bool success = _serviceController.StopService();

                if (success)
                {
                    ShowNotification("Service Stopped", "MazSvc has been stopped.");
                }
                else
                {
                    MessageBox.Show(
                        "Failed to stop the service.",
                        "Stop Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error stopping service:\n" + ex.Message,
                    "Stop Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                StopButton.Content = "Stop Service";
                UpdateServiceStatus();
            }
        }

        private void ManageExeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string serviceDirectory = _serviceInstaller.GetServiceDirectory();
                
                if (string.IsNullOrEmpty(serviceDirectory))
                {
                    MessageBox.Show(
                        "Could not find service installation directory.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ExecutableListWindow exeWindow = new ExecutableListWindow(serviceDirectory);
                exeWindow.Owner = this;
                exeWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error opening executable manager:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CopyFingerprintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(FingerprintText.Text);
                ShowNotification("Copied", "Hardware fingerprint copied to clipboard.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error copying to clipboard:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_statusTimer != null)
            {
                _statusTimer.Stop();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Dispose();
            }
        }
    }
}
