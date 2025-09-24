using System.Globalization;
using System.Security.Claims;
using Azure.Storage.Blobs;
using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Concrete;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Security;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// ------------------------- MVC + EF -------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BatteryShopContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ------------------------- DI -------------------------
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IFavoritesService, FavoritesService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICspNonceService, CspNonceService>();
builder.Services.AddMemoryCache();

// ------------------------- Options -------------------------
builder.Services.AddOptions<CurrencyOptions>()
    .Bind(builder.Configuration.GetSection("Currency"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl) && !string.IsNullOrWhiteSpace(o.ApiKey),
        "Currency:BaseUrl and Currency:ApiKey are required")
    .ValidateOnStart();

// ------------------------- HttpClient(s) & hosted -------------------------
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();
builder.Services.AddHostedService<FxThreeTimesDailyRefresher>();

// ------------------------- Data Protection → Azure Blob -------------------------
// Choose SAS if provided, else fall back to connection string + container.
// Requires: Azure.Storage.Blobs + Azure.Extensions.AspNetCore.DataProtection.Blobs
var storageConn = builder.Configuration["AzureStorage:ConnectionString"];
var containerName = builder.Configuration["AzureStorage:ContainerName"] ?? "keys";

var blobSvc = new BlobServiceClient(storageConn);
var container = blobSvc.GetBlobContainerClient(containerName);
await container.CreateIfNotExistsAsync();            // if in Main, call sync variant

var blob = container.GetBlobClient("keys.xml");

builder.Services.AddDataProtection()
    .SetApplicationName("ECommerceBatteryShop")
    .PersistKeysToAzureBlobStorage(blob);            // <- BlobClient overload





// ------------------------- Antiforgery -------------------------
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = ".ebs.af";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// ------------------------- Auth (Cookie + Google) -------------------------
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.Cookie.Name = ".ebs.auth.v1";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.None; // required for external redirects back
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(20);
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

    // Correlation cookie for OAuth "state"
    o.CorrelationCookie.SameSite = SameSiteMode.None;
    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    o.Events.OnCreatingTicket = async ctx =>
    {
        try
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
                // Don't overwrite OIDC "sub"; add your own app id
                foreach (var c in id.FindAll("app_user_id").ToList()) id.RemoveClaim(c);
                id.AddClaim(new Claim("app_user_id", user.Id.ToString(CultureInfo.InvariantCulture)));
            }
        }
        catch (Exception ex)
        {
            var logger = ctx.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("OAuth");
            logger.LogError(ex, "OnCreatingTicket failed");

            // Abort this sign-in gracefully; redirect to login with error
            ctx.Fail("oauth_db");
            ctx.Response.Redirect("/login?error=oauth_db");
            return;
        }
    };

    o.Events.OnRemoteFailure = ctx =>
    {
        ctx.HandleResponse();
        var msg = Uri.EscapeDataString(ctx.Failure?.Message ?? "remote_failure");
        ctx.Response.Redirect($"/login?error={msg}");
        return Task.CompletedTask;
    };

    // If you had a custom HttpLogHandler, plug it here. Otherwise comment out.
    // o.Backchannel = new HttpClient(new HttpClientHandler());
});

// ------------------------- Build -------------------------
var app = builder.Build();



// ------------------------- Middleware pipeline -------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Trust Cloudflare/X-Forwarded-* BEFORE anything that reads scheme/host
var fh = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
};
fh.KnownNetworks.Clear();
fh.KnownProxies.Clear();
app.UseForwardedHeaders(fh);

app.UseContentSecurityPolicy();

app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Optional anon id
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

// Minimal login/logout
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

// MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
