using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Concrete;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// MVC + EF
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BatteryShopContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

  builder.Services.AddScoped<IProductRepository, ProductRepository>();
  builder.Services.AddScoped<IAccountRepository, AccountRepository>();
  builder.Services.AddScoped<ICartRepository, CartRepository>();
  builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
  builder.Services.AddScoped<IUserService, UserService>();
  builder.Services.AddMemoryCache();

// Options
builder.Services.AddOptions<CurrencyOptions>()
    .Bind(builder.Configuration.GetSection("Currency"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl) && !string.IsNullOrWhiteSpace(o.ApiKey),
              "Currency:BaseUrl and Currency:ApiKey are required")
    .ValidateOnStart();

// Typed HttpClient for currency service
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

// Hosted service
builder.Services.AddHostedService<FxThreeTimesDailyRefresher>();

// ⬇️ AUTH: Cookie + Google
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.LoginPath = "/login";
    o.LogoutPath = "/logout";
})
.AddGoogle(o =>
{
    o.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    o.SaveTokens = true;
    o.Scope.Add("email");
    o.Scope.Add("profile");
    o.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ⬇️ AUTH MIDDLEWARE ORDER
app.UseAuthentication();
app.UseAuthorization();

// Debug endpoint you had
app.MapPost("/debug/currency/refresh", async (ICurrencyService svc, CancellationToken ct) =>
{
    var r = await svc.RefreshNowAsync(ct);
    return Results.Ok(new { rate = r });
});

// ⬇️ Minimal login/logout routes (optional; use your own controller if preferred)
app.MapGet("/login", (HttpContext ctx) =>
{
    var props = new AuthenticationProperties { RedirectUri = "/" };
    return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme });
});
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// Example home
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
