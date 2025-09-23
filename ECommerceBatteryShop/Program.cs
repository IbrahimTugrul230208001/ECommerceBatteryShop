using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Concrete;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// MVC + EF
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BatteryShopContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// DI
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

// HttpClient
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

// Hosted service
builder.Services.AddHostedService<FxThreeTimesDailyRefresher>();

// Data Protection (persist keys in DB)
builder.Services.AddDataProtection()
    .SetApplicationName("ECommerceBatteryShop")
    .PersistKeysToDbContext<BatteryShopContext>();

// Auth: Cookie + Google
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.Cookie.Name = ".ebs.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.None;     // cross-site external login
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
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

    // Correlation cookie must also be cross-site + secure
    o.CorrelationCookie.SameSite = SameSiteMode.None;
    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    o.Events.OnCreatingTicket = async ctx =>
    {
        var services = ctx.HttpContext.RequestServices;
        var db = services.GetRequiredService<BatteryShopContext>();
        var ct = ctx.HttpContext.RequestAborted;

        var email = ctx.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email)) return;

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        var displayName = ctx.Identity?.FindFirst(ClaimTypes.Name)?.Value;

        if (user is null)
        {
            user = new User
            {
                Email = email,
                UserName = string.IsNullOrWhiteSpace(displayName) ? email : displayName!,
                PasswordHash = string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else if (!string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(user.UserName))
        {
            user.UserName = displayName!;
            await db.SaveChangesAsync(ct);
        }

        if (ctx.Identity is ClaimsIdentity id)
        {
            // Use your own claim instead of overwriting OIDC "sub"
            foreach (var c in id.FindAll("app_user_id").ToList()) id.RemoveClaim(c);
            id.AddClaim(new Claim("app_user_id", user.Id.ToString(CultureInfo.InvariantCulture)));
        }
    };

    // Optional: debug exact OAuth failure reasons in logs
    o.Events ??= new OAuthEvents();
    o.Events.OnRemoteFailure = ctx =>
    {
        Console.WriteLine("OAuth failure: " + ctx.Failure);
        return Task.CompletedTask;
    };

    // Keep your backchannel if you need it
    o.Backchannel = new HttpClient(new HttpLogHandler(new HttpClientHandler()));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Forwarded headers BEFORE HTTPS/Auth (Azure/Front Door)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});
// Also set Azure App Setting: ASPNETCORE_FORWARDEDHEADERS_ENABLED=1

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// anonId cookie middleware AFTER auth
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

// Debug endpoint
app.MapPost("/debug/currency/refresh", async (ICurrencyService svc, CancellationToken ct) =>
{
    var r = await svc.RefreshNowAsync(ct);
    return Results.Ok(new { rate = r });
});

// Minimal login/logout routes
app.MapGet("/login", (HttpContext ctx) =>
{
    // If you pass subtotal as query(?subtotal=...), you can preserve it:
    // var subtotal = ctx.Request.Query["subtotal"].ToString();
    // var redirect = string.IsNullOrWhiteSpace(subtotal) ? "/" : $"/Cart/Checkout?subtotal={subtotal}";
    var props = new AuthenticationProperties { RedirectUri = "/" };
    return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme });
});
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
