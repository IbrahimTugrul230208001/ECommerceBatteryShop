using ECommerceBatteryShop.DataAccess.Entities;

namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IFavoritesRepository
    {
        Task<FavoriteList?> GetAsync(int? userId, string? anonId, bool createIfMissing, CancellationToken ct);
        Task<(bool Added, int Total)> ToggleAsync(int? userId, string? anonId, int productId, CancellationToken ct);
        Task<int> CountAsync(int? userId, string? anonId, CancellationToken ct);
    }
}
