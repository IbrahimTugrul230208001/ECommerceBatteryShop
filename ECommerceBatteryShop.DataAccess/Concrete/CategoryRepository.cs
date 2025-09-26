using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.DataAccess.Concrete;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly BatteryShopContext _ctx;

    public CategoryRepository(BatteryShopContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<List<Category>> GetCategoriesWithChildrenAsync(CancellationToken ct = default)
    {
        return await _ctx.Categories
            .AsNoTracking()
            .Where(c => c.ParentCategoryId == null)
            .Include(c => c.ProductCategories)
            .Include(c => c.SubCategories)
                .ThenInclude(sc => sc.ProductCategories)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }
}
