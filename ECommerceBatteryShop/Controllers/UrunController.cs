using System;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ECommerceBatteryShop.DataAccess.Entities;
using System.Text.RegularExpressions;

namespace ECommerceBatteryShop.Controllers
{
    public class UrunController : Controller
    {
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;
        private readonly ILogger<UrunController> _log;
        private readonly IFavoritesService _favorites;
        private readonly ICategoryRepository _categories;

        public UrunController(
            IProductRepository repo,
            ICurrencyService currency,
            ILogger<UrunController> log,
            IFavoritesService favorites,
            ICategoryRepository categories)
        {
            _repo = repo; _currency = currency; _log = log; _favorites = favorites; _categories = categories;

        }

        [HttpGet("/Urun/{categorySlug}")]
        public async Task<IActionResult> Index(string categorySlug, string? search, string? q, string? categoryPath,
                                         decimal? minPrice, decimal? maxPrice, string? sort,
                                         int page = 1, CancellationToken ct = default)
        {
            string categoryName = string.Empty;
            int? categoryId = null;

            // Resolve category by slug if provided
            if (!string.IsNullOrWhiteSpace(categorySlug))
            {
                var cat = await _categories.GetBySlugAsync(Uri.UnescapeDataString(categorySlug), ct);
                if (cat != null)
                {
                    categoryName = cat.Name;
                    categoryId = cat.Id;
                }
            }

            var term = search ?? q ?? null;
            const decimal KdvRate = 0.20m;
            var favoriteIds = await LoadFavoriteIdsAsync(ct);

            var contextTitle = "Pil Batarya Marketim Ürünleri";
            if (!string.IsNullOrWhiteSpace(term))
            {
                contextTitle = $"\"{term}\" için Arama Sonuçları";
            }
            else if (!string.IsNullOrWhiteSpace(categoryName))
            {
                contextTitle = $"{categoryName} Ürünleri";
            }

            ViewData["Title"] = $"{contextTitle} | Pil Batarya Marketim";
            ViewData["Description"] = !string.IsNullOrWhiteSpace(term)
                ? $"Pil Batarya Marketim'de \"{term}\" aramasıyla Li-ion ve LiFePO4 pil çeşitlerini, BMS çözümlerini ve enerji depolama ekipmanlarını inceleyin."
                : !string.IsNullOrWhiteSpace(categoryName)
                 ? $"Pil Batarya Marketim'in {categoryName} kategorisindeki Li-ion pil, LiFePO4 batarya, BMS ve enerji depolama ürünlerini keşfedin."
                   : "Pil Batarya Marketim'in Li-ion pil, LiFePO4 batarya, BMS ve enerji depolama ürünlerini filtreleyerek keşfedin.";
            ViewData["Keywords"] = "lityum pil ürünleri, lifepo4 batarya, bms devresi, enerji depolama mağazası";
            ViewData["Canonical"] = Request.GetDisplayUrl();
            ViewData["OgImage"] = Url.Content("~/img/dayı_amber_banner.jpg");

            var rate = await _currency.GetCachedUsdTryAsync(ct);
            decimal fx = rate ?? 42m;
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion unavailable; showing USD.";
                _log.LogWarning("USD→TRY unavailable; using USD display.");
            }

            // --- PRICE FILTERING ---
            // Inputs are given in the display currency -> convert back to USD for filtering source prices.
            decimal? minUsd = minPrice.HasValue ? Math.Max(0, minPrice.Value / fx) : null;
            decimal? maxUsd = maxPrice.HasValue ? Math.Max(0, maxPrice.Value / fx) : null;
            if (minUsd.HasValue && maxUsd.HasValue && minUsd > maxUsd)
                (minUsd, maxUsd) = (maxUsd, minUsd); // normalize swapped inputs

            const int PageSize = 28;
            var currentPage = page <= 0 ? 1 : page;

