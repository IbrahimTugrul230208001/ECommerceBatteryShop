using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ECommerceBatteryShop.Controllers;

public class HesapController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly IAddressRepository _addressRepository;
    private readonly ICartService _cartService;
    private readonly IOrderRepository _orderRepository;
    private readonly IOptions<SmtpOptions> _smtpOptions;
    private readonly ILogger<HesapController> _logger;

    public HesapController(
        IAccountRepository accountRepository,
        IOrderRepository orderRepository,
        IConfiguration configuration,
        IUserService userService,
        IAddressRepository addressRepository,
        ICartService cartService,
        IOptions<SmtpOptions> smtpOptions,
        ILogger<HesapController> logger)
    {
        _accountRepository = accountRepository;
        _configuration = configuration;
        _userService = userService;
        _addressRepository = addressRepository;
        _cartService = cartService;
        _orderRepository = orderRepository;
        _smtpOptions = smtpOptions;
        _logger = logger;
    }
    public IActionResult Ayarlar()
    {
        return View("~/Views/Hesap/Ayarlar.cshtml");
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Profil/Ayarlar.cshtml", model);
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var sub = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email) || sub is null || !int.TryParse(sub.Value, out var userId))
        {
            _logger.LogWarning("ChangePassword: user context missing");
            TempData["ErrorMessage"] = "Oturum doğrulanamadı.";
            return RedirectToAction(nameof(Ayarlar));
        }

        var user = await _accountRepository.LogInAsync(email, model.CurrentPassword, ct);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Mevcut şifreniz hatalı.");
            return View("~/Views/Profil/Ayarlar.cshtml", model);
        }

        var updated = await _accountRepository.UpdatePasswordAsync(userId, model.NewPassword, ct);
        if (!updated)
        {
            TempData["ErrorMessage"] = "Şifreniz güncellenemedi. Lütfen tekrar deneyin.";
            return RedirectToAction(nameof(Ayarlar));
        }

        TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi.";
        return RedirectToAction(nameof(Ayarlar));
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
            return Json(new { success = true, redirectUrl = Url.Action("Giris", "Hesap") });
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

    public IActionResult Giris()
    {
        return View("~/Views/Hesap/Giris.cshtml",new LoginViewModel());
    }
    public IActionResult Kayit()
    {
        return View("~/Views/Hesap/Kayit.cshtml");
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

            return RedirectToAction("UrunPaneli", "Admin");
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

        return RedirectToAction("Index", "Ev");
    }

    public IActionResult LogOut()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).Wait();
        }
        return RedirectToAction("Index", "Ev");
    }
    [HttpGet]
    public IActionResult SifreUnuttum()
    {
        if (TempData.TryGetValue("SuccessMessage", out var success))
        {
            ViewBag.SuccessMessage = success;
        }

        if (TempData.TryGetValue("ErrorMessage", out var error))
        {
            ViewBag.ErrorMessage = error;
        }

        return View("~/Views/Hesap/SifreUnuttum.cshtml", new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SifreUnuttum(ForgotPasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Hesap/SifreUnuttum.cshtml", model);
        }

        var user = await _accountRepository.GetByEmailAsync(model.Email, ct);
        if (user is null)
        {
            TempData["SuccessMessage"] = "E-posta adresiniz kayıtlı ise şifre yenileme bağlantısı gönderildi.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(48);
        var token = WebEncoders.Base64UrlEncode(tokenBytes);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var resetRecord = await _accountRepository.CreatePasswordResetTokenAsync(user.Id, token, expiresAt, ct);

        try
        {
            await SendPasswordResetEmailAsync(user.Email, token, expiresAt, ct);
            TempData["SuccessMessage"] = "Şifre yenileme bağlantısı e-posta adresinize gönderildi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email for {Email}", user.Email);
            await _accountRepository.InvalidatePasswordResetTokenAsync(resetRecord.Id, ct);
            TempData["ErrorMessage"] = "Şifre yenileme bağlantısı gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
        }

        return RedirectToAction(nameof(SifreUnuttum));
    }

    [HttpGet]
    public async Task<IActionResult> SifreYenile(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Şifre yenileme bağlantısı geçersiz.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        var resetToken = await _accountRepository.GetPasswordResetTokenAsync(token, ct);
        if (resetToken is null || resetToken.ExpiresAt < DateTime.UtcNow || resetToken.UsedAt is not null)
        {
            TempData["ErrorMessage"] = "Şifre yenileme bağlantısı geçersiz veya süresi dolmuş.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        var model = new ResetPasswordViewModel
        {
            Token = token,
            Email = resetToken.User.Email
        };

        return View("~/Views/Hesap/SifreYenile.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SifreYenile(ResetPasswordViewModel model, CancellationToken ct)
    {
        var resetToken = await _accountRepository.GetPasswordResetTokenAsync(model.Token, ct);
        if (resetToken is null)
        {
            TempData["ErrorMessage"] = "Şifre yenileme bağlantısı geçersiz veya süresi dolmuş.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        model.Email = resetToken.User.Email;

        if (!ModelState.IsValid)
        {
            return View("~/Views/Hesap/SifreYenile.cshtml", model);
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow || resetToken.UsedAt is not null)
        {
            TempData["ErrorMessage"] = "Şifre yenileme bağlantısı geçersiz veya süresi dolmuş.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        var updated = await _accountRepository.UpdatePasswordAsync(resetToken.UserId, model.Password, ct);
        if (!updated)
        {
            TempData["ErrorMessage"] = "Şifreniz güncellenemedi. Lütfen tekrar deneyin.";
            return RedirectToAction(nameof(SifreUnuttum));
        }

        await _accountRepository.InvalidatePasswordResetTokenAsync(resetToken.Id, ct);

        TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi. Giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Giris));
    }

    public async Task<IActionResult> Profil(CancellationToken ct)
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

        return View("~/Views/Hesap/Profil.cshtml",model);
    }


    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Hesap", new { returnUrl });
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

    private async Task SendPasswordResetEmailAsync(string recipientEmail, string token, DateTime expiresAt, CancellationToken ct)
    {
        var options = _smtpOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            throw new InvalidOperationException("SMTP ayarları eksik. Şifre yenileme e-postası gönderilemedi.");
        }

        var resetUrl = Url.Action(nameof(SifreYenile), "Hesap", new { token }, Request.Scheme);
        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            throw new InvalidOperationException("Şifre yenileme bağlantısı oluşturulamadı.");
        }

        var expiresInMinutes = Math.Max(1, (int)Math.Round((expiresAt - DateTime.UtcNow).TotalMinutes));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.SenderName ?? "ECommerce Battery Shop", options.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Şifre Yenileme Bağlantınız";
        message.Body = new TextPart("plain")
        {
            Text = $"Merhaba,\n\nŞifrenizi yenilemek için aşağıdaki bağlantıya tıklayın:\n{resetUrl}\n\nBağlantı {expiresInMinutes} dakika boyunca geçerlidir.\n\nEğer bu talebi siz oluşturmadıysanız lütfen bu e-postayı yok sayın."
        };

        using var client = new SmtpClient();
        var socketOptions = options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(options.Host, options.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(options.UserName))
        {
            await client.AuthenticateAsync(options.UserName, options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
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
