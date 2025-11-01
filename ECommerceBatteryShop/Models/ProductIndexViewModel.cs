using System;
using System.Collections.Generic;

namespace ECommerceBatteryShop.Models
{
    public class ProductIndexViewModel
    {
        public IReadOnlyList<ProductViewModel> Products { get; init; } = Array.Empty<ProductViewModel>();
        public string? SearchQuery { get; init; }
        public string? CategoryFilter { get; init; }
        public int CurrentPage { get; init; }

        public int TotalPages { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }
    }
}
