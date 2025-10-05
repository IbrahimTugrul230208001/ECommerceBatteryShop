using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetMainPageProductsAsync(
            int page,
            int pageSize,
            decimal? minUsd = null,
            decimal? maxUsd = null,
            CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 30;

            try
            {
                IQueryable<Product> query = _ctx.Products
      .AsNoTracking()
      .Include(p => p.Inventory)
      .Include(p => p.Variants)
      .OrderBy(p => p.Id)
      .ThenBy(p => p.Name);
                // tie-breaker to keep stable plans

                if (minUsd.HasValue)
                {
                    query = query.Where(p => p.Price >= minUsd.Value);
                }

                if (maxUsd.HasValue)
                {
                    query = query.Where(p => p.Price <= maxUsd.Value);
                }

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                return (items, totalCount);
            }
            catch (DbException dbEx)
            {
                _log.LogError(dbEx, "DB error while fetching main page products");
                return (Array.Empty<Product>(), 0);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error while fetching main page products");
                throw; // don’t hide coding errors
            }
        }

        public async Task<Product?> GetProductAsync(int id, CancellationToken ct)
        {
            return await _ctx.Products
                .Include(p => p.Inventory)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ProductSearchResultAsync(
            string searchTerm,
            int page,
            int pageSize,
            decimal? minUsd = null,
            decimal? maxUsd = null,
            CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 30;

            try
            {
                IQueryable<Product> query = _ctx.Products
                    .AsNoTracking()
                    .Include(p => p.Inventory);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim().ToLowerInvariant();
                    query = query.Where(p => p.Name.ToLower().Contains(term));
                }

                if (minUsd.HasValue)
                {
                    query = query.Where(p => p.Price >= minUsd.Value);
                }

                if (maxUsd.HasValue)
                {
                    query = query.Where(p => p.Price <= maxUsd.Value);
                }

                query = query
                    .OrderBy(p => p.Name)
                    .ThenBy(p => p.Id);

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[ProductSearchResultAsync] searchTerm='{SearchTerm}' failed", searchTerm);
                throw; // rethrow so you see it in logs/middleware
            }
        }

        public async Task<List<(int Id, string Name)>> ProductSearchPairsAsync(
            string searchTerm,
            CancellationToken ct = default)
        {
            var q = _ctx.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLowerInvariant();
                q = q.Where(p => p.Name.ToLower().Contains(term));
            }

            var raw = await q
                .GroupBy(p => p.Name)
                .Select(g => new { Id = g.Min(p => p.Id), Name = g.Key })
                .OrderBy(x => x.Name)
                .Take(10)
                .ToListAsync(ct);

            return raw.Select(x => (x.Id, x.Name)).ToList();
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> BringProductsByCategoryIdAsync(
          int categoryId,
          int page = 1,
          int pageSize = 24,
          decimal? minUsd = null,
          decimal? maxUsd = null,
          CancellationToken ct = default)
        {
            if (categoryId <= 0)
                return (Array.Empty<Product>(), 0);

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 30;

            var root = await _ctx.Categories.FindAsync(new object[] { categoryId }, ct);
            if (root == null)
                return (Array.Empty<Product>(), 0);

            var categoryIds = await _ctx.Categories
                .Where(c => c.Path.StartsWith(root.Path)) // root + descendants
                .Select(c => c.Id)
                .ToListAsync(ct);

            var query = _ctx.Products
                .AsNoTracking()
                .Include(p => p.Inventory)
                .Where(p => p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)));

            if (minUsd.HasValue)
                query = query.Where(p => p.Price >= minUsd.Value);

            if (maxUsd.HasValue)
                query = query.Where(p => p.Price <= maxUsd.Value);

            query = query.OrderBy(p => p.Name).ThenBy(p => p.Id);

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<List<Product>> GetLatestProductsAsync()
        {
            return await _ctx.Products
                .AsNoTracking()
                .Include(p => p.Inventory)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();
        }
    }
}
