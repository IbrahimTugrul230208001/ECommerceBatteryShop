using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using System.Configuration;
using System.Net.Http;
using System.Net.Mail;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ECommerceBatteryShop.Controllers;

public class AccountController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;

    public AccountController(IAccountRepository accountRepository, IConfiguration configuration)
    {
        _accountRepository = accountRepository;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromBody] User user)
    {
        try
        {
            string email = user.Email;
            string userName = user.UserName;
            string password = user.NewPassword;
            string confirmPassword = user.NewPasswordAgain;


            if (password != confirmPassword)
            {
                return Json(new { success = false, message = "Þifreler eþleþmiyor." });
            }

            if (await _userManager.ValidateEmailAsync(email) == false)
            {
                return Json(new { success = false, message = "Email zaten kayýtlý." });
            }

            if (await _userManager.ValidateUserNameAsync(userName) == false)
            {
                return Json(new { success = false, message = "Kullanýcý adý mevcut Baþka bir isim deneyiniz." });
            }

            string verificationCode = new Random().Next(100000, 999999).ToString();
            _userService.VerificationCode = verificationCode;
            await SendEmailAsync();
            _userService.UserName = userName;
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

    [HttpPost]
    public async Task<IActionResult> VerifyEmail([FromBody] User user)
    {
        if (user.VerificationCode == _userService.VerificationCode)
        {
            string email = _userService.Email;
            string userName = _userService.UserName;
            string password = _userService.Password;
            await _userManager.AddNewUserAsync(email, userName, password);
            return Json(new { success = true, redirectUrl = Url.Action("Profil", "Kullanici") });
        }
        else
        {
            return Json(new { success = false, message = "Hatalý Kod", redirectUrl = Url.Action("Verification") });
        }
    }
    [HttpPost]
    public async Task SendEmailAsync()
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Senin Kütüphanen", "seninkutuphanen@outlook.com"));
            message.To.Add(new MailboxAddress("", _userService.Email));
            message.Subject = "Verification Code";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Verification Code</title>
    <style>
        body {{
            background-color: #121212;
            color: #ffffff;
            font-family: Arial, sans-serif;
            text-align: center;
            padding: 20px;
        }}
        .container {{
            max-width: 400px;
            margin: 0 auto;
            background-color: #1e1e1e;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 0 10px rgba(255, 255, 255, 0.1);
        }}
        h3 {{
            color: #bb86fc;
        }}
        p {{
            font-size: 16px;
        }}
        .code {{
            font-size: 24px;
            font-weight: bold;
            color: #03dac6;
            background-color: #333;
            padding: 10px;
            border-radius: 5px;
            display: inline-block;
            margin-top: 10px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h3>Doðrulama Kodunuz</h3>
        <p>Hesabýnýzý doðrulamak için bu kodu kullanýn:</p>
        <div class=""code"">{GenerateVerificationCodeAsync()}</div>
    </div>
</body>
</html>
"
            };

            message.Body = bodyBuilder.ToMessageBody();

            // Retrieve the API key securely (e.g., from environment variables)
            var key = _configuration["SMTP:key"];

            using var client = new SmtpClient();

            // Connect to the SMTP server with STARTTLS
            await client.ConnectAsync("smtp-relay.brevo.com", 587, SecureSocketOptions.StartTls);
            Console.WriteLine("Connected to SMTP server.");

            await client.AuthenticateAsync("879650001@smtp-brevo.com", key);
            Console.WriteLine("Authenticated successfully.");

            // Send the email
            await client.SendAsync(message);
            Console.WriteLine("Email sent successfully.");

            // Disconnect from the server
            await client.DisconnectAsync(true);
            Console.WriteLine("Disconnected from SMTP server.");
        }
        catch (SmtpCommandException ex)
        {
            Console.WriteLine($"SMTP Command Error: {ex.Message} - Status Code: {ex.StatusCode}");
            // Log the full exception for debugging
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        catch (SmtpProtocolException ex)
        {
            Console.WriteLine($"SMTP Protocol Error: {ex.Message}");
            // Log the full exception for debugging
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Error: {ex.Message}");
            // Log the full exception for debugging
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    public async Task<string> GenerateVerificationCodeAsync()
    {
        string verificationCode = new Random().Next(100000, 999999).ToString();
        return verificationCode;
    }

    public IActionResult LogIn()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> LogIn(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)

        {
            return View(model);
        }

        var user = await _accountRepository.LogInAsync(model.Email, model.Password, ct);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password");
            return View(model);
        }

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

    public IActionResult Profile()
    {
        return View();
    }

    public IActionResult VerifyAccount()
    {
        return View();
    }
}
