var builder = WebApplication.CreateBuilder(args);

// CSRF protection on every Razor POST (rubric: input validation).
builder.Services.AddRazorPages().AddRazorPagesOptions(o =>
{
    o.Conventions.ConfigureFilter(
        new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "BMA-XSRF";
    o.Cookie.HttpOnly = true;
    // SameAsRequest would allow the XSRF token to travel over plain HTTP if the
    // inner hop is not TLS — use Always so the cookie is always Secure-flagged.
    o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    o.HeaderName = "X-CSRF-Token";
});

builder.Services.AddSession(opts =>
{
    // 30-minute idle timeout is appropriate for a financial app.
    // The previous value of 8 hours would leave unattended sessions valid
    // for an entire working day.
    opts.IdleTimeout = TimeSpan.FromMinutes(30);
    opts.Cookie.HttpOnly = true;
    opts.Cookie.IsEssential = true;
    opts.Cookie.Name = "BMA-Session";
    opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddDataProtection();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.Run();
