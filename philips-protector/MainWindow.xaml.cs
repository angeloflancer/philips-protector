using System;
using System.IO;
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
        private HardwareInfo _currentHardwareInfo;
        private string _tempIconPath;
        private byte[] _lastExtractedIconBytes;

        public MainWindow()
        {
            InitializeComponent();
            _tempIconPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "philips_protector_temp_icon.ico");
            InitializeTempIconState();
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
                MessageBox.Show(string.Format("Error loading hardware information: {0}", ex.Message),
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
                ShowStatus(string.Format("Error validating license: {0}", ex.Message), false);
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
                MessageBox.Show(string.Format("Error copying to clipboard: {0}", ex.Message),
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

                    MessageBox.Show(string.Format("License key saved successfully!\n\nLocation: {0}", saveFileDialog.FileName),
                        "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error saving license key: {0}", ex.Message),
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Icon extraction / replacement

        private void InitializeTempIconState()
        {
            bool exists = File.Exists(_tempIconPath);
            UseTempIconCheckBox.IsEnabled = exists;
            UseTempIconCheckBox.IsChecked = exists;
            if (exists)
            {
                IconExtractStatusTextBlock.Text = "Temp icon available.";
            }
        }

        private void SelectExtractExeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                ExtractExePathTextBox.Text = dialog.FileName;
            }
        }

        private void ExtractIconButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ExtractExePathTextBox.Text) || !File.Exists(ExtractExePathTextBox.Text))
                {
                    IconExtractStatusTextBlock.Text = "Please select a valid executable.";
                    IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                IconExtractor.IconExtractionResult result = IconExtractor.ExtractIconGroup(ExtractExePathTextBox.Text);
                _lastExtractedIconBytes = result.FullIcoBytes;
                byte[] previewBytes = result.PreviewIcoBytes ?? result.FullIcoBytes;
                SetIconPreview(previewBytes);

                IconExtractStatusTextBlock.Text = "Icon group extracted from executable.";
                IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            catch (Exception ex)
            {
                IconExtractStatusTextBlock.Text = string.Format("Error extracting icon: {0}", ex.Message);
                IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            }
        }

        private void SetIconPreview(byte[] iconBytes)
        {
            if (iconBytes == null)
            {
                IconPreviewImage.Source = null;
                return;
            }

            using (var ms = new MemoryStream(iconBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                IconPreviewImage.Source = bitmap;
            }
        }

        private void SaveIconToFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastExtractedIconBytes == null || _lastExtractedIconBytes.Length == 0)
                {
                    IconExtractStatusTextBlock.Text = "No icon to save. Extract first.";
                    IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                var dialog = new SaveFileDialog();
                dialog.Filter = "Icon Files (*.ico)|*.ico|All Files (*.*)|*.*";
                dialog.FileName = "extracted_icon.ico";
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(dialog.FileName, _lastExtractedIconBytes);
                    IconExtractStatusTextBlock.Text = string.Format("Icon saved to: {0}", dialog.FileName);
                    IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                }
            }
            catch (Exception ex)
            {
                IconExtractStatusTextBlock.Text = string.Format("Error saving icon: {0}", ex.Message);
                IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            }
        }

        private void SaveIconToTempButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastExtractedIconBytes == null || _lastExtractedIconBytes.Length == 0)
                {
                    IconExtractStatusTextBlock.Text = "No icon to save. Extract first.";
                    IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                File.WriteAllBytes(_tempIconPath, _lastExtractedIconBytes);
                UseTempIconCheckBox.IsEnabled = true;
                UseTempIconCheckBox.IsChecked = true;
                IconExtractStatusTextBlock.Text = string.Format("Icon saved to temp: {0}", _tempIconPath);
                IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            catch (Exception ex)
            {
                IconExtractStatusTextBlock.Text = string.Format("Error saving temp icon: {0}", ex.Message);
                IconExtractStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            }
        }

        private void SelectReplaceExeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                ReplaceExePathTextBox.Text = dialog.FileName;
            }
        }

        private void SelectIconFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Icon/Images/Executables (*.ico;*.png;*.bmp;*.jpg;*.jpeg;*.exe)|*.ico;*.png;*.bmp;*.jpg;*.jpeg;*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                ReplaceIconPathTextBox.Text = dialog.FileName;
            }
        }

        private void UseTempIconCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (UseTempIconCheckBox.IsChecked == true)
            {
                ReplaceIconPathTextBox.IsEnabled = false;
                SelectIconFileButton.IsEnabled = false;
            }
        }

        private void UseTempIconCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ReplaceIconPathTextBox.IsEnabled = true;
            SelectIconFileButton.IsEnabled = true;
        }

        private void ReplaceIconButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetExe = ReplaceExePathTextBox.Text;
                if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
                {
                    IconReplaceStatusTextBlock.Text = "Please select a valid target executable.";
                    IconReplaceStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                string iconPath = ReplaceIconPathTextBox.Text;
                bool useTemp = UseTempIconCheckBox.IsChecked == true;
                if (useTemp)
                {
                    iconPath = _tempIconPath;
                }

                if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
                {
                    IconReplaceStatusTextBlock.Text = "Please select an icon file (or save to temp first).";
                    IconReplaceStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                string icoPathToUse = iconPath;
                string extension = System.IO.Path.GetExtension(iconPath).ToLowerInvariant();
                if (extension == ".exe")
                {
                    IconExtractor.IconExtractionResult extracted = IconExtractor.ExtractIconGroup(iconPath);
                    icoPathToUse = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "philips_protector_exe_icon.ico");
                    File.WriteAllBytes(icoPathToUse, extracted.FullIcoBytes);
                }
                else if (extension != ".ico")
                {
                    icoPathToUse = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "philips_protector_converted_icon.ico");
                    ImageConverter.ConvertToIco(iconPath, icoPathToUse, new int[] { 16, 32, 48, 256 });
                }

                IconReplacer.ReplaceIcon(targetExe, icoPathToUse);
                IconReplaceStatusTextBlock.Text = "Icon replaced successfully.";
                IconReplaceStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            catch (Exception ex)
            {
                IconReplaceStatusTextBlock.Text = string.Format("Error replacing icon: {0}", ex.Message);
                IconReplaceStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            }
        }

        #endregion
    }
}