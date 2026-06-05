using BManagedMaui.BMsrv;
using BManagedMaui.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BManagedMaui.Pages;

public partial class OwnerHomePage : ContentPage
{
    private bool _loaded;

    public OwnerHomePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AppState.RequireLoginAsync(this)) return;

        GreetingLabel.Text = $"Hi, {AppState.Username}";
        SubtitleLabel.Text = $"{AppState.Role} · {DateTime.Now:ddd, dd MMM yyyy}";

        if (!_loaded)
        {
            _loaded = true;
            await LoadAsync();
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        try
        {
            var snap = await ServiceHelper.CallAsync(c =>
                c.GetOwnerDashboardSnapshotAsync(AppState.OwnerId, AppState.Currency));

            BuildKpis(snap);
            BuildRecent(snap);
        }
        catch (Exception ex)
        {
            KpiGrid.Clear();
            RecentList.Clear();
            EmptyLabel.IsVisible = true;
            EmptyLabel.Text = "Couldn't load dashboard.\n" + ex.Message;
        }
    }

    private void BuildKpis(OwnerDashboardSnapshot s)
    {
        KpiGrid.Clear();
        var cur = string.IsNullOrEmpty(s.DisplayCurrency) ? AppState.Currency : s.DisplayCurrency;

        var cards = new (string Label, string Value, Color Tint)[]
        {
            ("Customers",       s.CustomersCount.ToString(),           Color.FromArgb("#512BD4")),
            ("Active projects", s.ActiveProjectsCount.ToString(),      Color.FromArgb("#0A86D8")),
            ("Unpaid",          $"{UiHelpers.Money(s.UnpaidTotal)} {cur}", Color.FromArgb("#D97706")),
            ("Overdue",         s.OverdueCount.ToString(),             Color.FromArgb("#DC2626")),
            ("VAT due",         $"{UiHelpers.Money(s.VatDue)} {cur}",  Color.FromArgb("#059669")),
            ("Unread",          s.UnreadNotificationsCount.ToString(), Color.FromArgb("#7C3AED")),
        };

        for (int i = 0; i < cards.Length; i++)
            KpiGrid.Add(MakeCard(cards[i].Label, cards[i].Value, cards[i].Tint), i % 2, i / 2);
    }

    private static Border MakeCard(string label, string value, Color tint) => new()
    {
        StrokeThickness = 1,
        Stroke = new SolidColorBrush(Color.FromArgb("#E5E7EB")),
        BackgroundColor = Colors.White,
        StrokeShape = new RoundRectangle { CornerRadius = 14 },
        Padding = 14,
        Content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = value, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = tint },
                new Label { Text = label, FontSize = 12, TextColor = Colors.Gray },
            }
        }
    };

    private void BuildRecent(OwnerDashboardSnapshot s)
    {
        RecentList.Clear();
        var recent = s.RecentInvoices;

        if (recent == null || recent.Length == 0)
        {
            EmptyLabel.IsVisible = true;
            EmptyLabel.Text = "No recent invoices.";
            return;
        }
        EmptyLabel.IsVisible = false;

        foreach (var inv in recent)
        {
            var left = new VerticalStackLayout { Spacing = 2 };
            left.Add(new Label { Text = inv.InvoiceNumber, FontAttributes = FontAttributes.Bold });
            left.Add(new Label { Text = inv.CustomerName, FontSize = 12, TextColor = Colors.Gray });

            var right = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End };
            right.Add(new Label
            {
                Text = $"{UiHelpers.Money(inv.Total)} {inv.Currency}",
                HorizontalTextAlignment = TextAlignment.End,
            });
            right.Add(new Label
            {
                Text = inv.Status,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelpers.StatusColor(inv.Status),
                HorizontalTextAlignment = TextAlignment.End,
            });

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Padding = 12,
            };
            row.Add(left, 0, 0);
            row.Add(right, 1, 0);

            RecentList.Add(new Border
            {
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#E5E7EB")),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
                Content = row,
            });
        }
    }

    private async void OnInvoicesClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//Invoices");

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Sign out", "Sign out of B-Managed?", "Yes", "Cancel")) return;
        AppState.Clear();
        _loaded = false;
        await Shell.Current.GoToAsync("//Login");
    }
}
