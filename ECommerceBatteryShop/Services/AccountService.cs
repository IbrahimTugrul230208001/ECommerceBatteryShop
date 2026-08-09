using ECommerceBatteryShop.DataAccess.Abstract;

namespace ECommerceBatteryShop.Services;

public readonly record struct RegisterResult(bool Success, string? ErrorMessage);

public interface IAccountService
{
    /// <summary>
    /// Registration business rules: password confirmation, email/username availability, then create.
    /// HTTP shaping (JSON, redirects) stays in the controller.
    /// </summary>
    Task<RegisterResult> RegisterAsync(string email, string password, string confirmPassword,
        CancellationToken ct = default);
}

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _accounts;

    public AccountService(IAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, string confirmPassword,
        CancellationToken ct = default)
    {
        if (password != confirmPassword)
        {
            return new RegisterResult(false, "Şifreler eşleşmiyor.");
        }

        if (await _accounts.ValidateEmailAsync(email) == false)
        {
            return new RegisterResult(false, "Email zaten kayıtlı.");
        }

        if (await _accounts.ValidateUserNameAsync(email) == false)
        {
            return new RegisterResult(false, "Kullanıcı adı mevcut Başka bir isim deneyiniz.");
        }

        await _accounts.RegisterAsync(email, password, ct);
        return new RegisterResult(true, null);
    }
}
