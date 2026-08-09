using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.DataAccess.Concrete
{
    public sealed class InventoryRepository : IInventoryRepository
    {
        private readonly BatteryShopContext _ctx;

        public InventoryRepository(BatteryShopContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<IReadOnlyList<(int ProductId, string ProductName, int Quantity)>> SearchAsync(
            string? searchTerm, int take, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Array.Empty<(int, string, int)>();
            }

            searchTerm = searchTerm.Trim();
            var pattern = $"%{searchTerm}%";

            var query = _ctx.Products.AsNoTracking();
            if (int.TryParse(searchTerm, out var productId))
            {
                query = query.Where(p => p.Id == productId || EF.Functions.ILike(p.Name, pattern));
            }
            else
            {
                query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
            }

            var rows = await query
                .OrderBy(p => p.Name)
                .Take(take)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Quantity = p.Inventory != null && p.Inventory.Quantity >= 0 ? p.Inventory.Quantity : 0
                })
                .ToListAsync(ct);

            return rows.Select(r => (r.Id, r.Name, r.Quantity)).ToList();
        }

        public async Task UpdateQuantitiesAsync(IReadOnlyCollection<(int ProductId, int Quantity)> updates,
            CancellationToken ct = default)
        {
            if (updates.Count == 0)
            {
                return;
            }

            var productIds = updates.Select(u => u.ProductId).ToList();

            var existingProducts = await _ctx.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            var inventories = await _ctx.Inventories
                .Where(i => productIds.Contains(i.ProductId))
                .ToDictionaryAsync(i => i.ProductId, ct);

            var now = DateTime.UtcNow;

            foreach (var (productId, quantity) in updates)
            {
                if (!existingProducts.Contains(productId))
                {
                    continue;
                }

                if (inventories.TryGetValue(productId, out var inventory))
                {
                    inventory.Quantity = quantity;
                    inventory.LastUpdated = now;
                }
                else
                {
                    _ctx.Inventories.Add(new Inventory
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        LastUpdated = now
                    });
                }
            }

            await _ctx.SaveChangesAsync(ct);
        }
    }
}
