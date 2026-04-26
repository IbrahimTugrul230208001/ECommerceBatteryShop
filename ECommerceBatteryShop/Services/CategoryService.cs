using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceBatteryShop.Services
{
    public interface ICategoryService
    {
        Task<List<Category>> GetCategoryTreeAsync();
        void InvalidateCache();
    }

    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repo;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "CategoryTree";

        public CategoryService(ICategoryRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public async Task<List<Category>> GetCategoryTreeAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<Category>? cached) && cached is not null)
                return cached;

            var tree = await _repo.GetCategoryTreeAsync();

            _cache.Set(CacheKey, tree, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return tree;
        }

        /// <summary>
        /// Call this from AdminController after category create/update/delete.
        /// </summary>
        public void InvalidateCache() => _cache.Remove(CacheKey);
    }
}
