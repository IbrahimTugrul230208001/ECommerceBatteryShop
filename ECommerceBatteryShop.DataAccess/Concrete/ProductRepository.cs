using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
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

        public async Task<IReadOnlyList<Product>> GetMainPageProductsAsync(int count, CancellationToken ct = default)
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
        public async Task<Product?> GetProductAsync(int id, CancellationToken ct)
        {
            return await _ctx.Products
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }
        public async Task<List<Product>> ProductSearchResultAsync(string searchTerm)
        {
            try
            {
                IQueryable<Product> query = _ctx.Products.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLowerInvariant();
                    query = query.Where(b => b.Name.ToLower().Contains(term));
                }

                return await query
                    .Take(20) // paging recommended in production
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductSearchResultAsync] searchTerm='{searchTerm}' failed: {ex}");
                throw; // rethrow so you see it in logs/middleware
            }
        }

        public async Task<List<string>> ProductSearchQueryResultAsync(string searchTerm)
        {
            try
            {
                IQueryable<Product> query = _ctx.Products.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLowerInvariant();
                    query = query.Where(b => b.Name.ToLower().Contains(term));
                }

                return await query
                    .Select(b => b.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductSearchQueryResultAsync] searchTerm='{searchTerm}' failed: {ex}");
                throw;
            }
        }

        public async Task<IReadOnlyList<Product>> BringProductsByCategoryIdAsync(
      int categoryId,
      int page = 1,
      int pageSize = 24,
      CancellationToken ct = default)
        {
            if (categoryId <= 0) return Array.Empty<Product>();

            var q =
                from pc in _ctx.ProductCategories.AsNoTracking()
                where pc.CategoryId == categoryId
                select pc.Product!;

            return await q.Where(p => p != null)
                          .OrderBy(p => p.Name)               // or CreatedAt/Popularity
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync(ct);
        }

    }
}
