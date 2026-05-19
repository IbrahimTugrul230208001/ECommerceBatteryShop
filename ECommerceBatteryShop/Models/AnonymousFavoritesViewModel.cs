using System.Collections.Generic;
using System;

namespace ECommerceBatteryShop.Models;

public class AnonymousFavoriteItemViewModel
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
}

public class AnonymousFavoriteSummary
{
    public int FavoriteId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? AnonId { get; set; }
    public string? UserEmail { get; set; }
    public bool IsAnonymous => AnonId != null;
    public DateTime CreatedAt { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalValue { get; set; }
    public List<AnonymousFavoriteItemViewModel> Items { get; set; } = new();
}

public class AnonymousFavoritesViewModel
{
    public List<AnonymousFavoriteSummary> Favorites { get; set; } = new();
    public int TotalFavoriteCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
