using System;
using System.Windows;
using System.Windows.Input;
using MazManager.Helpers;

namespace MazManager.Views
{
    /// <summary>
    /// Interaction logic for PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : Window
    {
        public PasswordWindow()
        {
            InitializeComponent();
            UpdateAttemptsDisplay();
            
            // Enable rounded corners (Windows XP compatible)
            WindowHelper.EnableRoundedCorners(this, 10);
            
            // Focus password input after window loads
            this.Loaded += (s, e) => PasswordInput.Focus();
        }

        #region Custom Title Bar Events

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        private void UpdateAttemptsDisplay()
        {
            int remaining = PasswordManager.GetRemainingAttempts();
            AttemptsText.Text = string.Format("Remaining attempts: {0}", remaining);
        }

        private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordCheckBox.IsChecked == true)
            {
                PasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.Focus();
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
            }
            else
            {
                PasswordInput.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordInput.Focus();
            }
        }

        private void ValidatePassword()
        {
            string password = ShowPasswordCheckBox.IsChecked == true
                ? PasswordTextBox.Text
                : PasswordInput.Password;

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter a password.");
                return;
            }

            if (PasswordManager.ValidatePassword(password))
            {
                // Reset attempts on success
                PasswordManager.ResetPasswordLimit();

                // Handle remember me
                if (RememberMeCheckBox.IsChecked == true)
                {
                    PasswordManager.SetRememberMe(true);
                }
                else
                {
                    PasswordManager.SetRememberMe(false);
                }

                DialogResult = true;
                Close();
            }
            else
            {
                // Decrement attempts
                PasswordManager.DecrementPasswordLimit();
                
                int remaining = PasswordManager.GetRemainingAttempts();
                
                if (remaining <= 0)
                {
                    // Mark as invalid and potentially delete
                    PasswordManager.MarkAsInvalid();
                    PasswordManager.DeleteSelf();

                    MessageBox.Show(
                        "Too many failed attempts. The application will now close.",
                        "Access Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    DialogResult = false;
                    Close();
                    return;
                }

                ShowError(string.Format("Invalid password. {0} attempt(s) remaining.", remaining));
                UpdateAttemptsDisplay();

                // Clear password fields
                PasswordInput.Password = string.Empty;
                PasswordTextBox.Text = string.Empty;

                if (PasswordInput.Visibility == Visibility.Visible)
                {
                    PasswordInput.Focus();
                }
                else
                {
                    PasswordTextBox.Focus();
                }
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ValidatePassword();
            }
        }

        private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ValidatePassword();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
