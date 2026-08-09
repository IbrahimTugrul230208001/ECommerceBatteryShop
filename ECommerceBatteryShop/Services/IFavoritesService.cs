using ECommerceBatteryShop.DataAccess.Entities;

namespace ECommerceBatteryShop.Services
{
    public interface IFavoritesService
    {
        Task<int> CountAsync(FavoriteOwner owner, CancellationToken ct);
        Task<FavoriteList?> GetAsync(FavoriteOwner owner, bool createIfMissing, CancellationToken ct);
        Task<ToggleResult> ToggleAsync(FavoriteOwner owner, int productId, CancellationToken ct);
    }


    public sealed record ToggleResult(bool Added, int TotalCount);
    public sealed record FavoriteOwner
    {
        public int? UserId { get; }
        public string? AnonId { get; }
        public bool IsUser => UserId is not null;
        private FavoriteOwner(int? userId, string? anonId)
        {
            UserId = userId;
            AnonId = anonId;
        }

        public static FavoriteOwner FromUser(int userId) => new (userId, null);

        public static FavoriteOwner FromAnon(string anonId) => new (null, anonId);
    }
}