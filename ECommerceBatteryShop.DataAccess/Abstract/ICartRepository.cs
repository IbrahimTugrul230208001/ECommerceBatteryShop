using ECommerceBatteryShop.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface ICartRepository
{
    Task<int> AddToCartAsync(int? userId, string? anonId, int productId, int quantity = 1, CancellationToken ct = default);
    Task<int> SetQuantityAsync(int? userId, string? anonId, int productId, int quantity, CancellationToken ct = default);
    Task<int> RemoveItemAsync(int? userId, string? anonId, int productId, CancellationToken ct = default);
    Task ClearCartAsync(int? userId, string? anonId, CancellationToken ct = default);
    Task<int> GetCartItemCountAsync(int? userId, string? anonId, CancellationToken ct = default);
    Task<Cart?> GetCartAsync(int? userId, string? anonId, CancellationToken ct = default);
    Task MergeCartsAsync(string anonId, int userId, CancellationToken ct = default);
}
