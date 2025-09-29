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

    public sealed record CategoryFlat(int Id, string Name, int? ParentId);
    public sealed record Categories3Tier(
        List<CategoryFlat> Grand,   // ParentId == null
        List<CategoryFlat> Parent,  // ParentId != null && parent's ParentId == null
        List<CategoryFlat> Child    // ParentId != null && parent's ParentId != null
    );

    public async Task<Categories3Tier> GetCategories3TierAsync(CancellationToken ct = default)
    {
        var flat = await _ctx.Categories
            .AsNoTracking()
            .Select(c => new CategoryFlat(c.Id, c.Name, c.ParentCategoryId))
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        var byId = flat.ToDictionary(x => x.Id);

        bool IsGrand(CategoryFlat c) => c.ParentId is null;

        bool IsParent(CategoryFlat c)
            => c.ParentId is int pId && byId.TryGetValue(pId, out var p) && p.ParentId is null;

        bool IsChild(CategoryFlat c)
            => c.ParentId is int pId && byId.TryGetValue(pId, out var p) && p.ParentId is not null;

        var grand = new List<CategoryFlat>();
        var parent = new List<CategoryFlat>();
        var child = new List<CategoryFlat>();

        foreach (var c in flat)
        {
            if (IsGrand(c)) grand.Add(c);
            else if (IsParent(c)) parent.Add(c);
            else if (IsChild(c)) child.Add(c);
            // deeper levels (if any) are ignored by design
        }

        return new Categories3Tier(grand, parent, child);
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
