using System;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ECommerceBatteryShop.Domain.Entities;
using System.Text.RegularExpressions;

namespace ECommerceBatteryShop.Controllers
{
    public class UrunController : Controller
    {
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;
        private readonly ILogger<UrunController> _log;
        private readonly IFavoritesService _favorites;

        public UrunController(IProductRepository repo, ICurrencyService currency, ILogger<UrunController> log, IFavoritesService favorites)
        {
            _repo = repo; _currency = currency; _log = log; _favorites = favorites;

        }

        public async Task<IActionResult> Index(string? search, string? q, int? categoryId,
                                         decimal? minPrice, decimal? maxPrice,
                                         int page = 1,
                                         CancellationToken ct = default)
        {
            var term = search ?? q;
            const decimal KdvRate = 0.20m;
            var favoriteIds = await LoadFavoriteIdsAsync(ct);

            var contextTitle = "Dayily Enerji Ürünleri";
            if (!string.IsNullOrWhiteSpace(term))
            {
                contextTitle = $"\"{term}\" için Arama Sonuçları";
            }
            else if (categoryId.HasValue && categoryId > 0)
            {
                contextTitle = "Kategori Ürünleri";
            }

            ViewData["Title"] = $"{contextTitle} | Dayily Enerji";
            ViewData["Description"] = !string.IsNullOrWhiteSpace(term)
                ? $"Dayily Enerji'de \"{term}\" aramasıyla Li-ion ve LiFePO4 pil çeşitlerini, BMS çözümlerini ve enerji depolama ekipmanlarını inceleyin."
                : "Dayily Enerji'nin Li-ion pil, LiFePO4 batarya, BMS ve enerji depolama ürünlerini filtreleyerek keşfedin.";
            ViewData["Keywords"] = "lityum pil ürünleri, lifepo4 batarya, bms devresi, enerji depolama mağazası";
            ViewData["Canonical"] = Request.GetDisplayUrl();
            ViewData["OgImage"] = Url.Content("~/img/dayı_amber_banner.jpg");

            var rate = await _currency.GetCachedUsdTryAsync(ct);
            decimal fx = rate ?? 41.5m;
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion unavailable; showing USD.";
                _log.LogWarning("USD→TRY unavailable; using USD display.");
            }
          
            // --- PRICE FILTERING ---
            // Inputs are given in the display currency -> convert back to USD for filtering source prices.
            decimal ? minUsd = minPrice.HasValue ? Math.Max(0, minPrice.Value / fx) : null;
            decimal? maxUsd = maxPrice.HasValue ? Math.Max(0, maxPrice.Value / fx) : null;
            if (minUsd.HasValue && maxUsd.HasValue && minUsd > maxUsd)
                (minUsd, maxUsd) = (maxUsd, minUsd); // normalize swapped inputs

            const int PageSize = 30;
            var currentPage = page <= 0 ? 1 : page;

            async Task<(IReadOnlyList<Product> Items, int TotalCount)> LoadPageAsync(int targetPage)
            {
                if (!string.IsNullOrWhiteSpace(term))
                {
                    return await _repo.ProductSearchResultAsync(term, targetPage, PageSize, minUsd, maxUsd, ct);
                }

                if (categoryId.HasValue && categoryId > 0)
                {
                    return await _repo.BringProductsByCategoryIdAsync(categoryId.Value, targetPage, PageSize, minUsd, maxUsd, ct);
                }

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

            var mapped = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = (_currency.ConvertUsdToTry(p.Price /* USD */, fx) + p.ExtraAmount)* (1 + KdvRate), // displayed in TRY or USD
                Rating = p.Rating,
                ImageUrl = p.ImageUrl,
                IsFavorite = favoriteIds.Contains(p.Id),
                StockQuantity = p.Inventory?.Quantity ?? 0
            }).OrderBy(p=>p.Id).ToList();

            // for the view to persist current filters & "clear" button state
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.HasFilter = !string.IsNullOrWhiteSpace(term)
                                || (categoryId.HasValue && categoryId > 0)
                                || minPrice.HasValue || maxPrice.HasValue;
            ViewBag.SearchTerm = term;
            ViewBag.CategoryId = categoryId;
            ViewBag.CurrentPage = currentPage;
            
            var vm = new ProductIndexViewModel
            {
                Products = mapped,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                PageSize = PageSize,
                TotalCount = totalCount
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
        [HttpGet] // optional
        public async Task<IActionResult> Detaylar(int id, CancellationToken ct = default)
        {
            var product = await _repo.GetProductAsync(id, ct);
            if (product is null) return NotFound();

            const decimal KdvRate = 0.20m;
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? 41.5m;
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
                productDescription = $"{product.Name} ürününü Dayily Enerji'de uygun fiyatlı lityum pil ve enerji depolama çözümleriyle keşfedin.";
            }

            var productImage = string.IsNullOrWhiteSpace(product.ImageUrl)
                ? Url.Content("~/img/placeholder-image.svg")
                : product.ImageUrl;

            ViewData["Title"] = $"{product.Name} | Dayily Enerji";
            ViewData["Description"] = productDescription;
            ViewData["Canonical"] = Request.GetDisplayUrl();
            ViewData["OgImage"] = productImage;
            ViewData["Keywords"] = $"{product.Name}, lityum pil, enerji depolama";

            var vm = new ProductDetailsViewModel
            {
                product = new ProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = (_currency.ConvertUsdToTry(product.Price, fx) + product.ExtraAmount) * (1 + KdvRate),
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
                    .Select(p => new ProductViewModel
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Price = (_currency.ConvertUsdToTry(p.Price, fx) + p.ExtraAmount) * (1 + KdvRate),
                        Rating = p.Rating,
                        ImageUrl = p.ImageUrl ?? string.Empty,
                        IsFavorite = favoriteIds.Contains(p.Id),
                        StockQuantity = p.Inventory?.Quantity ?? 0
                    }).ToList()
            };

            return View("Detaylar", vm); // full view under _Layout
        }
        [HttpGet("/Urun/Search")]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct = default)
        {
            var productData = await _repo.ProductSearchPairsAsync(q ?? string.Empty, ct);
            var vm = productData
                .Select(p => new ProductPredictionDto(p.Id, p.Name))
                .ToList();

            return PartialView("_ProductPredictions", vm);
        }


    }

}
