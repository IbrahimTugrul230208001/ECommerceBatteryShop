namespace ECommerceBatteryShop.DataAccess.Abstract;

using ECommerceBatteryShop.DataAccess.Entities;
using static ECommerceBatteryShop.DataAccess.Concrete.CategoryRepository;

public interface ICategoryRepository
{
    /// Returns top-level categories with their child categories.
    Task<List<Category>> GetCategoryTreeAsync();

    /// Find a category by its slug.
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// Find a (tracked) category by id, or null.
    Task<Category?> GetByIdAsync(int id, CancellationToken ct = default);

    /// True if any product is linked to this category.
    Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default);

    /// Delete a category and persist.
    Task DeleteAsync(Category category, CancellationToken ct = default);

    /// Categories a product may be assigned to (non-root, plus leaf-root categories).
    Task<IReadOnlyList<(int Id, string Name)>> GetAssignableAsync(CancellationToken ct = default);
}
