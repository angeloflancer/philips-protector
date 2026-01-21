using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MazManager.Models;
using MazManager.Services;
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

        public ExecutableListWindow(string serviceDirectory)
        {
            InitializeComponent();

            _serviceDirectory = serviceDirectory;
            _configEncryptor = new ConfigEncryptor();
            _executables = new ObservableCollection<ProtectedExecutable>();
            _hasChanges = false;

            ExecutableList.ItemsSource = _executables;

            LoadExecutables();
        }

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
                            _executables.Add(newExe);
                            _hasChanges = true;
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
            if (_hasChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveButton_Click(sender, e);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            Close();
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes. Close anyway?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
    }
}
