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

    public async Task<int> AddToCartAsync(int userId, int productId, int quantity = 1, CancellationToken ct = default)
    {
        var cart = await _ctx.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow };
            _ctx.Carts.Add(cart);
        }

        var product = await _ctx.Products.FindAsync(new object?[] { productId }, ct);
        if (product == null)
        {
            _log.LogWarning("Product {ProductId} not found when adding to cart", productId);
            return await GetCartItemCountAsync(userId, ct);
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

    public async Task<int> GetCartItemCountAsync(int userId, CancellationToken ct = default)
    {
        var cart = await _ctx.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        return cart?.Items.Sum(i => i.Quantity) ?? 0;
    }
    public async Task<Cart?> GetCartAsync(int userId, CancellationToken ct = default)
    {
        return await _ctx.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
    }
}
