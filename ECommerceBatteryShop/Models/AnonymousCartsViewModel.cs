using System.Collections.Generic;

namespace ECommerceBatteryShop.Models;

public class AnonymousCartItemViewModel
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class AnonymousCartSummary
{
    public int CartId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? AnonId { get; set; }
    public string? UserEmail { get; set; }
    public bool IsAnonymous => AnonId != null;
    public DateTime CreatedAt { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalValue { get; set; }
    public List<AnonymousCartItemViewModel> Items { get; set; } = new();
}

public class AnonymousCartsViewModel
{
    public List<AnonymousCartSummary> Carts { get; set; } = new();
    public int TotalCartCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
