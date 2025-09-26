using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ECommerceBatteryShop.Controllers;

public class AccountController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly BatteryShopContext _db;
    public AccountController(IAccountRepository accountRepository, IConfiguration configuration, IUserService userService, BatteryShopContext db)
    {
        _accountRepository = accountRepository;
        _configuration = configuration;
        _userService = userService;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromBody] UserViewModel userViewModel)
    {
        try
        {
            string email = userViewModel.Email;
            string password = userViewModel.NewPassword;
            string confirmPassword = userViewModel.NewPasswordAgain;


            if (password != confirmPassword)
            {
                return Json(new { success = false, message = "Şifreler eşleşmiyor." });
            }

            if (await _accountRepository.ValidateEmailAsync(email) == false)
            {
                return Json(new { success = false, message = "Email zaten kayıtlı." });
            }

            if (await _accountRepository.ValidateUserNameAsync(email) == false)
            {
                return Json(new { success = false, message = "Kullanıcı adı mevcut Başka bir isim deneyiniz." });
            }

            string verificationCode = new Random().Next(100000, 999999).ToString();
            _userService.VerificationCode = verificationCode;
            _userService.Email = email;
            _userService.Password = password;
            return Json(new { success = true, redirectUrl = Url.Action("KullaniciDogrulama", "Dogrulama") });
        }
        catch (ApplicationException ex)
        {
            Console.WriteLine($"Application Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected Error: {ex.Message}");
            return Json(new { success = false, message = "An unexpected error occurred while registering the user." });
        }
    }

    public IActionResult LogIn()
    {
        return View(new LoginViewModel());
    }
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogIn(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)

        {
            return View(model);
        }

        var adminSection = _configuration.GetSection("Admin");
        var adminEmail = adminSection["Email"];
        var adminPassword = adminSection["Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) &&
            !string.IsNullOrWhiteSpace(adminPassword) &&
            string.Equals(model.Email, adminEmail, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(model.Password, adminPassword, StringComparison.Ordinal))
        {
            var adminClaims = new List<Claim>
            {
                new Claim("sub", "admin"),
                new Claim(ClaimTypes.NameIdentifier, adminEmail),
                new Claim(ClaimTypes.Email, adminEmail),
                new Claim(ClaimTypes.Name, "Yönetici"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var adminIdentity = new ClaimsIdentity(adminClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var adminPrincipal = new ClaimsPrincipal(adminIdentity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, adminPrincipal);

            return RedirectToAction("Index", "Admin");
        }

        var user = await _accountRepository.LogInAsync(model.Email, model.Password, ct);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.UserName) ? user.Email : user.UserName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction(nameof(Profile));
    }


    public IActionResult ForgotPassword()
    {
        return View();
    }

    public IActionResult ResetPassword()
    {
        return View();
    }

    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var addressList = new AddressListViewModel
        {
            ContainerId = "address-list",
            Addresses = Array.Empty<AddressViewModel>()
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                var addresses = await _db.Addresses
                    .AsNoTracking()
                    .Where(a => a.UserId == userId)
                    .ToListAsync(ct);

                addressList = new AddressListViewModel
                {
                    ContainerId = "address-list",
                    Addresses = addresses
                        .OrderByDescending(a => a.IsDefault)
                        .ThenBy(a => a.Id)
                        .Select(AddressViewModel.FromEntity)
                        .ToList()
                };
            }
        }

        ViewBag.AddressList = addressList;

        return View();
    }

    public IActionResult VerifyAccount()
    {
        return View();
    }

    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Account", new { returnUrl });
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl ?? Url.Content("~/")
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            TempData["ErrorMessage"] = $"Google ile oturum açma sırasında hata oluştu: {remoteError}";
            return RedirectToAction(nameof(LogIn));
        }

        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            var externalResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!externalResult.Succeeded || externalResult.Principal is null)
            {
                TempData["ErrorMessage"] = "Google ile oturum açma tamamlanamadı.";
                return RedirectToAction(nameof(LogIn));
            }

            var claims = externalResult.Principal.Claims.ToList();
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            var tokens = externalResult.Properties?.GetTokens();
            if (tokens is not null)
            {
                authProperties.StoreTokens(tokens);
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        }

        return LocalRedirect(returnUrl);
    }
}
