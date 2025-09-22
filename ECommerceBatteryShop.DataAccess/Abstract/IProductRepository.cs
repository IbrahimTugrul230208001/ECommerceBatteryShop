using ECommerceBatteryShop.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IProductRepository
    {
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetMainPageProductsAsync(
            int page,
            int pageSize,
            decimal? minUsd = null,
            decimal? maxUsd = null,
            CancellationToken ct = default);
        Task<Product?> GetProductAsync(int id, CancellationToken ct);
        Task<(IReadOnlyList<Product> Items, int TotalCount)> ProductSearchResultAsync(
            string searchTerm,
            int page,
            int pageSize,
            decimal? minUsd = null,
            decimal? maxUsd = null,
            CancellationToken ct = default);
        Task<List<(int Id, string Name)>> ProductSearchPairsAsync(string searchTerm, CancellationToken ct = default);
        Task<(IReadOnlyList<Product> Items, int TotalCount)> BringProductsByCategoryIdAsync(
            int categoryId,
            int page = 1,
            int pageSize = 24,
            decimal? minUsd = null,
            decimal? maxUsd = null,
            CancellationToken ct = default);
    }
}
