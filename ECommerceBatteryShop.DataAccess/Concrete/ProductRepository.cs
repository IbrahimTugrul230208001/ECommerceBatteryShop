using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Concrete
{

    public sealed class ProductRepository : IProductRepository
    {
        private readonly BatteryShopContext _ctx;
        private readonly ILogger<ProductRepository> _log;

        public ProductRepository(BatteryShopContext ctx, ILogger<ProductRepository> log)
        {
            _ctx = ctx;
            _log = log;
        }

        public async Task<IReadOnlyList<Product>> GetMainPageProductsAsync(int count = 8, CancellationToken ct = default)
        {
            try
            {
                var query = _ctx.Products
                    .AsNoTracking()
                    .Include(p => p.Variants)
                    // Optional filters:
                    //.Where(p => p.IsActive)
                    //.Where(p => p.Stock > 0)
                    // Deterministic order:
                    .OrderBy(p => p.Id)  // “newest” if Id is identity/autoincrement
                    .ThenBy(p => p.Name); // tie-breaker to keep stable plans

                return await query.Take(count).ToListAsync(ct);
            }
            catch (DbException dbEx)
            {
                _log.LogError(dbEx, "DB error while fetching main page products");
                return Array.Empty<Product>();
            }
            catch (System.Exception ex)
            {
                _log.LogError(ex, "Unexpected error while fetching main page products");
                throw; // don’t hide coding errors
            }
        }
    }
}
