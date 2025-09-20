using ECommerceBatteryShop.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IProductRepository
    {
        Task<IReadOnlyList<Product>> GetMainPageProductsAsync(int count, CancellationToken ct = default);
        Task<Product?> GetProductAsync(int id, CancellationToken ct);
        Task<List<Product>> ProductSearchResultAsync(string searchTerm);
        Task<List<(int Id, string Name)>> ProductSearchPairsAsync(string searchTerm, CancellationToken ct = default);
       Task<IReadOnlyList<Product>> BringProductsByCategoryIdAsync(int categoryId, int page = 1, int pageSize = 24, CancellationToken ct = default);
    }
}
