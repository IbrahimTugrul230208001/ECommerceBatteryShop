using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

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


    private static string HashPassword(string password) => HashValue(password);

    private static string HashToken(string token) => HashValue(token);

    private static string HashValue(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
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

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return _ctx.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<PasswordResetToken> CreatePasswordResetTokenAsync(int userId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        var existing = await _ctx.PasswordResetTokens
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _ctx.PasswordResetTokens.RemoveRange(existing);
        }

        var entity = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = HashToken(token),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.PasswordResetTokens.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return entity;
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken ct = default)
    {
        var hash = HashToken(token);
        return _ctx.PasswordResetTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _ctx.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            _log.LogWarning("User {UserId} not found when updating password", userId);
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task InvalidatePasswordResetTokenAsync(int tokenId, CancellationToken ct = default)
    {
        var entity = await _ctx.PasswordResetTokens.SingleOrDefaultAsync(x => x.Id == tokenId, ct);
        if (entity is null)
        {
            return;
        }

        entity.UsedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}
