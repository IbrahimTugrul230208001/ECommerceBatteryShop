using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
    public async Task<List<Category>> GetCategoryTreeAsync()
    {
        var all = await _ctx.Categories
            .AsNoTracking()
            .OrderBy(c => c.Depth).ThenBy(c => c.Id)
            .ToListAsync();

        // Path ? node
        var byPath = all.ToDictionary(c => c.Path, c => {
            c.SubCategories = new List<Category>();
            return c;
        });

        var roots = new List<Category>();

        foreach (var c in all)
        {
            if (c.Depth == "0") { roots.Add(c); continue; }

            // parentPath = Path up to last '/'
            var s = c.Path;
            var i = s.LastIndexOf('/');
            var parentPath = i < 0 ? "" : s.Substring(0, i);

            if (byPath.TryGetValue(parentPath, out var parent))
                ((List<Category>)parent.SubCategories!).Add(c);
            else
                roots.Add(c); // fallback if data inconsistent
        }
        return roots; // ready for your recursive Razor (which caps at depth 4)
    }


}
