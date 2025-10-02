using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
    }
}
