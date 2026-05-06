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

                if (sign.IsOwner)         NavigationService?.Navigate(new OwnerHome());
                else if (sign.IsEmployee) NavigationService?.Navigate(new EmployeeHome());
                else                      NavigationService?.Navigate(new ClientHome());
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
    }
}
