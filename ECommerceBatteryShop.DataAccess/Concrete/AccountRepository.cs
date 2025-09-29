using System.Security.Cryptography;
using System.Text;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerceBatteryShop.DataAccess.Concrete;

public sealed class AccountRepository : IAccountRepository
{
    private readonly BatteryShopContext _ctx;
    private readonly ILogger<AccountRepository> _log;

    public AccountRepository(BatteryShopContext ctx, ILogger<AccountRepository> log)
    {
        _ctx = ctx;
        _log = log;
    }

    public async Task<User?> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        if (await _ctx.Users.AnyAsync(u => u.Email == email, ct))
        {
            _log.LogInformation("Email already exists: {Email}", email);
            return null;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User?> LogInAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _ctx.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user == null)
        {
            _log.LogInformation("User not found: {Email}", email);
            return null;
        }

        var hash = HashPassword(password);
        if (hash != user.PasswordHash)
        {
            _log.LogInformation("Invalid password for {Email}", email);
            return null;
        }

        return user;
    }


    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public Task<bool> ValidateEmailAsync(string email)
    {
        bool exists = _ctx.Users.Any(u => u.Email == email);
        return Task.FromResult(!exists);
    }

    public Task<bool> ValidateUserNameAsync(string userName)
    {
        bool exists = _ctx.Users.Any(u => u.UserName == userName);
        return Task.FromResult(!exists);

    }

}
