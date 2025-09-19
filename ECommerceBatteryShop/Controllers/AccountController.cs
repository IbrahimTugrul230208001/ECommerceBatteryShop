using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ECommerceBatteryShop.Controllers;

public class AccountController : Controller
{
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    public AccountController(IAccountRepository accountRepository, IConfiguration configuration, IUserService userService)
    {
        _accountRepository = accountRepository;
        _configuration = configuration;
        _userService = userService;
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
                return Json(new { success = false, message = "Þifreler eþleþmiyor." });
            }

            if (await _accountRepository.ValidateEmailAsync(email) == false)
            {
                return Json(new { success = false, message = "Email zaten kayýtlý." });
            }

            if (await _accountRepository.ValidateUserNameAsync(email) == false)
            {
                return Json(new { success = false, message = "Kullanýcý adý mevcut Baþka bir isim deneyiniz." });
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
        return View();
    }
    public IActionResult Register()
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
