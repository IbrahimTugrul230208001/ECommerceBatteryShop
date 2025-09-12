using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Services
{
    public readonly record struct CartOwner(int? UserId, string? AnonId)
    {
        public bool IsUser => UserId is not null;
        public static CartOwner FromUser(int userId) => new(userId, null);
        public static CartOwner FromAnon(string anonId) => new(null, anonId);
    }

    public interface ICartService
    {
        Task<int> AddAsync(CartOwner owner, int productId, int qty = 1, CancellationToken ct = default);
        Task<int> SetQuantityAsync(CartOwner owner, int productId, int qty, CancellationToken ct = default);
        Task<int> RemoveAsync(CartOwner owner, int productId, CancellationToken ct = default);
        Task<int> CountAsync(CartOwner owner, CancellationToken ct = default);
        Task<Cart> GetAsync(CartOwner owner, bool createIfMissing = false, CancellationToken ct = default);
        Task MergeGuestIntoUserAsync(string anonId, int userId, CancellationToken ct = default);
    }

}
