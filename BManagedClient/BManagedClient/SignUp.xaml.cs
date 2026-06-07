using BManagedClient.BMsrv;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public partial class SignUp : Page
    {
        private static readonly Regex UsernameRx = new Regex(@"^[A-Za-z0-9_.]{4,20}$");
        private static readonly Regex EmailRx    = new Regex(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$");
        private static readonly Regex PhoneRx    = new Regex(@"^\+?\d{7,15}$");
        private static readonly Regex PasswordRx = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");

        // "Owner", "Employee", or "Client" — set when the user picks a path.
        // Empty = step 1 still.
        private string _selectedRole = "";

        // Employee + Client both join via invite code, so they share the same
        // invite-code panel and the same lookup path.
        private static bool IsInviteRole(string r) => r == "Employee" || r == "Client";

        public SignUp() => InitializeComponent();

        private void ChooseOwner_Click(object s, RoutedEventArgs e)
            => SwitchToForm("Owner");

        private void ChooseEmployee_Click(object s, RoutedEventArgs e)
            => SwitchToForm("Employee");

        private void ChooseClient_Click(object s, RoutedEventArgs e)
            => SwitchToForm("Client");

        private void SwitchToForm(string role)
        {
            _selectedRole = role;
            roleStep.Visibility = Visibility.Collapsed;
            formStep.Visibility = Visibility.Visible;
            ownerExtras.Visibility    = role == "Owner"     ? Visibility.Visible : Visibility.Collapsed;
            employeeExtras.Visibility = IsInviteRole(role)  ? Visibility.Visible : Visibility.Collapsed;
            titleText.Text =
                role == "Owner"  ? "Owner sign-up" :
                role == "Client" ? "Client sign-up" :
                                   "Employee sign-up";
            subtitleText.Text =
                role == "Owner"  ? "Run the business — you'll log invoices, expenses, and VAT." :
                role == "Client" ? "Enter the invite code your business sent you." :
                                   "Enter the invite code from your company's Owner.";
        }

        private void ResetSteps_Click(object s, RoutedEventArgs e)
        {
            _selectedRole = "";
            formStep.Visibility = Visibility.Collapsed;
            roleStep.Visibility = Visibility.Visible;
            titleText.Text = "Create account";
            subtitleText.Text = "Pick the role that fits you.";
            status.Text = "";
        }

        private void Biz_Changed(object s, SelectionChangedEventArgs e)
        {
            // Zair is allowed for either Patur or Murshe — leave checkbox enabled.
        }

        // -------- Live per-field validation (TextChanged + PasswordChanged) --
        // Each handler runs the matching regex and toggles a red BorderBrush +
        // a small inline error text below the field. Empty value = neutral
        // (don't shout at the user before they typed anything).

        private static readonly System.Windows.Media.SolidColorBrush _transparentBrush =
            System.Windows.Media.Brushes.Transparent;

        private System.Windows.Media.Brush Rose
            => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["Rose"];

        private void Mark(System.Windows.Controls.Control ctrl, TextBlock label, bool ok, string msg)
        {
            if (ok)
            {
                ctrl.BorderBrush = _transparentBrush;
                ctrl.BorderThickness = new Thickness(1);
                if (label != null) label.Text = "";
            }
            else
            {
                ctrl.BorderBrush = Rose;
                ctrl.BorderThickness = new Thickness(1.5);
                if (label != null) label.Text = msg;
            }
        }

        private void LiveUser_Changed(object s, TextChangedEventArgs e)
        {
            string v = usernameBox.Text ?? "";
            if (v.Length == 0) { Mark(usernameBox, userErr, true, ""); return; }
            Mark(usernameBox, userErr, UsernameRx.IsMatch(v),
                "4–20 letters, digits, _ or . only.");
        }

        private void LivePass_Changed(object s, RoutedEventArgs e)
        {
            string v = passwordBox.Password ?? "";
            if (v.Length == 0) { Mark(passwordBox, passErr, true, ""); return; }
            bool ok = PasswordRx.IsMatch(v);
            string u = usernameBox.Text ?? "";
            if (ok && !string.IsNullOrEmpty(u) && string.Equals(u, v, StringComparison.OrdinalIgnoreCase))
            { Mark(passwordBox, passErr, false, "Password cannot match the username."); return; }
            Mark(passwordBox, passErr, ok,
                "8+ chars, must include a letter and a digit.");
        }

        private void LiveEmail_Changed(object s, TextChangedEventArgs e)
        {
            string v = emailBox.Text ?? "";
            if (v.Length == 0) { Mark(emailBox, emailErr, true, ""); return; }
            Mark(emailBox, emailErr, EmailRx.IsMatch(v),
                "Email looks invalid (e.g. name@example.com).");
        }

        private void LivePhone_Changed(object s, TextChangedEventArgs e)
        {
            string v = phoneBox.Text ?? "";
            if (v.Length == 0) { Mark(phoneBox, phoneErr, true, ""); return; }
            Mark(phoneBox, phoneErr, PhoneRx.IsMatch(v),
                "7–15 digits, optionally + country code.");
        }

        private void LiveBiz_Changed(object s, TextChangedEventArgs e)
        {
            string v = (bizNameBox.Text ?? "").Trim();
            if (v.Length == 0) { Mark(bizNameBox, bizErr, true, ""); return; }
            Mark(bizNameBox, bizErr, v.Length >= 2 && v.Length <= 120,
                "Business name: 2–120 characters.");
        }

        private void LiveInvite_Changed(object s, TextChangedEventArgs e)
        {
            string v = (inviteBox.Text ?? "").Trim();
            if (v.Length == 0) { Mark(inviteBox, inviteErr, true, ""); return; }
            Mark(inviteBox, inviteErr,
                System.Text.RegularExpressions.Regex.IsMatch(v, @"^[A-Z0-9\-]{4,16}$"),
                "Invite code looks like ACME-7K3D (4–16 chars, letters/digits/dash).");
        }

        private void Create_Click(object s, RoutedEventArgs e)
        {
            if (_selectedRole != "Owner" && _selectedRole != "Employee" && _selectedRole != "Client")
            { ResetSteps_Click(s, e); return; }

            string u = usernameBox.Text?.Trim() ?? "";
            string p = passwordBox.Password ?? "";
            string em = emailBox.Text?.Trim() ?? "";
            string ph = phoneBox.Text?.Trim() ?? "";
            string cur = (curBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ILS";

            // Per-field red borders so the user sees exactly which field is wrong.
            var rose = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["Rose"];
            var soft = System.Windows.Media.Brushes.Transparent;
            usernameBox.BorderBrush = soft; emailBox.BorderBrush = soft; phoneBox.BorderBrush = soft;
            passwordBox.BorderBrush = soft;
            bool bad = false; string firstErr = null;
            void BadTb(System.Windows.Controls.TextBox b, string msg)
            { b.BorderBrush = rose; b.BorderThickness = new Thickness(1.5); bad = true; if (firstErr == null) firstErr = msg; }
            void BadPb(System.Windows.Controls.PasswordBox b, string msg)
            { b.BorderBrush = rose; b.BorderThickness = new Thickness(1.5); bad = true; if (firstErr == null) firstErr = msg; }

            if (!UsernameRx.IsMatch(u))
                BadTb(usernameBox, "Username must be 4–20 letters / digits / _ / .");
            if (!PasswordRx.IsMatch(p))
                BadPb(passwordBox, "Password: 8+ chars, must include a letter and a digit.");
            else if (string.Equals(u, p, StringComparison.OrdinalIgnoreCase))
                BadPb(passwordBox, "Password cannot match the username.");
            if (!EmailRx.IsMatch(em))
                BadTb(emailBox, "Email looks invalid (e.g. name@example.com).");
            if (!PhoneRx.IsMatch(ph))
                BadTb(phoneBox, "Phone must be 7–15 digits, optionally + country code.");
            if (bad) { status.Text = firstErr; return; }

            string bizType = "Individual";
            string bizName = null;
            bool isZair = false;
            int? employeeOwnerId = null;
            if (_selectedRole == "Owner")
            {
                bizType = (bizBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Patur";
                isZair = zairBox.IsChecked == true;
                bizName = (bizNameBox.Text ?? "").Trim();
            }
            else if (IsInviteRole(_selectedRole))
            {
                string code = (inviteBox.Text ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                { status.Text = "Enter the invite code from your business."; return; }
                try
                {
                    var owner = ServiceGateway.Use(c => c.GetOwnerByInviteCode(code));
                    if (owner == null)
                    { status.Text = "Invite code not recognised. Ask the business for it."; return; }
                    employeeOwnerId = owner.Id;
                }
                catch (Exception ex)
                { status.Text = "Lookup failed: " + ex.Message; return; }
            }

            try
            {
                bool exists = ServiceGateway.Use(c => c.CheckUserExist(u));
                if (exists) { status.Text = "Username already taken."; return; }

                bool ok = ServiceGateway.Use(c => c.AddUser(u, p, em, ph, _selectedRole, cur));
                if (!ok) { status.Text = "Server rejected the request."; return; }

                string newInviteCode = null;
                if (_selectedRole == "Owner")
                {
                    try
                    {
                        // Batch the post-create configuration ops on a single
                        // shared channel so we don't reopen WCF 4 times.
                        ServiceGateway.Use(c =>
                        {
                            int newId = c.GetUserId(u);
                            c.SetBusinessType(newId, bizType);
                            if (isZair) c.SetIsZair(newId, true);
                            if (!string.IsNullOrWhiteSpace(bizName))
                                c.SetBusinessName(newId, bizName);
                            // Auto-generate an invite code so the new Owner can
                            // share it with their employees right away.
                            string seed = string.IsNullOrWhiteSpace(bizName) ? u : bizName;
                            newInviteCode = NewInviteCode(seed);
                            c.SetInviteCode(newId, newInviteCode);
                        });
                    }
                    catch { /* best-effort; account exists either way */ }
                }
                else if (IsInviteRole(_selectedRole) && employeeOwnerId.HasValue)
                {
                    try
                    {
                        ServiceGateway.Use(c =>
                        {
                            int newId = c.GetUserId(u);
                            c.SetOwnerId(newId, employeeOwnerId.Value);

                            // Mirror Web behaviour: when a Client signs up, drop
                            // a Customer row into the Owner's CRM so they show
                            // up immediately. Best-effort — failure here
                            // doesn't roll back the user account.
                            if (_selectedRole == "Client")
                            {
                                try
                                {
                                    c.AddCustomer(new Customer
                                    {
                                        BusinessName      = u,
                                        ContactName       = u,
                                        Email             = em,
                                        Phone             = ph,
                                        OwnerId           = employeeOwnerId.Value,
                                        PreferredCurrency = cur ?? "ILS",
                                        Notes             = "Auto-created from Client signup",
                                    });
                                }
                                catch { /* best-effort */ }
                            }
                        });
                    }
                    catch { /* link is best-effort; Owner can fix in Manage Users */ }
                }

                string msg;
                if (_selectedRole == "Owner")
                {
                    msg = "Owner account created. You can sign in now.";
                    if (!string.IsNullOrEmpty(newInviteCode))
                        msg += "\n\nYour company invite code is: " + newInviteCode +
                               "\nShare it with employees and clients who need to join your company.";
                }
                else if (_selectedRole == "Client")
                {
                    msg = "Client account created. The business owner must approve your account before you can sign in.";
                }
                else
                {
                    msg = "Employee account created. The Owner of " +
                          ((employeeOwnerId.HasValue ? "the company" : "the system")) +
                          " must approve your account before you can sign in.";
                }
                MessageBox.Show(msg, "Welcome", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new LogIn());
            }
            catch (Exception ex) { status.Text = "Error: " + ex.Message; }
        }

        private void Back_Click(object s, RoutedEventArgs e)
            => NavigationService?.Navigate(new LogIn());

        // Format: PREFIX-XXXX (4 chars from business name + 4 random alpha-numerics).
        private static string NewInviteCode(string seed)
        {
            string prefix = new string((seed ?? "")
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(4)
                .ToArray());
            if (prefix.Length < 2) prefix = "BMNG";
            const string alpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var rnd = new Random();
            var tail = new string(System.Linq.Enumerable.Range(0, 4)
                .Select(_ => alpha[rnd.Next(alpha.Length)]).ToArray());
            return prefix + "-" + tail;
        }
    }
}
