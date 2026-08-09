using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.DataAccess.Concrete
{
    public sealed class FavoritesRepository : IFavoritesRepository
    {
        private readonly BatteryShopContext _db;

        public FavoritesRepository(BatteryShopContext db)
        {
            _db = db;
        }

        public async Task<FavoriteList?> GetAsync(int? userId, string? anonId, bool createIfMissing, CancellationToken ct)
        {
            IQueryable<FavoriteList> query = _db.Set<FavoriteList>()
                .Include(f => f.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p!.Inventory)
                .Include(f => f.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p!.Discounts);

            FavoriteList? list = userId is int uid
                ? await query.FirstOrDefaultAsync(f => f.UserId == uid, ct)
                : await query.FirstOrDefaultAsync(f => f.AnonId == anonId, ct);

            if (list == null && createIfMissing)
            {
                list = new FavoriteList
                {
                    UserId = userId,
                    AnonId = anonId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Add(list);
                await _db.SaveChangesAsync(ct);
            }

            return list;
        }

        public async Task<(bool Added, int Total)> ToggleAsync(int? userId, string? anonId, int productId,
            CancellationToken ct)
        {
            var list = await GetAsync(userId, anonId, createIfMissing: true, ct);

            var existing = await _db.Set<FavoriteListItem>()
                .FirstOrDefaultAsync(i => i.FavoriteId == list!.Id && i.ProductId == productId, ct);

            bool added;
            if (existing is null)
            {
                _db.Add(new FavoriteListItem
                {
                    FavoriteId = list!.Id,
                    ProductId = productId
                });
                added = true;
            }
            else
            {
                _db.Remove(existing);
                added = false;
            }

            await _db.SaveChangesAsync(ct);

            var total = await _db.Set<FavoriteListItem>()
                .CountAsync(i => i.FavoriteId == list!.Id, ct);

            return (added, total);
        }

        public async Task<int> CountAsync(int? userId, string? anonId, CancellationToken ct)
        {
            var list = await GetAsync(userId, anonId, createIfMissing: false, ct);
            if (list is null)
            {
                return 0;
            }

            return await _db.Set<FavoriteListItem>()
                .CountAsync(i => i.FavoriteId == list.Id, ct);
        }
    }
}
