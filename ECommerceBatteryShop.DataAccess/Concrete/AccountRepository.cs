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

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
