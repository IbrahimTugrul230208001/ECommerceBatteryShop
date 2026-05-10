using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.DataAccess.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
    public class EvController : Controller
    {
        private readonly ILogger<EvController> _logger;
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;
        private readonly IFavoritesService _favorites;

        public EvController(IProductRepository repo,
            ICurrencyService currency,
            IFavoritesService favorites,
            ILogger<EvController> log)
        {
            _repo = repo;
            _currency = currency;
            _favorites = favorites;
            _logger = log;
        }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            const decimal KdvRate = 0.20m;
            const int perSection = 16;

            ViewData["Title"] = "PilBataryaMarketim | Lityum Pil ve Enerji Depolama Mağazası";
            ViewData["Description"] =
                "PilBataryaMarketim'de Li-ion ve LiFePO4 pil paketleri, BMS koruma devreleri ve enerji depolama sistemleriyle ihtiyaçlarınıza uygun çözümleri keşfedin.";
            ViewData["Keywords"] = "PilBataryaMarketim, Aspilsan, lityum pil, lifepo4 batarya, bms, enerji depolama";
            ViewData["OgImage"] = Url.Content("~/img/pilbataryamarketim.webp");
            ViewData["Canonical"] = Request.GetDisplayUrl();

            // category ids from your DB
            const int LiIonId = 20;
            const int LiPolymerId = 21;
            const int BmsId = 50;
            const int LfpId = 22;
            const int socketsId = 51;
            const int puntaCihazıId = 53;
            const int siliconCablesId = 54;
            const int bandsId = 55;
            const int batteryPackages12vId = 59;
            const int batteryPackages24vId = 60;
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? 41.5m;

            // Ensure this includes ProductCategories (CategoryId is enough; Category.Include not required)
            var plan = new[]
            {
                new { Title = "Lityum 12V Batarya Paketleri", CatId = batteryPackages12vId, CatSlug = "lityum-batarya-paketleri-12v" },
                new { Title = "Lityum 24V Batarya Paketleri", CatId = batteryPackages24vId, CatSlug = "lityum-batarya-paketleri-24v" },
                new { Title = "Lithium Polymer Pil", CatId = LiPolymerId, CatSlug = "lithium-polymer-pil" },
                new { Title = "Punta Cihazları", CatId = puntaCihazıId, CatSlug = "punta-cihazi" },
                new { Title = "Lithium-ion Pil", CatId = LiIonId, CatSlug = "lithium-ion-pil" },
                new { Title = "BMS - Pil Koruma Devresi", CatId = BmsId, CatSlug = "bms-pil-koruma-devresi" },
                new { Title = "LiFePO4 Pil", CatId = LfpId, CatSlug = "lifepo4-pil" },
                new { Title = "Soketler", CatId = socketsId, CatSlug = "soketler" },
                new { Title = "Silikon Kablolar", CatId = siliconCablesId, CatSlug = "silikon-kablolar" },
                new { Title = "Bantlar", CatId = bandsId, CatSlug = "bantlar" }
            };
            var favoriteIds = await LoadFavoriteIdsAsync(ct);

            ProductViewModel Map(Product p)
            {
                var basePrice = (_currency.ConvertUsdToTry(p.Price, fx) + p.ExtraAmount) * (1 + KdvRate);

                // Find the best active discount for this product
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
                    ImageUrl = p.ImageUrl ?? string.Empty,
                    ExtraAmount = p.ExtraAmount,
                    Description = p.Description ?? string.Empty,
                    IsFavorite = favoriteIds.Contains(p.Id),
                    Slug = p.Slug,
                    StockQuantity = p.Inventory?.Quantity ?? 0
                };
            }


            var sections = new List<ProductSectionViewModel>();
            var used = new HashSet<int>();

            foreach (var def in plan)
            {
                var raw = await _repo.BringProductsByCategoryIdAsync(def.CatId, 1, perSection * 2);
                var ps = raw.Items.Where(p => !used.Contains(p.Id)).Take(perSection).ToList();
                foreach (var p in ps) used.Add(p.Id);

                if (ps.Count > 0)
                    sections.Add(new ProductSectionViewModel
                    {
                        Title = def.Title,
                        AllLink = $"/Urun/{def.CatSlug}",
                        Products = ps.Select(Map).ToList()
                    });
            }

            return View(sections);

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
        }

        public IActionResult Gizlilik()
        {
            return View();
        }

        public IActionResult Iade()
        {
            return View();
        }

        public IActionResult Cerezler()
        {
            return View();
        }

        public IActionResult Hakkimizda()
        {
            return View();
        }
    }
}
