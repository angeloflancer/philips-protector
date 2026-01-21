using System;
using System.Windows;
using MazManager.Views;

namespace MazManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Check for invalid copy first
                if (PasswordManager.CheckInvalidCopy())
                {
                    // Copy was invalid and deleted itself
                    Shutdown();
                    return;
                }

                // Check if password is remembered
                if (PasswordManager.IsRemembered())
                {
                    // Skip password dialog, show main window
                    ShowMainWindow();
                    return;
                }

                // Show password dialog
                PasswordWindow passwordWindow = new PasswordWindow();
                bool? result = passwordWindow.ShowDialog();

                if (result == true)
                {
                    // Password validated successfully
                    ShowMainWindow();
                }
                else
                {
                    // Password dialog cancelled or failed
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred during startup:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ShowMainWindow()
        {
            MainWindow mainWindow = new MainWindow();
            
            // Set as the main window and switch shutdown mode
            this.MainWindow = mainWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            mainWindow.Show();
        }
    }
}
