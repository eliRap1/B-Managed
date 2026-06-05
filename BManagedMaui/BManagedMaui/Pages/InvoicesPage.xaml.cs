using System.Collections.ObjectModel;
using BManagedMaui.BMsrv;
using BManagedMaui.Services;

namespace BManagedMaui.Pages;

public partial class InvoicesPage : ContentPage
{
    private readonly ObservableCollection<InvoiceRow> _rows = new();
    private Invoice[] _all = Array.Empty<Invoice>();
    private bool _loaded;

    public InvoicesPage()
    {
        InitializeComponent();
        InvoicesView.ItemsSource = _rows;
        FilterPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AppState.RequireLoginAsync(this)) return;

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
            _all = await ServiceHelper.CallAsync(c => c.GetInvoicesForOwnerAsync(AppState.OwnerId))
                   ?? Array.Empty<Invoice>();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Invoices", "Couldn't load invoices.\n" + ex.Message, "OK");
        }
    }

    private void OnFilterChanged(object sender, EventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<Invoice> q = _all;
        var today = DateTime.Today;

        switch (FilterPicker.SelectedIndex)
        {
            case 1: // Unpaid
                q = _all.Where(i => !IsPaid(i));
                break;
            case 2: // Overdue
                q = _all.Where(i => !IsPaid(i) && i.DueDate.Date < today);
                break;
        }

        _rows.Clear();
        foreach (var inv in q.OrderByDescending(i => i.IssueDate))
            _rows.Add(InvoiceRow.From(inv));
    }

    private static bool IsPaid(Invoice i)
        => string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase);

    private async void OnMarkPaidClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int id }) return;

        bool confirm = await DisplayAlert("Mark paid", "Mark this invoice as paid today?", "Yes", "Cancel");
        if (!confirm) return;

        try
        {
            await ServiceHelper.CallAsync(c =>
                c.MarkInvoicePaidForOwnerAsync(id, AppState.OwnerId, DateTime.Today));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Mark paid", "Failed.\n" + ex.Message, "OK");
        }
    }
}

/// <summary>View-model row for the invoices list.</summary>
public class InvoiceRow
{
    public int    Id          { get; set; }
    public string Number      { get; set; } = string.Empty;
    public string Status      { get; set; } = string.Empty;
    public Color  StatusColor { get; set; } = Colors.Gray;
    public string DueText     { get; set; } = string.Empty;
    public string AmountText  { get; set; } = string.Empty;
    public bool   CanMarkPaid { get; set; }

    public static InvoiceRow From(Invoice i) => new()
    {
        Id          = i.Id,
        Number      = i.InvoiceNumber,
        Status      = i.Status,
        StatusColor = UiHelpers.StatusColor(i.Status),
        DueText     = $"Due {i.DueDate:dd MMM yyyy}",
        AmountText  = $"{UiHelpers.Money(i.Total)} {i.Currency}",
        CanMarkPaid = !string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase),
    };
}
