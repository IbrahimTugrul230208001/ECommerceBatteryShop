using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Concrete
{
    public class OrderRepository : IOrderRepository
    {
        private readonly BatteryShopContext _ctx;

        public OrderRepository(BatteryShopContext ctx)
        {
            _ctx = ctx;
        }
        public async Task<Order> InsertOrderAsync(Order order, CancellationToken ct = default)
        {
            await _ctx.Orders.AddAsync(order, ct);
            await _ctx.SaveChangesAsync(ct);
            return order; // now has generated PK
        }
        public async Task CancelOrder(int orderId)
        {
            var order = await _ctx.Orders.Where(o => o.OrderId == orderId).FirstOrDefaultAsync();
            if (order != null)
            {
                order.Status = "İptal edildi";
                await _ctx.SaveChangesAsync();
            }
        }
        public async Task<List<Order>> GetOrdersAsync()
        {
            return await _ctx.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.Shipment)
                .Include(o => o.Address)
                .Include(o => o.Payments)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Order?> GetOrdersByUserIdAsync(int userId)
        {
            return await _ctx.Orders
                .Include(o => o.Items)
                .Include(o => o.Shipment)
                .Include(o => o.Address)
                .Include(o=>o.Payments)
                .FirstOrDefaultAsync(o => o.UserId == userId);
        }
        public async Task UpdateOrderStatusAsync(int orderId, string newStatus, CancellationToken ct = default)
        {
            var order = await _ctx.Orders.Where(o=>o.OrderId==orderId).FirstOrDefaultAsync();
            if (order != null)
            {
                order.Status = newStatus;   
                await _ctx.SaveChangesAsync(ct);
            }
        }

        public Task<Order?> GetOrderByUserIdAsync(int userId)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<Order>> GetOrdersByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await _ctx.Orders
                .Where(o => o.UserId == userId)
                .Include(o=> o.Items).ThenInclude(i => i.Product)
                .OrderByDescending(o => o.OrderDate)
                .AsNoTracking()
                .ToListAsync(ct);
        }
    }
}
