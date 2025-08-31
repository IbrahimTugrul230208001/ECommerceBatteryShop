using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface ICartRepository
{
    Task<int> AddToCartAsync(int userId, int productId, int quantity = 1, CancellationToken ct = default);
    Task<int> GetCartItemCountAsync(int userId, CancellationToken ct = default);
}
