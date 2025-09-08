namespace ECommerceBatteryShop.DataAccess.Abstract;

using ECommerceBatteryShop.Domain.Entities;

public interface ICategoryRepository
{
    /// Returns top-level categories with their child categories.

    Task<List<Category>> GetCategoriesWithChildrenAsync(CancellationToken ct = default);
}
