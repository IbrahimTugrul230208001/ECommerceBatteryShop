using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
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

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return _ctx.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _ctx.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default)
    {
        return _ctx.ProductCategories.AnyAsync(pc => pc.CategoryId == categoryId, ct);
    }

    public async Task DeleteAsync(Category category, CancellationToken ct = default)
    {
        _ctx.Categories.Remove(category);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> GetAssignableAsync(CancellationToken ct = default)
    {
        // Include all non-root categories, plus depth-0 categories that have no children
        // (i.e. leaf root categories that can directly hold products).
        var rows = await _ctx.Categories
            .AsNoTracking()
            .Where(c => c.Depth != "0"
                || !_ctx.Categories.Any(child => child.Path.StartsWith(c.Path + "/") && child.Id != c.Id))
            .OrderBy(c => c.Path)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        return rows.Select(r => (r.Id, r.Name)).ToList();
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
