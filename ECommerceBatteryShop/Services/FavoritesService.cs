using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;

namespace ECommerceBatteryShop.Services
{
    /// <summary>
    /// Thin orchestration over <see cref="IFavoritesRepository"/>: translates the web-layer
    /// <see cref="FavoriteOwner"/> into (userId, anonId) primitives. Data access lives in the repository.
    /// </summary>
    public sealed class FavoritesService : IFavoritesService
    {
        private readonly IFavoritesRepository _repo;

        public FavoritesService(IFavoritesRepository repo)
        {
            _repo = repo;
        }

        public Task<FavoriteList?> GetAsync(FavoriteOwner owner, bool createIfMissing, CancellationToken ct)
            => _repo.GetAsync(owner.UserId, owner.AnonId, createIfMissing, ct);

        public async Task<ToggleResult> ToggleAsync(FavoriteOwner owner, int productId, CancellationToken ct)
        {
            var (added, total) = await _repo.ToggleAsync(owner.UserId, owner.AnonId, productId, ct);
            return new ToggleResult(added, total);
        }

        public Task<int> CountAsync(FavoriteOwner owner, CancellationToken ct)
            => _repo.CountAsync(owner.UserId, owner.AnonId, ct);
    }
}
