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

        #region Protect via Spy

        private void SelectTargetExeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                TargetExePathTextBox.Text = dialog.FileName;
            }
        }

        private string ExtractEmbeddedSpy()
        {
            string tempSpyPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "philips_protector_spy_temp.exe");
            
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                // Try different possible resource name formats
                string[] possibleNames = new string[]
                {
                    "philips_protector.Assets.philips-protector-spy.exe",
                    "philips_protector.Assets.philips_protector_spy.exe",
                    "Assets.philips-protector-spy.exe",
                    "Assets.philips_protector_spy.exe"
                };
                
                Stream resourceStream = null;
                string foundResourceName = null;
                
                foreach (string resourceName in possibleNames)
                {
                    resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream != null)
                    {
                        foundResourceName = resourceName;
                        break;
                    }
                }
                
                // If not found, try to find by listing all resources
                if (resourceStream == null)
                {
                    string[] allResources = assembly.GetManifestResourceNames();
                    foreach (string resourceName in allResources)
                    {
                        if (resourceName.Contains("spy") && resourceName.EndsWith(".exe"))
                        {
                            resourceStream = assembly.GetManifestResourceStream(resourceName);
                            if (resourceStream != null)
                            {
                                foundResourceName = resourceName;
                                break;
                            }
                        }
                    }
                }
                
                if (resourceStream == null)
                {
                    string allResourcesList = string.Join(", ", assembly.GetManifestResourceNames());
                    throw new InvalidOperationException("Embedded spy file not found. Available resources: " + allResourcesList);
                }
                
                using (resourceStream)
                {
                    using (FileStream fileStream = new FileStream(tempSpyPath, FileMode.Create))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
                
                return tempSpyPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to extract embedded spy file: " + ex.Message, ex);
            }
        }

        private void ProtectViaSpyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProtectStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                ProtectStatusTextBlock.Text = "Starting protection process...";

                // Validate target executable
                string targetExePath = TargetExePathTextBox.Text;
                if (string.IsNullOrWhiteSpace(targetExePath) || !File.Exists(targetExePath))
                {
                    ProtectStatusTextBlock.Text = "Please select a valid target executable.";
                    ProtectStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    return;
                }

                // Get target directory
                string targetDirectory = System.IO.Path.GetDirectoryName(targetExePath);
                string targetFileName = System.IO.Path.GetFileName(targetExePath);
                string targetFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(targetExePath);
                string targetExtension = System.IO.Path.GetExtension(targetExePath);

                // Step 1: Extract embedded spy to temp location
                ProtectStatusTextBlock.Text = "Extracting embedded spy file...";
                string tempSpyPath = ExtractEmbeddedSpy();

                // Step 2: Extract target exe's icon group
                ProtectStatusTextBlock.Text = "Extracting target executable icon group...";
                string tempIconPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "philips_protector_target_icon.ico");
                bool iconExtracted = false;
                
                try
                {
                    IconExtractor.IconExtractionResult iconResult = IconExtractor.ExtractIconGroup(targetExePath);
                    File.WriteAllBytes(tempIconPath, iconResult.FullIcoBytes);
                    iconExtracted = true;
                }
                catch (Exception iconEx)
                {
                    // Icon extraction failed, continue without icon replacement
                    ProtectStatusTextBlock.Text = string.Format("Warning: Could not extract icon from target ({0}). Continuing without icon replacement...", iconEx.Message);
                }

                // Step 3: Replace spy's icon if extraction succeeded
                if (iconExtracted)
                {
                    ProtectStatusTextBlock.Text = "Replacing spy icon with target icon...";
                    try
                    {
                        IconReplacer.ReplaceIcon(tempSpyPath, tempIconPath);
                    }
                    catch (Exception iconReplaceEx)
                    {
                        // Icon replacement failed, continue anyway
                        ProtectStatusTextBlock.Text = string.Format("Warning: Could not replace spy icon ({0}). Continuing...", iconReplaceEx.Message);
                    }
                }

                // Step 4: Copy spy to target directory
                ProtectStatusTextBlock.Text = "Copying spy to target directory...";
                string spyInTargetDir = System.IO.Path.Combine(targetDirectory, "philips-protector-spy.exe");
                File.Copy(tempSpyPath, spyInTargetDir, true);

                // Step 5: Rename target exe to modsvc.sys
                ProtectStatusTextBlock.Text = "Renaming target executable to modsvc.sys...";
                string modsvcPath = System.IO.Path.Combine(targetDirectory, "modsvc.sys");
                if (File.Exists(modsvcPath))
                {
                    File.Delete(modsvcPath);
                }
                File.Move(targetExePath, modsvcPath);

                // Step 6: Rename spy to original target exe filename
                ProtectStatusTextBlock.Text = "Renaming spy to original target filename...";
                string finalSpyPath = System.IO.Path.Combine(targetDirectory, targetFileName);
                if (File.Exists(finalSpyPath))
                {
                    File.Delete(finalSpyPath);
                }
                File.Move(spyInTargetDir, finalSpyPath);

                // Step 7: Generate and save license file
                ProtectStatusTextBlock.Text = "Generating and saving license file...";
                string licenseKey = LicenseGenerator.GenerateLicenseKey(_currentHardwareInfo);
                string licensePath = System.IO.Path.Combine(targetDirectory, "mazlicense.lic");
                File.WriteAllText(licensePath, licenseKey, Encoding.UTF8);

                // Cleanup temp files
                try
                {
                    if (File.Exists(tempSpyPath)) File.Delete(tempSpyPath);
                    if (File.Exists(tempIconPath)) File.Delete(tempIconPath);
                }
                catch { }

                ProtectStatusTextBlock.Text = string.Format("Protection completed successfully!\n\nTarget: {0}\nSpy: {1}\nLicense: {2}", 
                    modsvcPath, finalSpyPath, licensePath);
                ProtectStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            catch (Exception ex)
            {
                ProtectStatusTextBlock.Text = string.Format("Error during protection: {0}", ex.Message);
                ProtectStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            }
        }

        #endregion
    }
}