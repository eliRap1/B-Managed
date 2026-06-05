namespace BManagedMaui.Services;

/// <summary>Small formatting helpers shared by the dashboard and invoice screens.</summary>
public static class UiHelpers
{
    public static string Money(decimal value) => value.ToString("N0");

    /// <summary>Status-pill colour, matching the web client's status palette.</summary>
    public static Color StatusColor(string status) => (status ?? string.Empty).ToLowerInvariant() switch
    {
        "paid"    => Color.FromArgb("#059669"),
        "overdue" => Color.FromArgb("#DC2626"),
        "sent"    => Color.FromArgb("#0A86D8"),
        "draft"   => Color.FromArgb("#6B7280"),
        _         => Color.FromArgb("#6B7280"),
    };
}
