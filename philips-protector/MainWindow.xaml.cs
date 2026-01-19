using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using philips_protector.Models;

namespace philips_protector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HardwareInfo? _currentHardwareInfo;

        public MainWindow()
        {
            InitializeComponent();
            LoadHardwareInfo();
        }

        private void LoadHardwareInfo()
        {
            try
            {
                // Get hardware information
                _currentHardwareInfo = HardwareFingerprint.GetHardwareInfo();

                // Display fingerprint
                string fingerprint = LicenseValidator.GetCurrentFingerprint();
                FingerprintTextBox.Text = fingerprint;

                // Display hardware details
                DisplayHardwareDetails(_currentHardwareInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading hardware information: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayHardwareDetails(HardwareInfo hardwareInfo)
        {
            HardwareDetailsPanel.Children.Clear();

            AddDetailRow("CPU ID:", hardwareInfo.CpuId);
            AddDetailRow("Motherboard Serial:", hardwareInfo.MotherboardSerial);
            AddDetailRow("Hard Drive Serial:", hardwareInfo.HardDriveSerial);
            AddDetailRow("MAC Address:", hardwareInfo.MacAddress);
            AddDetailRow("BIOS Serial:", hardwareInfo.BiosSerial);
            AddDetailRow("Machine GUID:", hardwareInfo.MachineGuid);
        }

        private void AddDetailRow(string label, string value)
        {
            var stackPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Margin = new Thickness(0, 5, 0, 5) 
            };

            var labelText = new TextBlock 
            { 
                Text = label, 
                FontWeight = FontWeights.SemiBold,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center
            };

            var valueText = new TextBlock 
            { 
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(labelText);
            stackPanel.Children.Add(valueText);
            HardwareDetailsPanel.Children.Add(stackPanel);
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateLicense();
        }

        private void LicenseKeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ValidateLicense();
            }
        }

        private void ValidateLicense()
        {
            string licenseKey = LicenseKeyTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                ShowStatus("Please enter a license key.", false);
                return;
            }

            try
            {
                bool isValid = LicenseValidator.ValidateLicenseKey(licenseKey);

                if (isValid)
                {
                    ShowStatus("✓ License Key is VALID - System authorized!", true);
                }
                else
                {
                    ShowStatus("✗ License Key is INVALID - Does not match current hardware.", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error validating license: {ex.Message}", false);
            }
        }

        private void ShowStatus(string message, bool isValid)
        {
            StatusTextBlock.Text = message;
            StatusBorder.Visibility = Visibility.Visible;

            if (isValid)
            {
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Light green
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Dark green
            }
            else
            {
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Light red
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28)); // Dark red
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LicenseKeyTextBox.Clear();
            StatusBorder.Visibility = Visibility.Collapsed;
            LicenseKeyTextBox.Focus();
        }

        private void CopyFingerprintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(FingerprintTextBox.Text);
                MessageBox.Show("Hardware fingerprint copied to clipboard!", 
                    "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying to clipboard: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLicenseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FingerprintTextBox.Text))
                {
                    MessageBox.Show("No license key to save. Please ensure hardware fingerprint is loaded.", 
                        "No License Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "License Key Files (*.lic)|*.lic|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Save License Key",
                    FileName = "license_key.lic",
                    DefaultExt = "lic"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string licenseKey = FingerprintTextBox.Text;
                    System.IO.File.WriteAllText(saveFileDialog.FileName, licenseKey, Encoding.UTF8);
                    
                    MessageBox.Show($"License key saved successfully!\n\nLocation: {saveFileDialog.FileName}", 
                        "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving license key: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}