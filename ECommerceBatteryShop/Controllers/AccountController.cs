using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ECommerceBatteryShop.Controllers;

public class AccountController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly IAddressRepository _addressRepository;
    private readonly ICartService _cartService;
    private readonly IOrderRepository _orderRepository;
    public AccountController(IAccountRepository accountRepository,IOrderRepository orderRepository, IConfiguration configuration, IUserService userService, IAddressRepository addressRepository, ICartService cartService)
    {
        _accountRepository = accountRepository;
        _configuration = configuration;
        _userService = userService;
        _addressRepository = addressRepository;
        _cartService = cartService;
        _orderRepository = orderRepository;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser(UserViewModel userViewModel)
    {
        try
        {
            string email = userViewModel.Email;
            string password = userViewModel.Password;
            string confirmPassword = userViewModel.ConfirmPassword;


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
            await _accountRepository.RegisterAsync(email, password);
            return Json(new { success = true, redirectUrl = Url.Action("LogIn", "Account") });
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

        var anonId = Request.Cookies["ANON_ID"];
        if (!string.IsNullOrWhiteSpace(anonId))
        {
            await _cartService.MergeGuestIntoUserAsync(anonId, user.Id, ct);
            Response.Cookies.Delete("ANON_ID");
        }

        return RedirectToAction("Index", "Home");
    }

    public IActionResult LogOut()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).Wait();
        }
        return RedirectToAction("Index", "Home");
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
        IReadOnlyList<AddressViewModel> addresses = Array.Empty<AddressViewModel>();
        IReadOnlyList<Order> orders = Array.Empty<Order>();
        if (User.Identity?.IsAuthenticated == true)
        {
            var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out var userId))
            {
                var entities = await _addressRepository.GetByUserAsync(userId, ct);
                orders = await _orderRepository.GetOrdersByUserIdAsync(userId, ct);
                addresses = entities.Select(MapAddress).ToList();
            }
        }

        var model = new ProfileViewModel
        {
            Addresses = addresses,
            Orders = orders
        };

        return View(model);
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

    private static AddressViewModel MapAddress(Address address)
    {
        return new AddressViewModel
        {
            Id = address.Id,
            UserId = address.UserId,
            Title = address.Title,
            Name = address.Name,
            Surname = address.Surname,
            PhoneNumber = address.PhoneNumber,
            FullAddress = address.FullAddress,
            City = address.City,
            State = address.State,
            Neighbourhood = address.Neighbourhood,
            IsDefault = address.IsDefault
        };
    }
}
