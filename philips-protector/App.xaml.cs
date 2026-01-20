using System.Configuration;
using System.Data;
using System.Windows;

namespace philips_protector
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Step 1: Check if this copy is marked as invalid
            PasswordManager.CheckInvalidCopy();
            // If invalid, CheckInvalidCopy() will delete self and exit, so we won't reach here

            // Step 2: Check if "Remember me" is active
            if (PasswordManager.IsRemembered())
            {
                // User is remembered, skip password dialog and show MainWindow
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                // User is not remembered, show password dialog
                PasswordWindow passwordWindow = new PasswordWindow();
                bool? result = passwordWindow.ShowDialog();

                if (result == true)
                {
                    // Password was correct, show MainWindow
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                else
                {
                    // User cancelled or closed the dialog, exit application
                    Shutdown();
                }
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            // Prevent automatic shutdown on session end
            e.Cancel = false;
            base.OnSessionEnding(e);
        }
    }

}
