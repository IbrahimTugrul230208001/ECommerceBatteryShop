using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface IAccountRepository
{
    Task<User?> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<User?> LogInAsync(string email, string password, CancellationToken ct = default);
    Task<bool> ValidateEmailAsync(string email);
    Task<bool> ValidateUserNameAsync(string userName);
    Task AddNewUserAsync(string email, string password);
}
