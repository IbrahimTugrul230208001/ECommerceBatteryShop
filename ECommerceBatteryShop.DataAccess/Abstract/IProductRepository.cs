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
        Task<List<string>> ProductSearchQueryResultAsync(string searchTerm);
    }
}
