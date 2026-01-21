using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MazManager.Models;
using MazManager.Services;
using MazManager.Helpers;
using Microsoft.Win32;

namespace MazManager.Views
{
    /// <summary>
    /// Interaction logic for ExecutableListWindow.xaml
    /// </summary>
    public partial class ExecutableListWindow : Window
    {
        private string _serviceDirectory;
        private ConfigEncryptor _configEncryptor;
        private ObservableCollection<ProtectedExecutable> _executables;
        private bool _hasChanges;
        private DispatcherTimer _statusTimer;

        public ExecutableListWindow(string serviceDirectory)
        {
            InitializeComponent();
            
            // Enable rounded corners (Windows XP compatible)
            WindowHelper.EnableRoundedCorners(this, 10);

            _serviceDirectory = serviceDirectory;
            _configEncryptor = new ConfigEncryptor();
            _executables = new ObservableCollection<ProtectedExecutable>();
            _hasChanges = false;

            ExecutableList.ItemsSource = _executables;

            LoadExecutables();
            
            // Start status monitoring timer
            StartStatusMonitoring();
        }

        #region Custom Title Bar Events

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                this.DragMove();
            }
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            HandleClose();
        }

        #endregion

        #region Status Monitoring

        private void StartStatusMonitoring()
        {
            // Update status immediately
            UpdateAllStatuses();

            // Start timer for periodic updates
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.Tick += (s, e) => UpdateAllStatuses();
            _statusTimer.Start();
        }

        private void UpdateAllStatuses()
        {
            foreach (ProtectedExecutable exe in _executables)
            {
                UpdateExecutableStatus(exe);
            }
        }

        private void UpdateExecutableStatus(ProtectedExecutable exe)
        {
            try
            {
                // Check if directory is encrypted (Critical status)
                string directory = Path.GetDirectoryName(exe.FullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    string encryptedMarker = Path.Combine(directory, ".encrypted");
                    if (File.Exists(encryptedMarker))
                    {
                        exe.Status = ExecutableStatus.Critical;
                        return;
                    }
                }

                // Check if process is running (Running status)
                string processName = Path.GetFileNameWithoutExtension(exe.Name);
                if (!string.IsNullOrEmpty(processName))
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    
                    if (processes.Length > 0)
                    {
                        // Check if any of the running processes match the full path
                        bool foundMatch = false;
                        foreach (Process process in processes)
                        {
                            try
                            {
                                // Try to get the process path
                                string processPath = GetProcessPath(process);
                                if (!string.IsNullOrEmpty(processPath) && 
                                    string.Equals(processPath, exe.FullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    foundMatch = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // Can't access process, might be running elevated
                                // Consider it a match if the name matches
                                foundMatch = true;
                                break;
                            }
                        }

                        if (foundMatch)
                        {
                            exe.Status = ExecutableStatus.Running;
                            return;
                        }
                    }
                }

                // Default to Normal
                exe.Status = ExecutableStatus.Normal;
            }
            catch
            {
                // On error, default to Normal
                exe.Status = ExecutableStatus.Normal;
            }
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                // Try MainModule first
                if (process.MainModule != null)
                {
                    return process.MainModule.FileName;
                }
            }
            catch
            {
                // MainModule may fail for elevated processes
            }

            // Try WMI as fallback
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    string.Format("SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {0}", process.Id)))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        object path = obj["ExecutablePath"];
                        if (path != null)
                        {
                            return path.ToString();
                        }
                    }
                }
            }
            catch
            {
                // WMI failed
            }

            return null;
        }

        private void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAllStatuses();
            UpdateStatus("Status refreshed.");
        }

        #endregion

        private void LoadExecutables()
        {
            try
            {
                List<ProtectedExecutable> loaded = _configEncryptor.LoadConfig(_serviceDirectory);
                
                _executables.Clear();
                foreach (ProtectedExecutable exe in loaded)
                {
                    _executables.Add(exe);
                }

                UpdateStatus(string.Format("Loaded {0} executable(s)", _executables.Count));
            }
            catch (Exception ex)
            {
                UpdateStatus("Error loading: " + ex.Message);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                openFileDialog.Title = "Select Executable to Protect";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        // Check if already exists
                        bool exists = false;
                        foreach (ProtectedExecutable existing in _executables)
                        {
                            if (string.Equals(existing.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            ProtectedExecutable newExe = new ProtectedExecutable();
                            newExe.Name = Path.GetFileName(filePath);
                            newExe.FullPath = filePath;
                            newExe.Status = ExecutableStatus.Normal;
                            _executables.Add(newExe);
                            _hasChanges = true;
                            
                            // Update status for the new executable
                            UpdateExecutableStatus(newExe);
                        }
                    }

                    UpdateStatus("Added executable(s). Don't forget to save!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error adding executable:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ExecutableList.SelectedItems.Count == 0)
                {
                    MessageBox.Show(
                        "Please select an executable to remove.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Collect items to remove
                List<ProtectedExecutable> toRemove = new List<ProtectedExecutable>();
                foreach (object item in ExecutableList.SelectedItems)
                {
                    toRemove.Add((ProtectedExecutable)item);
                }

                // Remove them
                foreach (ProtectedExecutable exe in toRemove)
                {
                    _executables.Remove(exe);
                }

                _hasChanges = true;
                UpdateStatus("Removed executable(s). Don't forget to save!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error removing executable:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ProtectedExecutable> listToSave = new List<ProtectedExecutable>(_executables);
                _configEncryptor.SaveConfig(listToSave, _serviceDirectory);

                _hasChanges = false;
                UpdateStatus("Configuration saved successfully.");

                MessageBox.Show(
                    "Configuration saved successfully.\n\nNote: The service will use the new configuration on its next check cycle.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error saving configuration:\n" + ex.Message,
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HandleClose();
        }

        private void HandleClose()
        {
            // Stop the timer
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer = null;
            }

            if (_hasChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveButton_Click(null, null);
                    Close();
                }
                else if (result == MessageBoxResult.No)
                {
                    Close();
                }
                // Cancel - do nothing
            }
            else
            {
                Close();
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Stop the timer when closing
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer = null;
            }

            base.OnClosing(e);
        }
    }
}
