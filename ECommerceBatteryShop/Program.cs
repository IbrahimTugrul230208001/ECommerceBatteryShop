using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Concrete;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Linq;
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
  builder.Services.AddScoped<ICartService, CartService>();
  builder.Services.AddScoped<IFavoritesService, FavoritesService>();
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
/*builder.Services.AddDataProtection()
    .SetApplicationName("ECommerceBatteryShop")
    .PersistKeysToDbContext<BatteryShopContext>();*/
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
    o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.SaveTokens = true;
    o.Scope.Add("email");
    o.Scope.Add("profile");
    o.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
    o.Events.OnCreatingTicket = async ctx =>
    {
        var services = ctx.HttpContext.RequestServices;
        var db = services.GetRequiredService<BatteryShopContext>();
        var cancellationToken = ctx.HttpContext.RequestAborted;

        var email = ctx.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        var displayName = ctx.Identity?.FindFirst(ClaimTypes.Name)?.Value;

        if (user is null)
        {
            user = new User
            {
                Email = email,
                UserName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                PasswordHash = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(user.UserName))
        {
            user.UserName = displayName;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (ctx.Identity is ClaimsIdentity identity)
        {
            foreach (var existing in identity.FindAll("sub").ToList())
            {
                identity.RemoveClaim(existing);
            }

            identity.AddClaim(new Claim("sub", user.Id.ToString(CultureInfo.InvariantCulture)));
        }
    };
    o.Backchannel = new HttpClient(new HttpLogHandler(new HttpClientHandler()));

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
// Program.cs (middleware)
app.Use(async (ctx, next) =>
{
    const string Cookie = "ANON_ID";
    if (!(ctx.User?.Identity?.IsAuthenticated ?? false) &&
        !ctx.Request.Cookies.ContainsKey(Cookie))
    {
        ctx.Response.Cookies.Append(
            Cookie, Guid.NewGuid().ToString(),
            new CookieOptions { HttpOnly = true, IsEssential = true, Expires = DateTimeOffset.UtcNow.AddMonths(3) });
    }
    await next();
});

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
