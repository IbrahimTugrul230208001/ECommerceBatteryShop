using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Services
{
    public interface ICategoryService
    {
        Task<List<Category>> GetCategoryTreeAsync();
    }

    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repo;
        public CategoryService(ICategoryRepository repo) => _repo = repo;

        public Task<List<Category>> GetCategoryTreeAsync() => _repo.GetCategoryTreeAsync();
    }

}
