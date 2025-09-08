namespace ECommerceBatteryShop.DataAccess.Abstract;

using ECommerceBatteryShop.Domain.Entities;

public interface ICategoryRepository
{
    /// <summary>
    /// Returns top-level categories with their child categories.
    /// </summary>
    Task<List<Category>> GetCategoriesWithChildrenAsync(CancellationToken ct = default);
}
