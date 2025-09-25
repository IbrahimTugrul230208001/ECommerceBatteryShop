using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace ECommerceBatteryShop.Services
{
    public class CartService : ICartService
    {
        private readonly BatteryShopContext _db;
        private readonly ILogger<CartService> _log;
        public CartService(BatteryShopContext db, ILogger<CartService> log) { _db = db; _log = log; }

        public async Task<int> AddAsync(CartOwner owner, int productId, int qty = 1, CancellationToken ct = default)
        {
            if (qty <= 0) return await CountAsync(owner, ct);

            var cart = await GetAsync(owner, createIfMissing: true, ct);
            var product = await _db.Products.FindAsync(new object?[] { productId }, ct);
            if (product is null) { _log.LogWarning("Add: product {id} not found", productId); return await CountAsync(owner, ct); }

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is null)
                cart.Items.Add(new CartItem { ProductId = productId, Quantity = qty, UnitPrice = product.Price });
            else
                item.Quantity += qty;

            await _db.SaveChangesAsync(ct);
            return cart.Items.Sum(i => i.Quantity);
        }

        public async Task<int> ChangeQuantityAsync(
        CartOwner owner, int productId, int delta, CancellationToken ct = default)
        {
            var cart = await GetAsync(owner, createIfMissing: delta > 0, ct);
            if (cart is null) return 0;

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is null)
            {
                if (delta <= 0) return cart.Items.Sum(i => i.Quantity);
                var product = await _db.Products.FindAsync(new object?[] { productId }, ct);
                if (product is null) return cart.Items.Sum(i => i.Quantity);

                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = delta,
                    UnitPrice = product.Price
                });
            }
            else
            {
                var newQty = item.Quantity + delta;
                if (newQty <= 0) cart.Items.Remove(item);
                else item.Quantity = newQty;
            }

            await _db.SaveChangesAsync(ct);
            return cart.Items.Sum(i => i.Quantity);
        }


        public async Task<int> RemoveAsync(CartOwner owner, int productId, CancellationToken ct = default)
        {
            var cart = await GetAsync(owner, createIfMissing: false, ct);
            if (cart is null) return 0;
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is not null) cart.Items.Remove(item);
            await _db.SaveChangesAsync(ct);
            return cart.Items.Sum(i => i.Quantity);
        }
        public async Task<int> RemoveAllAsync(CartOwner owner, CancellationToken ct = default)
        {
            var cart = await GetAsync(owner, createIfMissing: false, ct);
            if (cart is null) return 0;

            cart.Items.Clear(); // removes all items at once

            await _db.SaveChangesAsync(ct);
            return 0; // since all products are removed, total quantity is always zero
        }

        public async Task<int> CountAsync(CartOwner owner, CancellationToken ct = default)
            => await _db.Carts
                .Where(c => (owner.IsUser && c.UserId == owner.UserId) || (!owner.IsUser && c.AnonId == owner.AnonId))
                .SelectMany(c => c.Items)
                .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        public async Task<Cart?> TryGetAsync(CartOwner owner, CancellationToken ct)
            => await _db.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c =>
                    (owner.IsUser && c.UserId == owner.UserId) ||
                    (!owner.IsUser && c.AnonId == owner.AnonId), ct);

        public async Task<Cart> GetAsync(CartOwner owner, bool createIfMissing = false, CancellationToken ct = default)
        {
            var cart = await TryGetAsync(owner, ct);
            if (cart is not null || !createIfMissing) return cart!;
            cart = owner.IsUser ? new Cart { UserId = owner.UserId, CreatedAt = DateTime.UtcNow }
                                : new Cart { AnonId = owner.AnonId, CreatedAt = DateTime.UtcNow };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
            // ensure Items loaded for callers that sum right away
            _db.Entry(cart).Collection(c => c.Items).Load();
            return cart;
        }

        public async Task MergeGuestIntoUserAsync(string anonId, int userId, CancellationToken ct = default)
        {
            // idempotent: tolerate missing guest or existing user cart
            var guest = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.AnonId == anonId, ct);
            if (guest is null) return;

            var user = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (user is null) { guest.UserId = userId; guest.AnonId = null; await _db.SaveChangesAsync(ct); return; }

            foreach (var gi in guest.Items)
            {
                var ui = user.Items.FirstOrDefault(i => i.ProductId == gi.ProductId);
                if (ui is null) user.Items.Add(new CartItem { ProductId = gi.ProductId, Quantity = gi.Quantity, UnitPrice = gi.UnitPrice });
                else ui.Quantity += gi.Quantity;
            }
            _db.Carts.Remove(guest);
            await _db.SaveChangesAsync(ct);
        }
    }

}
