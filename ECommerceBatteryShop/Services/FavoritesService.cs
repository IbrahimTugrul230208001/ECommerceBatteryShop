using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace ECommerceBatteryShop.Services
{
    public sealed class FavoritesService : IFavoritesService
    {
        private readonly BatteryShopContext _db;

        public FavoritesService(BatteryShopContext db)
        {
            _db = db;
        }

        public async Task<FavoriteList?> GetAsync(FavoriteOwner owner, bool createIfMissing, CancellationToken ct)
        {
            IQueryable<FavoriteList> query = _db.Set<FavoriteList>()
                .Include(f => f.Items)
                .ThenInclude(i => i.Product);

            FavoriteList? list = owner.UserId is int uid
                ? await query.FirstOrDefaultAsync(f => f.UserId == uid, ct)
                : await query.FirstOrDefaultAsync(f => f.AnonId == owner.AnonId, ct);

            if (list == null && createIfMissing)
            {
                list = new FavoriteList
                {
                    UserId = owner.UserId,
                    AnonId = owner.AnonId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Add(list);
                await _db.SaveChangesAsync(ct);
            }

            return list;
        }

        public async Task<ToggleResult> ToggleAsync(FavoriteOwner owner, int productId, CancellationToken ct)
        {
            var list = await GetAsync(owner, createIfMissing: true, ct);

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

            return new ToggleResult(added, total);
        }
        public async Task<int> CountAsync(FavoriteOwner owner, CancellationToken ct)
        {
            var list = await GetAsync(owner, createIfMissing: false, ct);
            if (list is null)
            {
                return 0;
            }

            var count = await _db.Set<FavoriteListItem>()
                .CountAsync(i => i.FavoriteId == list.Id, ct);

            return count;
        }

    }
}
