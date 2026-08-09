using System.Security.Claims;
using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers;

public class MisafirController : Controller
{

    [HttpGet]
    public IActionResult Index()
    {
        return View(new GuestCheckoutViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(GuestCheckoutViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Persist minimal guest data in a cookie for checkout
        Response.Cookies.Append("GUEST_INFO", System.Text.Json.JsonSerializer.Serialize(new
        {
            model.Name,
            model.Surname,
            model.Email,
            model.Phone,
            model.City,
            model.State,
            model.Neighbourhood,
            model.FullAddress
        }), new CookieOptions
        {
            IsEssential = true,
            HttpOnly = false,
            Secure = false,
            Expires = DateTimeOffset.UtcNow.AddHours(4)
        });

        return RedirectToAction("Siparis", "Sepet");
    }
}
