using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace ECommerceBatteryShop.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepo;
        private readonly ILogger<CartService> _log;

        public CartService(ICartRepository cartRepo, ILogger<CartService> log) 
        { 
            _cartRepo = cartRepo; 
            _log = log; 
        }

        public async Task<int> AddAsync(CartOwner owner, int productId, int qty = 1, CancellationToken ct = default)
        {
            if (qty <= 0) return await CountAsync(owner, ct);
            return await _cartRepo.AddToCartAsync(owner.UserId, owner.AnonId, productId, qty, ct);
        }

        public async Task<int> SetQuantityAsync(CartOwner owner, int productId, int quantity, CancellationToken ct = default)
        {
            return await _cartRepo.SetQuantityAsync(owner.UserId, owner.AnonId, productId, quantity, ct);
        }

        public async Task<int> RemoveAsync(CartOwner owner, int productId, CancellationToken ct = default)
        {
            return await _cartRepo.RemoveItemAsync(owner.UserId, owner.AnonId, productId, ct);
        }

        public async Task<int> RemoveAllAsync(CartOwner owner, CancellationToken ct = default)
        {
            await _cartRepo.ClearCartAsync(owner.UserId, owner.AnonId, ct);
            return 0;
        }

        public async Task<int> CountAsync(CartOwner owner, CancellationToken ct = default)
        {
            return await _cartRepo.GetCartItemCountAsync(owner.UserId, owner.AnonId, ct);
        }

        public async Task<Cart?> GetAsync(CartOwner owner, bool createIfMissing = false, CancellationToken ct = default)
        {
            var cart = await _cartRepo.GetCartAsync(owner.UserId, owner.AnonId, ct);
            // If createIfMissing is required and cart is null, we can't easily creating empty cart without adding item via current Repo API.
            // Assuming createIfMissing was mostly for internal use. If external use requires it, we'd need to expand Repo.
            // For now, return what we found.
            return cart;
        }

        public async Task MergeGuestIntoUserAsync(string anonId, int userId, CancellationToken ct = default)
        {
            await _cartRepo.MergeCartsAsync(anonId, userId, ct);
        }

        public async Task<decimal> CartTotalPriceAsync(CartOwner owner, CancellationToken ct = default)
        {
            var cart = await _cartRepo.GetCartAsync(owner.UserId, owner.AnonId, ct);
            if (cart is null) return 0m;

            decimal totalPrice = 0m;
            foreach (var item in cart.Items)
            {
                totalPrice += item.UnitPrice * item.Quantity * 1.2m; // Including tax
            }
            return totalPrice;
        }
    }
}
