using BManagedMaui.Services;

namespace BManagedMaui.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please fill all fields");
            return;
        }

        SetBusy(true);
        try
        {
            // Fail-fast for an unknown user — skips the expensive PBKDF2 path,
            // exactly like the Web client's Login handler.
            if (!await ServiceHelper.CallAsync(c => c.CheckUserExistAsync(username)))
            {
                ShowError("Invalid username or password");
                return;
            }

            if (!await ServiceHelper.CallAsync(c => c.CheckUserPasswordAsync(username, password)))
            {
                // VerifyPassword filters on isActive, so an unapproved account looks
                // like a wrong password. Probe once to give a clearer reason.
                var probe = await ServiceHelper.CallAsync(async c =>
                {
                    int id = await c.GetUserIdAsync(username);
                    return id > 0 ? await c.GetUserByIdAsync(id) : null;
                });

                ShowError(probe is { IsActive: false }
                    ? "Account awaiting Owner approval."
                    : "Invalid username or password");
                return;
            }

            int userId = await ServiceHelper.CallAsync(c => c.GetUserIdAsync(username));
            var user   = await ServiceHelper.CallAsync(c => c.GetUserByIdAsync(userId));

            AppState.Username = user.Username;
            AppState.UserId   = user.Id;
            AppState.Role     = user.Role;
            AppState.Currency = string.IsNullOrEmpty(user.PreferredCurrency) ? "ILS" : user.PreferredCurrency;
            // Employees/Clients read their parent Owner's books; an Owner reads their own.
            AppState.OwnerId  = user.OwnerId ?? user.Id;

            PasswordEntry.Text = string.Empty;
            await Shell.Current.GoToAsync("//OwnerHome");
        }
        catch (Exception ex)
        {
            ShowError("Cannot reach the server.\n" + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        LoginButton.IsEnabled = !busy;
        if (busy) ErrorLabel.IsVisible = false;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
