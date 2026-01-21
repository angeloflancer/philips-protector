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
                    // Skip password dialog, show main window directly
                    MainWindow mainWindow = new MainWindow();
                    this.MainWindow = mainWindow;
                    mainWindow.Show();
                    return;
                }

                // Show password window (as a regular window, not dialog)
                PasswordWindow passwordWindow = new PasswordWindow();
                this.MainWindow = passwordWindow;
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                passwordWindow.Show();
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
    }
}
