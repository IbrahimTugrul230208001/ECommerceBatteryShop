namespace ECommerceBatteryShop.DataAccess.Abstract;

using ECommerceBatteryShop.Domain.Entities;
using static ECommerceBatteryShop.DataAccess.Concrete.CategoryRepository;

public interface ICategoryRepository
{
    /// Returns top-level categories with their child categories.
    Task<List<Category>> GetCategoryTreeAsync();

    /// Find a category by its slug.
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
