using ECommerceBatteryShop.DataAccess.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartRepository _repo;

        public CartController(ICartRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1, CancellationToken ct = default)
        {
            const int userId = 1; // TODO: replace with actual authenticated user id
            var count = await _repo.AddToCartAsync(userId, productId, quantity, ct);
            return PartialView("_CartCount", count);
        }
    }
}
