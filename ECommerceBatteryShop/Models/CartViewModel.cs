using System.Collections.Generic;
using System.Linq;

namespace ECommerceBatteryShop.Models;

public class CartItemViewModel
{
    public int ProductId { get; set; }
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public bool CookiesDisabled { get; set; }
    public string? CookieMessage { get; set; }
}
