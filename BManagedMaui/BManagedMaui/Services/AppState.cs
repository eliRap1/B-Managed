namespace BManagedMaui.Services;

/// <summary>
/// Process-wide state for the signed-in user, plus simple page guards.
/// Mobile equivalent of the Web client's HttpContext.Session. (Mirrors the
/// AppState concept from the Driver-moodle MAUI app.)
/// </summary>
public static class AppState
{
    public static string Username { get; set; } = string.Empty;
    public static int    UserId   { get; set; }
    public static string Role     { get; set; } = string.Empty;   // Owner / Employee / Client
    public static string Currency { get; set; } = "ILS";

    /// <summary>
    /// The Owner whose books we read: an Owner's own id, or an Employee/Client's
    /// parent Owner (falls back to their own id).
    /// </summary>
    public static int OwnerId { get; set; }

    public static bool IsOwner    => Role == "Owner";
    public static bool IsLoggedIn => UserId > 0 && !string.IsNullOrEmpty(Role);

    /// <summary>
    /// Page guard: if nobody is signed in, bounce back to the Login route and
    /// return false so the caller can stop loading.
    /// </summary>
    public static async Task<bool> RequireLoginAsync(Page page)
    {
        if (IsLoggedIn) return true;
        await Shell.Current.GoToAsync("//Login");
        return false;
    }

    public static void Clear()
    {
        Username = string.Empty;
        UserId   = 0;
        Role     = string.Empty;
        Currency = "ILS";
        OwnerId  = 0;
    }
}
