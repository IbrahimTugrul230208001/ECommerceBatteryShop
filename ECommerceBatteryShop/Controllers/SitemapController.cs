using ECommerceBatteryShop.DataAccess.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;

namespace ECommerceBatteryShop.Controllers;

public class SitemapController : Controller
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<SitemapController> _logger;

    public SitemapController(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ILogger<SitemapController> logger)
    {
        _productRepository = productRepository;
   _categoryRepository = categoryRepository;
        _logger = logger;
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "User-Agent")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
        
            var urlset = new XElement(ns + "urlset");

   // 1. Static pages (high priority)
          AddUrl(urlset, ns, baseUrl, "/", "1.0", "daily");
     AddUrl(urlset, ns, baseUrl, "/Ev/Hakkimizda", "0.8", "monthly");
 AddUrl(urlset, ns, baseUrl, "/Ev/Gizlilik", "0.5", "yearly");
       AddUrl(urlset, ns, baseUrl, "/Ev/Cerezler", "0.5", "yearly");
   AddUrl(urlset, ns, baseUrl, "/Ev/Iade", "0.6", "monthly");
    AddUrl(urlset, ns, baseUrl, "/Urun/Index", "0.9", "daily");

       // 2. All categories with their slugs: /Urun/{category-slug}
 var categories = await _categoryRepository.GetCategoryTreeAsync();
          var allCategories = FlattenCategories(categories);
 
            foreach (var category in allCategories)
  {
          if (!string.IsNullOrWhiteSpace(category.Slug))
    {
        var categoryUrl = $"/Urun/{Uri.EscapeDataString(category.Slug)}";
  AddUrl(urlset, ns, baseUrl, categoryUrl, "0.8", "weekly");
       }
     }

            // 3. All products with their slugs: /{product-slug}
            const int pageSize = 500;
            int page = 1;
   bool hasMore = true;

            while (hasMore)
   {
         var (products, totalCount) = await _productRepository.GetMainPageProductsAsync(
       page, 
     pageSize,
        minUsd: null,
             maxUsd: null,
        ct: ct
 );

                foreach (var product in products)
      {
   if (!string.IsNullOrWhiteSpace(product.Slug))
   {
     var productUrl = $"/{Uri.EscapeDataString(product.Slug)}";
   // Products change more frequently, higher priority for in-stock items
            var priority = product.Inventory?.Quantity > 0 ? "0.9" : "0.7";
      AddUrl(urlset, ns, baseUrl, productUrl, priority, "daily");
          }
      }

 hasMore = page * pageSize < totalCount;
           page++;
            }

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
 urlset
        );

         var xml = document.ToString();
   return Content(xml, "application/xml", Encoding.UTF8);
    }
        catch (Exception ex)
        {
     _logger.LogError(ex, "Error generating sitemap");
      return StatusCode(500, "Error generating sitemap");
 }
    }

    private static void AddUrl(
  XElement urlset, 
   XNamespace ns, 
      string baseUrl, 
   string path, 
   string priority, 
        string changeFreq)
    {
        var url = new XElement(ns + "url",
  new XElement(ns + "loc", $"{baseUrl}{path}"),
            new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd")),
            new XElement(ns + "changefreq", changeFreq),
            new XElement(ns + "priority", priority)
        );

      urlset.Add(url);
    }

private static List<ECommerceBatteryShop.Domain.Entities.Category> FlattenCategories(
        List<ECommerceBatteryShop.Domain.Entities.Category> categories)
    {
        var result = new List<ECommerceBatteryShop.Domain.Entities.Category>();
        
   foreach (var category in categories)
        {
    result.Add(category);
     
    if (category.SubCategories != null && category.SubCategories.Any())
 {
         result.AddRange(FlattenCategories(category.SubCategories.ToList()));
        }
        }
        
  return result;
    }
}
