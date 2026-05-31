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
    o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    o.HeaderName = "X-CSRF-Token";
});

builder.Services.AddSession(opts =>
{
    opts.IdleTimeout = TimeSpan.FromHours(8);
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
