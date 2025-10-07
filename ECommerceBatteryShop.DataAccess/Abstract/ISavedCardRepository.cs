using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface ISavedCardRepository
{
    Task<IReadOnlyList<SavedCard>> GetByUserAsync(int userId, CancellationToken ct = default);
    Task<SavedCard?> GetByTokenAsync(int userId, string cardToken, CancellationToken ct = default);
    Task AddAsync(SavedCard card, CancellationToken ct = default);
    Task DeleteAsync(int userId, int id, CancellationToken ct = default);
}
