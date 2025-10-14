using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface IAccountRepository
{
    Task<User?> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<User?> LogInAsync(string email, string password, CancellationToken ct = default);
    Task<bool> ValidateEmailAsync(string email);
    Task<bool> ValidateUserNameAsync(string userName);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<PasswordResetToken> CreatePasswordResetTokenAsync(int userId, string token, DateTime expiresAt, CancellationToken ct = default);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken ct = default);
    Task<bool> UpdatePasswordAsync(int userId, string newPassword, CancellationToken ct = default);
    Task InvalidatePasswordResetTokenAsync(int tokenId, CancellationToken ct = default);
}
