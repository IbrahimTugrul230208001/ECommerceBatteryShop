namespace ECommerceBatteryShop.DataAccess.Abstract;

using ECommerceBatteryShop.Domain.Entities;
using static ECommerceBatteryShop.DataAccess.Concrete.CategoryRepository;

public interface ICategoryRepository
{
    /// Returns top-level categories with their child categories.
    Task<List<Category>> GetCategoriesWithChildrenAsync(CancellationToken ct = default);
    Task<List<Category>> GetCategoryTreeAsync();
}
