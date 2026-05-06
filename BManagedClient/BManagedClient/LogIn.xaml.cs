using BManagedClient.BMsrv;
using System;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class LogIn : Page
    {
        public static Sign sign = new Sign();

        public LogIn()
        {
            InitializeComponent();
            DataContext = sign;
        }

        private void signIn_Click(object sender, RoutedEventArgs e)
        {
            error.Visibility = Visibility.Collapsed;
            string u = username.Text?.Trim() ?? "";
            string p = pass.Password ?? "";
            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            {
                ShowError("Fill both fields.");
                return;
            }
            try
            {
                bool ok = ServiceGateway.Use(c => c.CheckUserPassword(u, p));
                if (!ok) { ShowError("Wrong username or password."); return; }

                var user = ServiceGateway.Use(c => c.GetUserById(c.GetUserId(u)));
                sign.Username          = user.Username;
                sign.Password          = p;
                sign.Email             = user.Email;
                sign.Phone             = user.Phone;
                sign.Id                = user.Id;
                sign.Role              = user.Role;
                sign.PreferredCurrency = user.PreferredCurrency;
                sign.IsActive          = user.IsActive;

                // If they signed in with the manager-issued temp password,
                // jump straight to Settings so they can pick their own.
                bool tempPassword = string.Equals(p, "reset1234", StringComparison.Ordinal);
                if (tempPassword)
                {
                    MessageBox.Show(
                        "You're signed in with the temporary password 'reset1234'.\n" +
                        "Please choose a new password in the Settings page.",
                        "Change your password",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                page.Visibility = Visibility.Visible;
                if (tempPassword)         page.Navigate(new Settings());
                else if (sign.IsOwner)    page.Navigate(new OwnerHome());
                else if (sign.IsEmployee) page.Navigate(new EmployeeHome());
                else                      page.Navigate(new ClientHome());
            }
            catch (Exception ex)
            {
                ShowError("Connection error: " + ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            error.Text = msg;
            error.Visibility = Visibility.Visible;
        }

        private void signUp_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new SignUp());

        private void forgot_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new ForgotPassword());
    }
}
