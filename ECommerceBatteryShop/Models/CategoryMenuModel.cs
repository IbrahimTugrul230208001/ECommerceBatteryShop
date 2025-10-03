using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public sealed class CategoryMenuModel
    {
        public IEnumerable<Category> Items { get; init; } = Enumerable.Empty<Category>();
        public int Depth { get; init; } = 0; // 0..3
    }

}
