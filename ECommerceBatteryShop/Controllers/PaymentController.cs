using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
    public class PaymentController : Controller
    {
        public readonly ICartService _cartService;
        public PaymentController(ICartService cartService)
        {
            _cartService = cartService;
        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> InsertOrder()
        {
            CartOwner owner;
            var userId = int.Parse(User.FindFirst("sub")!.Value);
            owner = CartOwner.FromUser(userId);

            var cart = await _cartService.GetAsync(owner);
            if (cart is null || !cart.Items.Any())
                return RedirectToAction("Index", "Cart");
            return View(cart);
        }
    }
}
