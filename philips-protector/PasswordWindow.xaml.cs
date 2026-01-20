using System;
using System.Windows;
using System.Windows.Input;

namespace philips_protector
{
    /// <summary>
    /// Interaction logic for PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : Window
    {
        private bool _isUpdatingPassword = false;

        public PasswordWindow()
        {
            InitializeComponent();
            PasswordBox.Focus();
            UpdateRemainingAttempts();
        }

        private void UpdateRemainingAttempts()
        {
            int remaining = PasswordManager.GetRemainingAttempts();
            if (remaining < 3)
            {
                ErrorMessageTextBlock.Text = string.Format("Remaining attempts: {0}", remaining);
                ErrorMessageTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingPassword) return;

            _isUpdatingPassword = true;
            PasswordTextBox.Text = PasswordBox.Password;
            _isUpdatingPassword = false;

            // Clear error message when user starts typing
            if (ErrorMessageTextBlock.Visibility == Visibility.Visible && !string.IsNullOrEmpty(PasswordBox.Password))
            {
                ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void PasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingPassword) return;

            _isUpdatingPassword = true;
            PasswordBox.Password = PasswordTextBox.Text;
            _isUpdatingPassword = false;

            // Clear error message when user starts typing
            if (ErrorMessageTextBlock.Visibility == Visibility.Visible && !string.IsNullOrEmpty(PasswordTextBox.Text))
            {
                ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }

        private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _isUpdatingPassword = true;
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
            PasswordTextBox.Focus();
            PasswordTextBox.SelectAll();
            _isUpdatingPassword = false;
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isUpdatingPassword = true;
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordBox.Focus();
            _isUpdatingPassword = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Get password from the currently visible control
            string password = string.Empty;
            if (ShowPasswordCheckBox.IsChecked == true)
            {
                password = PasswordTextBox.Text ?? string.Empty;
            }
            else
            {
                password = PasswordBox.Password ?? string.Empty;
            }

            if (string.IsNullOrEmpty(password))
            {
                ErrorMessageTextBlock.Text = "Please enter a password.";
                ErrorMessageTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));
                ErrorMessageTextBlock.Visibility = Visibility.Visible;
                if (ShowPasswordCheckBox.IsChecked == true)
                {
                    PasswordTextBox.Focus();
                }
                else
                {
                    PasswordBox.Focus();
                }
                return;
            }

            if (PasswordManager.ValidatePassword(password))
            {
                // Password is correct - reset limit to 3
                PasswordManager.ResetPasswordLimit();

                // Save "Remember me" state if checked
                if (RememberMeCheckBox.IsChecked == true)
                {
                    PasswordManager.SetRememberMe(true);
                }
                else
                {
                    PasswordManager.SetRememberMe(false);
                }

                // Close dialog with success
                DialogResult = true;
                Close();
            }
            else
            {
                // Password is incorrect - decrement limit
                bool limitReached = PasswordManager.DecrementPasswordLimit();

                if (limitReached)
                {
                    // Limit reached 0 - mark as invalid and delete
                    PasswordManager.MarkAsInvalid();
                    PasswordManager.DeleteSelf();
                    // DeleteSelf() will exit the application, so we won't reach here
                }
                else
                {
                    // Show error with remaining attempts
                    int remaining = PasswordManager.GetRemainingAttempts();
                    ErrorMessageTextBlock.Text = string.Format("Password incorrect, limit is {0}", remaining);
                    ErrorMessageTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));
                    ErrorMessageTextBlock.Visibility = Visibility.Visible;
                    
                    // Clear both password fields to keep them in sync
                    _isUpdatingPassword = true;
                    PasswordBox.Password = string.Empty;
                    PasswordTextBox.Text = string.Empty;
                    _isUpdatingPassword = false;
                    
                    if (ShowPasswordCheckBox.IsChecked == true)
                    {
                        PasswordTextBox.Focus();
                    }
                    else
                    {
                        PasswordBox.Focus();
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
