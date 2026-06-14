using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    // =========================================================================
    // Settings page (WPF).
    // -------------------------------------------------------------------------
    // Single page used by every role; the Owner-only StackPanel
    // (`ownerSection`) is hidden for Employee + Client logins.
    // Sections:
    //   * Account (everyone)   — email, phone, preferred currency.
    //   * Company (Owner only) — business name, VAT registration
    //     (Patur/Murshe), Osek-Zair flag, invite-code display + Copy +
    //     Rotate buttons.
    //   * Security             — password change with the same regex as
    //     SignUp (8+ chars, must include letter + digit). Blocks downgrades.
    // Invite-code rotation:
    //   RotateInvite_Click prompts Yes/No before generating a new code.
    //   Old code is invalidated immediately because GetOwnerByInviteCode
    //   does an exact match — employees still in signup will get
    //   'Invite code not recognised' until they're given the new value.
    // Page is wrapped in a ScrollViewer (XAML) so the Owner section is
    // reachable below the fold on small displays.
    // =========================================================================
    public partial class Settings : Page
    {
        public Settings()
        {
            InitializeComponent();
            if (!ClientSession.IsLoggedIn) { NavigationService?.Navigate(new LogIn()); return; }
            emailBox.Text  = LogIn.sign.Email;
            phoneBox.Text  = LogIn.sign.Phone;
            curCombo.SelectedItem = curCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(x => x.Content?.ToString() == LogIn.sign.PreferredCurrency);

            if (ClientSession.IsOwner)
            {
                ownerSection.Visibility = Visibility.Visible;
                LoadOwnerSettings();
            }
        }

        private void LoadOwnerSettings()
        {
            try
            {
                var u = ServiceGateway.Use(c => c.GetUserById(LogIn.sign.Id));
                if (u == null) return;
                bizNameBox.Text = u.BusinessName ?? "";
                bizTypeBox.SelectedItem = bizTypeBox.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(x => x.Tag?.ToString() == (u.BusinessType ?? "Patur"))
                    ?? bizTypeBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
                zairCheck.IsChecked = u.IsZair;
                inviteCodeText.Text = string.IsNullOrEmpty(u.InviteCode) ? "(none yet)" : u.InviteCode;
                LogIn.sign.BusinessType = u.BusinessType ?? "Individual";
                LogIn.sign.IsZair       = u.IsZair;
            }
            catch (Exception ex) { status.Text = "Load: " + ex.Message; }
        }

        private void Save_Click(object s, RoutedEventArgs e)
        {
            try
            {
                string cur = (curCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";
                ServiceGateway.Use(c => c.UpdateUserProfile(LogIn.sign.Id, emailBox.Text, phoneBox.Text, cur));
                LogIn.sign.Email = emailBox.Text;
                LogIn.sign.Phone = phoneBox.Text;
                LogIn.sign.PreferredCurrency = cur;
                status.Text = "Saved.";
            }
            catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
        }

        private void SaveOwner_Click(object s, RoutedEventArgs e)
        {
            try
            {
                string biz  = (bizNameBox.Text ?? "").Trim();
                string type = (bizTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Patur";
                bool zair   = zairCheck.IsChecked == true;

                ServiceGateway.Use(c =>
                {
                    c.SetBusinessName(LogIn.sign.Id, biz);
                    c.SetBusinessType(LogIn.sign.Id, type);
                    c.SetIsZair(LogIn.sign.Id, zair);
                });
                LogIn.sign.BusinessType = type;
                LogIn.sign.IsZair       = zair;
                status.Text = "Company settings saved.";
            }
            catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
        }

        private void RotateInvite_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Rotate the invite code?\nThe current code will stop working immediately. Employees still in signup must use the new code.",
                    "Confirm rotate", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;
            try
            {
                string seed = string.IsNullOrWhiteSpace(bizNameBox.Text) ? LogIn.sign.Username : bizNameBox.Text;
                string code = NewInviteCode(seed);
                string newCode = ServiceGateway.Use(c => c.SetInviteCode(LogIn.sign.Id, code));
                inviteCodeText.Text = newCode ?? code;
                status.Text = "New invite code generated.";
            }
            catch (Exception ex) { status.Text = "Rotate failed: " + ex.Message; }
        }

        private void CopyInvite_Click(object s, RoutedEventArgs e)
        {
            try { Clipboard.SetText(inviteCodeText.Text ?? ""); status.Text = "Copied."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        }

        private void UpdatePass_Click(object s, RoutedEventArgs e)
        {
            // Match the SignUp regex so existing accounts can't downgrade to a
            // weak password from Settings.
            var rx = new System.Text.RegularExpressions.Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");
            if (!rx.IsMatch(newPass.Password ?? ""))
            { status.Text = "Password must be 8+ chars and include a letter and a digit."; return; }
            try
            {
                ServiceGateway.Use(c => c.ResetPassword(LogIn.sign.Id, newPass.Password));
                newPass.Clear();
                status.Text = "Password updated.";
            }
            catch (Exception ex) { status.Text = "Failed: " + ex.Message; }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(ClientSession.IsOwner ? (Page)new OwnerHome()
                                       : ClientSession.IsEmployee ? new EmployeeHome()
                                       : new ClientHome());

        // PREFIX-XXXX (4 alpha-numeric from seed + 4 random, ambiguity-free alphabet).
        private static string NewInviteCode(string seed)
        {
            string prefix = new string((seed ?? "")
                .ToUpperInvariant().Where(char.IsLetterOrDigit).Take(4).ToArray());
            if (prefix.Length < 2) prefix = "BMNG";
            const string alpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            // Use RandomNumberGenerator (CSPRNG) — System.Random is clock-seeded
            // and predictable; an attacker knowing the prefix and rotation time
            // can brute-force the 4-char suffix in ~923 k tries.
            var tail = new char[4];
            var buf  = new byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Fill(buf);
            for (int i = 0; i < 4; i++)
                tail[i] = alpha[buf[i] % alpha.Length];
            return prefix + "-" + new string(tail);
        }
    }
}
