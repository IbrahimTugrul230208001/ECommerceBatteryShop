using ECommerceBatteryShop.DataAccess.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IProductRepository
    {
        Task<IReadOnlyList<Product>> GetMainPageProductsAsync(int count = 8, CancellationToken ct = default);
    }
}
