using ECommerceBatteryShop.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IOrderRepository
    {
        Task<Order> InsertOrderAsync(Order order, CancellationToken ct=default);
    }
}
