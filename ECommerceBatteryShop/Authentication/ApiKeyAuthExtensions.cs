using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace ECommerceBatteryShop.Authentication;

public static class ApiKeyAuthExtensions
{
    public const string Scheme = "ApiKey";
    public const string PolicyCatalogWrite = "CatalogWrite";
    private const string DefaultHeader = "X-Api-Key";

    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration config)
    {
        // Add the custom scheme WITHOUT overriding app defaults
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(Scheme, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyCatalogWrite, policy =>
            {
                policy.RequireClaim("scope", "catalog.write");
            });
        });

        services.Configure<ApiKeySettings>(config.GetSection("ApiKeys"));
        return services;
    }

    private sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IOptions<ApiKeySettings> _settings;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IOptions<ApiKeySettings> settings)
            : base(options, logger, encoder, clock)
        {
            _settings = settings;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var settings = _settings.Value ?? new ApiKeySettings();

            var headerName = string.IsNullOrWhiteSpace(settings.Header) ? DefaultHeader : settings.Header;
            if (!Request.Headers.TryGetValue(headerName, out var provided))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var providedKey = provided.ToString();
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var validKeys = settings.CatalogWrite ?? new List<string>();
            var isValid = validKeys.Contains(providedKey);

            if (!isValid)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
            }

            var claims = new List<Claim>
            {
                new("scope", "catalog.write"),
                new(ClaimTypes.Name, "api-client")
            };
            var identity = new ClaimsIdentity(claims, ApiKeyAuthExtensions.Scheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiKeyAuthExtensions.Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

public sealed class ApiKeySettings
{
    public string? Header { get; set; } = "X-Api-Key";
    public List<string>? CatalogWrite { get; set; }
}
