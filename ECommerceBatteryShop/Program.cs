using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Concrete;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + EF
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BatteryShopContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddMemoryCache();

// Options (with validation is better)
builder.Services.AddOptions<CurrencyOptions>()
    .Bind(builder.Configuration.GetSection("Currency"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl) && !string.IsNullOrWhiteSpace(o.ApiKey),
              "Currency:BaseUrl and Currency:ApiKey are required")
    .ValidateOnStart();

// Typed HttpClient for currency service
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

// ⬇️ Make sure this matches your actual hosted service class name
builder.Services.AddHostedService<FxThreeTimesDailyRefresher>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Quick manual trigger to verify everything runs
app.MapPost("/debug/currency/refresh", async (ICurrencyService svc, CancellationToken ct) =>
{
    var r = await svc.RefreshNowAsync(ct);
    return Results.Ok(new { rate = r });
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
