using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class ManageUsers : Page
    {
        public ManageUsers()
        {
            InitializeComponent();
            if (!ClientSession.IsOwner) { NavigationService?.Navigate(new LogIn()); return; }
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                // Tenant-scoped: only this Owner's users (Owner row + their
                // employees / clients). Replaces GetAllUsers() which returned
                // every account on the server.
                var all = ServiceGateway.Use(c => c.GetUsersForOwner(LogIn.sign.Id));
                userList.ItemsSource = all;
                int pending = all?.Count(u => !u.IsActive) ?? 0;
                pendingBadge.Text = pending > 0 ? $"⏳ {pending} pending" : "";
            }
            catch (Exception ex) { MessageBox.Show("Load: " + ex.Message); }
        }

        private User Selected => userList.SelectedItem as User;

        private void SetRole_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) { MessageBox.Show("Select a user."); return; }
            string role = (roleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (role == null) { MessageBox.Show("Pick a role."); return; }
            try { ServiceGateway.Use(c => c.UpdateUserRole(Selected.Id, role)); Refresh(); }
            catch (Exception ex) { MessageBox.Show("Set role failed: " + ex.Message); }
        }

        private void Toggle_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) return;
            bool target = !Selected.IsActive;
            try { ServiceGateway.Use(c => c.SetUserActive(Selected.Id, target)); Refresh(); }
            catch (Exception ex) { MessageBox.Show("Toggle failed: " + ex.Message); }
        }

        private void ResetPw_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show($"Reset {Selected.Username}'s password to 'reset1234'?", "Confirm",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            // TODO(audit): "reset1234" is a predictable well-known temporary password.
            // Generate a random token instead and display it to the Owner so they can
            // securely pass it to the user. Coordinate with Users.cshtml.cs (Web) and
            // ForgotPassword.cshtml.cs.
            try { ServiceGateway.Use(c => c.ResetPassword(Selected.Id, "reset1234")); MessageBox.Show("Done."); }
            catch (Exception ex) { MessageBox.Show("Reset failed: " + ex.Message); }
        }

        private void Delete_Click(object s, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show($"Delete {Selected.Username}? This cannot be undone.", "Confirm",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { ServiceGateway.Use(c => c.DeleteUser(Selected.Id)); Refresh(); }
            catch (Exception ex) { MessageBox.Show("Delete failed: " + ex.Message); }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new OwnerHome());
    }
}
