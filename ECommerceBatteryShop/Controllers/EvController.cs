using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
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

            ViewData["Title"] = "Dayily Enerji | Lityum Pil ve Enerji Depolama Mağazası";
            ViewData["Description"] = "Dayily Enerji'de Li-ion ve LiFePO4 pil paketleri, BMS koruma devreleri ve enerji depolama sistemleriyle ihtiyaçlarınıza uygun çözümleri keşfedin.";
            ViewData["Keywords"] = "dayily enerji, lityum pil, lifepo4 batarya, bms, enerji depolama";
            ViewData["OgImage"] = Url.Content("~/img/dayı_amber_banner.jpg");
            ViewData["Canonical"] = Request.GetDisplayUrl();

            // category ids from your DB
            const int LiIonId = 20;
            const int BmsId = 50;
            const int LfpId = 22;
            const int socketsId = 51;
            const int puntaCihazıId = 53;
            const int siliconCablesId = 54;
            const int bandsId= 55;
            const int batteryPackages12vId = 59;
            const int batteryPackages24vId = 60;
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? 41.5m;

            // Ensure this includes ProductCategories (CategoryId is enough; Category.Include not required)

            var favoriteIds = await LoadFavoriteIdsAsync(ct);

            ProductViewModel Map(Product p) => new()
            {
                Id = p.Id,
                Name = p.Name,
                Price = _currency.ConvertUsdToTry(p.Price, fx) * (1 + KdvRate),
                Rating = p.Rating,
                ImageUrl = p.ImageUrl ?? string.Empty,
                ExtraAmount = p.ExtraAmount,
                Description = p.Description ?? string.Empty,
                IsFavorite = favoriteIds.Contains(p.Id),
                StockQuantity = p.Inventory?.Quantity ?? 0
            };
            var plan = new[]
            {
    new { Title = "Punta Cihazları", CatId = puntaCihazıId },
    new { Title = "Lithium-ion Pil",          CatId = LiIonId },
    new { Title = "BMS - Pil Koruma Devresi", CatId = BmsId   },
    new { Title = "LiFePO4 - Silindirik Pil", CatId = LfpId   },
    new {Title = "LifePO4 12V Batarya Paketleri", CatId = batteryPackages12vId },
    new {Title = "LifePO4 24V Batarya Paketleri", CatId = batteryPackages24vId },
    new { Title = "Soketler", CatId = socketsId },
    new { Title = "Silikon Kablolar" , CatId  = siliconCablesId },
    new {Title = "Bantlar" , CatId = bandsId }

};

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
                        AllLink = $"/Urun?categoryId={def.CatId}",
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
        public IActionResult MesafeliSatis()
        {
            return RedirectToAction("MesafeliSatis", "Sepet");
        }
    }
}
