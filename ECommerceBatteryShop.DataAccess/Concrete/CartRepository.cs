using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Concrete;

public class CartRepository : ICartRepository
{
    private readonly BatteryShopContext _ctx;
    private readonly ILogger<CartRepository> _log;

    public CartRepository(BatteryShopContext ctx, ILogger<CartRepository> log)
    {
        _ctx = ctx;
        _log = log;
    }

    private IQueryable<Cart> GetCartQuery(int? userId, string? anonId)
    {
        var query = _ctx.Carts.Include(c => c.Items).ThenInclude(i => i.Product).AsQueryable();
        if (userId.HasValue)
            return query.Where(c => c.UserId == userId.Value);
        else
            return query.Where(c => c.AnonId == anonId);
    }

    public async Task<int> AddToCartAsync(int? userId, string? anonId, int productId, int quantity = 1, CancellationToken ct = default)
    {
        var cart = await GetCartQuery(userId, anonId).FirstOrDefaultAsync(ct);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, AnonId = anonId, CreatedAt = DateTime.UtcNow };
            _ctx.Carts.Add(cart);
        }

        var product = await _ctx.Products.FindAsync(new object?[] { productId }, ct);
        if (product == null)
        {
            _log.LogWarning("Product {ProductId} not found when adding to cart", productId);
            return await GetCartItemCountAsync(userId, anonId, ct);
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }
        else
        {
            item.Quantity += quantity;
        }

        await _ctx.SaveChangesAsync(ct);
        return cart.Items.Sum(i => i.Quantity);
    }

    public async Task<int> SetQuantityAsync(int? userId, string? anonId, int productId, int quantity, CancellationToken ct = default)
    {
        var cart = await GetCartQuery(userId, anonId).FirstOrDefaultAsync(ct);
        if (cart == null)
        {
            if (quantity <= 0) return 0;
            cart = new Cart { UserId = userId, AnonId = anonId, CreatedAt = DateTime.UtcNow };
            _ctx.Carts.Add(cart);
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (quantity <= 0)
        {
            if (item != null)
            {
                cart.Items.Remove(item);
                await _ctx.SaveChangesAsync(ct);
            }
            return cart.Items.Sum(i => i.Quantity);
        }

        if (item == null)
        {
            var product = await _ctx.Products.FindAsync(new object?[] { productId }, ct);
            if (product != null)
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }
        }
        else
        {
            item.Quantity = quantity;
        }

        await _ctx.SaveChangesAsync(ct);
        return cart.Items.Sum(i => i.Quantity);
    }

    public async Task<int> RemoveItemAsync(int? userId, string? anonId, int productId, CancellationToken ct = default)
    {
        var cart = await GetCartQuery(userId, anonId).FirstOrDefaultAsync(ct);
        if (cart == null) return 0;

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cart.Items.Remove(item);
            await _ctx.SaveChangesAsync(ct);
        }
        return cart.Items.Sum(i => i.Quantity);
    }

    public async Task ClearCartAsync(int? userId, string? anonId, CancellationToken ct = default)
    {
        var cart = await GetCartQuery(userId, anonId).FirstOrDefaultAsync(ct);
        if (cart != null)
        {
            cart.Items.Clear();
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetCartItemCountAsync(int? userId, string? anonId, CancellationToken ct = default)
    {
        var query = _ctx.Carts.AsNoTracking().Include(c => c.Items).AsQueryable();
        if (userId.HasValue) query = query.Where(c => c.UserId == userId.Value);
        else query = query.Where(c => c.AnonId == anonId);

        var cart = await query.FirstOrDefaultAsync(ct);
        return cart?.Items.Sum(i => i.Quantity) ?? 0;
    }

    public async Task<Cart?> GetCartAsync(int? userId, string? anonId, CancellationToken ct = default)
    {
        return await GetCartQuery(userId, anonId).AsNoTracking().FirstOrDefaultAsync(ct);
    }

    public async Task MergeCartsAsync(string anonId, int userId, CancellationToken ct = default)
    {
        var guest = await _ctx.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.AnonId == anonId, ct);
        if (guest == null) return;

        var user = await _ctx.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (user == null)
        {
            guest.UserId = userId;
            guest.AnonId = null;
            await _ctx.SaveChangesAsync(ct);
            return;
        }

        foreach (var gi in guest.Items)
        {
            var ui = user.Items.FirstOrDefault(i => i.ProductId == gi.ProductId);
            if (ui == null)
            {
                user.Items.Add(new CartItem { ProductId = gi.ProductId, Quantity = gi.Quantity, UnitPrice = gi.UnitPrice });
            }
            else
            {
                ui.Quantity += gi.Quantity;
            }
        }
        _ctx.Carts.Remove(guest);
        await _ctx.SaveChangesAsync(ct);
    }
}
