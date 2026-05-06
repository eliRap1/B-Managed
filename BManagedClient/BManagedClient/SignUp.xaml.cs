using BManagedClient.BMsrv;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class SignUp : Page
    {
        private static readonly Regex EmailRx = new Regex(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$");
        private static readonly Regex PhoneRx = new Regex(@"^\+?\d{7,15}$");

        public SignUp() => InitializeComponent();

        private void Create_Click(object s, RoutedEventArgs e)
        {
            string u = usernameBox.Text?.Trim() ?? "";
            string p = passwordBox.Password ?? "";
            string em = emailBox.Text?.Trim() ?? "";
            string ph = phoneBox.Text?.Trim() ?? "";
            string cur = (curBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";

            if (u.Length < 4 || p.Length < 4) { status.Text = "Username + password must be 4+ chars."; return; }
            if (!EmailRx.IsMatch(em)) { status.Text = "Email looks invalid."; return; }
            if (!PhoneRx.IsMatch(ph)) { status.Text = "Phone looks invalid."; return; }

            try
            {
                bool exists = ServiceGateway.Use(c => c.CheckUserExist(u));
                if (exists) { status.Text = "Username already taken."; return; }

                bool ok = ServiceGateway.Use(c => c.AddUser(u, p, em, ph, "Client", cur));
                if (!ok) { status.Text = "Server rejected the request."; return; }

                MessageBox.Show("Account created. Owner will confirm soon.\nYou can now sign in.",
                    "Welcome", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new LogIn());
            }
            catch (Exception ex) { status.Text = "Error: " + ex.Message; }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new LogIn());
    }
}
