using BManagedClient.BMsrv;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BManagedClient
{
    // =========================================================================
    // LogIn page — entry point of the WPF client.
    // -------------------------------------------------------------------------
    // Flow:
    //   1. User types username + password and presses 'Sign in'.
    //   2. signIn_Click runs ASYNC (May 2026 fix) so the WCF call doesn't
    //      freeze the UI thread — pre-fix Windows showed "(Not Responding)"
    //      while the cold WCF channel + PBKDF2 verify (10,000 iterations)
    //      ran on the message-pump thread.
    //   3. Result is cached in the static `sign` (a Sign DTO) so any other
    //      WPF page can read role / id / business type without another round-
    //      trip. ClientSession is the read-only facade over this.
    //   4. Role + tempPassword decide which Page we navigate to next.
    //
    // Connections:
    //   - server-side  : Service1.CheckUserPassword + GetUserById + GetUserId
    //                    (all routed through ServiceGateway.Use → shared channel).
    //   - downstream WPF: Sign DTO (Sign.cs) — populated here, read everywhere.
    //                    page.Navigate goes to OwnerHome / EmployeeHome /
    //                    ClientHome / Settings depending on context.
    // =========================================================================
    public partial class LogIn : Page
    {
        /// <summary>
        /// Single instance shared across all WPF pages. Holds the currently
        /// signed-in user (id, role, business type, IsZair, currency).
        /// Mutated only by signIn_Click after a successful auth.
        /// </summary>
        public static Sign sign = new Sign();

        public LogIn()
        {
            InitializeComponent();
            // Bind sign so XAML two-way bindings (e.g. ProfileBadge) update
            // when fields are written below.
            DataContext = sign;
        }

        /// <summary>
        /// Async event handler — the `async void` is the documented exception
        /// to "no async void" for WPF UI events (no caller can await it).
        /// </summary>
        private async void signIn_Click(object sender, RoutedEventArgs e)
        {
            // Hide the previous error (if any) — fresh attempt, fresh state.
            error.Visibility = Visibility.Collapsed;

            // Trim username so trailing spaces don't break server-side lookups.
            string u = username.Text?.Trim() ?? "";
            string p = pass.Password ?? "";

            // Cheap client-side guard before bothering the server.
            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            {
                ShowError("Fill both fields.");
                return;
            }

            // Disable inputs + show busy cursor so the user knows we're working
            // and so a double-click doesn't queue two SOAP calls.
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;
            username.IsEnabled = false;
            pass.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                User user = null;
                try
                {
                    // Off-load WCF + PBKDF2 to a worker thread so the UI thread
                    // keeps pumping (no "Not Responding"). Two SOAP ops are
                    // batched into one ServiceGateway.Use() so the shared
                    // channel only opens once.
                    user = await Task.Run(() => ServiceGateway.Use(c =>
                        c.CheckUserPassword(u, p) ? c.GetUserById(c.GetUserId(u)) : null));
                }
                catch (Exception ex)
                {
                    // Channel-open / network failure — report and exit.
                    ShowError("Connection error: " + ex.Message);
                    return;
                }

                // Same generic message for "wrong password" and "no such user"
                // so attackers can't enumerate usernames from the response.
                if (user == null) { ShowError("Wrong username or password."); return; }

                // Persist the user record into the static `sign`. From here
                // on, any WPF page can read these without another SOAP call.
                sign.Username          = user.Username;
                // NOTE: plaintext password is NOT persisted in memory.
                // The previous `sign.Password = p` assignment was removed because
                // keeping credentials in a public static field exposes them to
                // crash dumps, memory scans, and any future reflection-based code.
                // No active re-auth flow in this app required it.
                sign.Email             = user.Email;
                sign.Phone             = user.Phone;
                sign.Id                = user.Id;
                sign.Role              = user.Role;
                sign.PreferredCurrency = user.PreferredCurrency;
                sign.IsActive          = user.IsActive;
                sign.BusinessType      = string.IsNullOrEmpty(user.BusinessType) ? "Individual" : user.BusinessType;
                sign.IsZair            = user.IsZair;

                // Manager-issued temp password ('reset1234') — force the user
                // straight to Settings to pick a new password before they
                // touch the rest of the app.
                bool tempPassword = string.Equals(p, "reset1234", StringComparison.Ordinal);
                if (tempPassword)
                {
                    MessageBox.Show(
                        "You're signed in with the temporary password 'reset1234'.\n" +
                        "Please choose a new password in the Settings page.",
                        "Change your password",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Reveal the inner Frame and navigate to the role-specific home.
                page.Visibility = Visibility.Visible;
                if (tempPassword)         page.Navigate(new Settings());
                else if (sign.IsOwner)    page.Navigate(new OwnerHome());
                else if (sign.IsEmployee) page.Navigate(new EmployeeHome());
                else                      page.Navigate(new ClientHome());
            }
            finally
            {
                // Always restore the UI state — even if we returned early.
                Mouse.OverrideCursor = null;
                if (btn != null) btn.IsEnabled = true;
                username.IsEnabled = true;
                pass.IsEnabled = true;
            }
        }

        /// <summary>Show the inline error label under the form.</summary>
        private void ShowError(string msg)
        {
            error.Text = msg;
            error.Visibility = Visibility.Visible;
        }

        /// <summary>Navigate to the SignUp page (two-step Owner/Employee chooser).</summary>
        private void signUp_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new SignUp());

        /// <summary>Navigate to ForgotPassword — sends a notification to the user's Owner.</summary>
        private void forgot_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new ForgotPassword());
    }
}
