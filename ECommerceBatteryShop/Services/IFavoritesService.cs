using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Services
{
    public interface IFavoritesService
    {
        Task<int> CountAsync(FavoriteOwner owner, CancellationToken ct);
        Task<FavoriteList?> GetAsync(FavoriteOwner owner, bool createIfMissing, CancellationToken ct);
        Task<ToggleResult> ToggleAsync(FavoriteOwner owner, int productId, CancellationToken ct);
    }


    public sealed record ToggleResult(bool Added, int TotalCount);
    public sealed class FavoriteOwner
    {
        public int? UserId { get; }
        public string? AnonId { get; }

        private FavoriteOwner(int? userId, string? anonId)
        {
            UserId = userId;
            AnonId = anonId;
        }

        public static FavoriteOwner FromUser(int userId) =>
            new FavoriteOwner(userId, null);

        public static FavoriteOwner FromAnon(string anonId) =>
            new FavoriteOwner(null, anonId);
    }
}