            async Task<(IReadOnlyList<Product> Items, int TotalCount)> LoadPageAsync(int targetPage)
            {
                // Priority 1: Search term
                if (!string.IsNullOrWhiteSpace(term))
                {
                    return await _repo.ProductSearchResultAsync(term, targetPage, PageSize, minUsd, maxUsd, ct);
                }

                // Priority 2: Category filter
                if (categoryId.HasValue)
                {
                    return await _repo.BringProductsByCategoryIdAsync(categoryId.Value, targetPage, PageSize, minUsd, maxUsd, ct);
                }

                // Priority 3: All products
                return await _repo.GetMainPageProductsAsync(targetPage, PageSize, minUsd, maxUsd, ct);
            }

            var result = await LoadPageAsync(currentPage);
            var products = result.Items;
            var totalCount = result.TotalCount;
            var totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)PageSize);

            if (totalCount > 0 && currentPage > totalPages)
            {
                currentPage = totalPages;
                result = await LoadPageAsync(currentPage);
                products = result.Items;
                totalCount = result.TotalCount;
                totalPages = totalCount == 0
                    ? 1
                    : (int)Math.Ceiling(totalCount / (double)PageSize);
            }

            var mapped = products.Select(p =>
            {
                var basePrice = (_currency.ConvertUsdToTry(p.Price, fx) + p.ExtraAmount) * (1 + KdvRate);
                var now = DateTime.UtcNow;
                var activeDiscount = p.Discounts?
                    .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                    .OrderByDescending(d => d.DiscountPercentage)
                    .FirstOrDefault();
                var discountPct = activeDiscount?.DiscountPercentage ?? 0m;
                var finalPrice = discountPct > 0
                    ? Math.Round(basePrice * (1 - discountPct / 100m), 2)
                    : basePrice;

                return new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = finalPrice,
                    OriginalPrice = discountPct > 0 ? basePrice : null,
                    DiscountPercentage = discountPct,
                    Rating = p.Rating,
                    ImageUrl = p.ImageUrl,
                    IsFavorite = favoriteIds.Contains(p.Id),
                    Slug = p.Slug,
                    StockQuantity = p.Inventory?.Quantity ?? 0
                };
            }).ToList();

            if (sort == "asc")
            {
                mapped = mapped.OrderBy(p => p.Price).ToList();
            }
            else if (sort == "desc")
            {
                mapped = mapped.OrderByDescending(p => p.Price).ToList();
            }
            else
            {
                mapped = mapped.OrderBy(p => p.Id).ToList();
            }

            // for the view to persist current filters & "clear" button state
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.HasFilter = !string.IsNullOrWhiteSpace(term)
                                || minPrice.HasValue || maxPrice.HasValue || !string.IsNullOrWhiteSpace(sort);
            ViewBag.SearchTerm = term;
            ViewBag.CurrentPage = currentPage;
            ViewBag.Sort = sort;
            ViewBag.CategoryId = categoryId;

            var vm = new ProductIndexViewModel
            {
                CategoryPath = categoryPath,
                Products = mapped,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                PageSize = PageSize,
                TotalCount = totalCount,
                CategoryFilter = categoryName,
                SearchQuery = term
            };

            return View(vm);



        }

        async Task<HashSet<int>> LoadFavoriteIdsAsync(CancellationToken token)
        {
            FavoriteOwner? owner = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var sub = User.FindFirst("sub")?.Value;
                if (int.TryParse(sub, out var userId))
                {
                    owner = FavoriteOwner.FromUser(userId);
                }
            }
            else
            {
                var anonId = Request.Cookies["ANON_ID"];
                if (!string.IsNullOrWhiteSpace(anonId))
                {
                    owner = FavoriteOwner.FromAnon(anonId);
                }
            }

            if (owner is null)
            {
                return new HashSet<int>();
            }

            var list = await _favorites.GetAsync(owner, createIfMissing: false, token);

            return list is null
                ? new HashSet<int>()
                : new HashSet<int>(list.Items.Select(i => i.ProductId));
        }
        [HttpGet("/{slug}")] // attribute route only; avoid mixing with conventional
        public async Task<IActionResult> Detaylar(string categorySlug, string slug, CancellationToken ct = default)
        {
            var decoded = Uri.UnescapeDataString(slug);
            var product = await _repo.GetProductBySlugAsync(decoded, ct);

            if (product is null) return NotFound();

            const decimal KdvRate = 0.20m;
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? 42m;
            var favoriteIds = await LoadFavoriteIdsAsync(ct);
            var relatedProducts = await _repo.GetLatestProductsAsync();

            var productDescription = product.Description ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(productDescription))
            {
                productDescription = Regex.Replace(productDescription, "<[^>]*>", string.Empty);
                if (productDescription.Length > 160)
                {
                    productDescription = productDescription[..157] + "...";
                }
            }
            else
            {
                productDescription = $"{product.Name} ürününü Pil Batarya Marketim'de uygun fiyatlı lityum pil ve enerji depolama çözümleriyle keşfedin.";
            }

            var productImage = string.IsNullOrWhiteSpace(product.ImageUrl)
                ? Url.Content("~/img/placeholder-image.svg")
                : product.ImageUrl;

            ViewData["Title"] = $"{product.Name} | Pil Batarya Marketim";
            ViewData["Description"] = productDescription;
            ViewData["Canonical"] = Request.GetDisplayUrl();
            ViewData["OgImage"] = productImage;
            ViewData["Keywords"] = $"{product.Name}, lityum pil, enerji depolama";

            // Compute discount for main product
            var mainBasePrice = (_currency.ConvertUsdToTry(product.Price, fx) + product.ExtraAmount) * (1 + KdvRate);
            var now = DateTime.UtcNow;
            var mainActiveDiscount = product.Discounts?
                .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                .OrderByDescending(d => d.DiscountPercentage)
                .FirstOrDefault();
            var mainDiscountPct = mainActiveDiscount?.DiscountPercentage ?? 0m;
            var mainFinalPrice = mainDiscountPct > 0
                ? Math.Round(mainBasePrice * (1 - mainDiscountPct / 100m), 2)
                : mainBasePrice;

            var vm = new ProductDetailsViewModel
            {
                product = new ProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = mainFinalPrice,
                    OriginalPrice = mainDiscountPct > 0 ? mainBasePrice : null,
                    DiscountPercentage = mainDiscountPct,
                    Rating = product.Rating,
                    ImageUrl = product.ImageUrl ?? string.Empty,
                    IsFavorite = favoriteIds.Contains(product.Id),
                    Description = product.Description ?? string.Empty,
                    StockQuantity = product.Inventory?.Quantity ?? 0,
                    AttachmentUrl = product.DocumentUrl ?? string.Empty
                },
                RelatedProducts = relatedProducts
                    .Where(p => p.Id != product.Id)
                    .Take(16)
                    .Select(p =>
                    {
                        var relBasePrice = (_currency.ConvertUsdToTry(p.Price, fx) + p.ExtraAmount) * (1 + KdvRate);
                        var relActiveDiscount = p.Discounts?
                            .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
                            .OrderByDescending(d => d.DiscountPercentage)
                            .FirstOrDefault();
                        var relDiscountPct = relActiveDiscount?.DiscountPercentage ?? 0m;
                        var relFinalPrice = relDiscountPct > 0
                            ? Math.Round(relBasePrice * (1 - relDiscountPct / 100m), 2)
                            : relBasePrice;

                        return new ProductViewModel
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Price = relFinalPrice,
                            OriginalPrice = relDiscountPct > 0 ? relBasePrice : null,
                            DiscountPercentage = relDiscountPct,
                            Rating = p.Rating,
                            ImageUrl = p.ImageUrl ?? string.Empty,
                            IsFavorite = favoriteIds.Contains(p.Id),
                            StockQuantity = p.Inventory?.Quantity ?? 0,
                            Slug = p.Slug
                        };
                    }).ToList()
            };

            return View("Detaylar", vm); // full view under _Layout
        }
        [HttpGet("/Urun/Search")]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct = default)
        {
            var productData = await _repo.ProductSearchPairsAsync(q ?? string.Empty, ct);
            var vm = productData
                .Select(p => new ProductPredictionDto(p.Id, p.Name, p.Slug))
                .ToList();

            return PartialView("_ProductPredictions", vm);
        }


    }

}
