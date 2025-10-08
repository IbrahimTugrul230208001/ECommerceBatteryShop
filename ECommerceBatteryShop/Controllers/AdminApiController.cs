using ECommerceBatteryShop.Authentication;
using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ECommerceBatteryShop.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(AuthenticationSchemes = ApiKeyAuthExtensions.Scheme, Policy = ApiKeyAuthExtensions.PolicyCatalogWrite)]
public class AdminApiController : ControllerBase
{
    private readonly BatteryShopContext _context;
    private readonly IWebHostEnvironment _env;

    public AdminApiController(BatteryShopContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public sealed class UpdateStockRequest
    {
        [Required]
        public int ProductId { get; set; }
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public sealed class BulkUpdateStockRequest
    {
        [MinLength(1)]
        public List<UpdateStockItem> Items { get; set; } = new();
        public sealed class UpdateStockItem
        {
            [Required]
            public int ProductId { get; set; }
            [Range(0, int.MaxValue)]
            public int Quantity { get; set; }
        }
    }

    public sealed class UpdatePriceRequest
    {
        [Required]
        public int ProductId { get; set; }
        [Range(0.01, 100000)]
        public decimal Price { get; set; }
    }

    public sealed class CreateProductRequest
    {
        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;
        [Range(0.01, 100000)]
        public decimal Price { get; set; }
        [MaxLength(4000)]
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public IFormFile? Image { get; set; }
        public IFormFile? Document { get; set; }
    }

    [HttpPost("stock")]
    public async Task<IActionResult> UpdateStock([FromBody] UpdateStockRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var product = await _context.Products
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product is null) return NotFound($"Product {request.ProductId} was not found.");

        if (product.Inventory is null)
        {
            product.Inventory = new Inventory
            {
                ProductId = product.Id,
                Quantity = request.Quantity,
                LastUpdated = DateTime.UtcNow
            };
            _context.Inventories.Add(product.Inventory);
        }
        else
        {
            product.Inventory.Quantity = request.Quantity;
            product.Inventory.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { productId = product.Id, quantity = product.Inventory.Quantity });
    }

    [HttpPost("stock/bulk")]
    public async Task<IActionResult> BulkUpdateStock([FromBody] BulkUpdateStockRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (request.Items.Count == 0) return BadRequest("No items provided.");

        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Include(p => p.Inventory)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null) continue;

            if (product.Inventory is null)
            {
                product.Inventory = new Inventory
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    LastUpdated = now
                };
                _context.Inventories.Add(product.Inventory);
            }
            else
            {
                product.Inventory.Quantity = item.Quantity;
                product.Inventory.LastUpdated = now;
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { updated = request.Items.Count });
    }

    [HttpPost("price")]
    public async Task<IActionResult> UpdatePrice([FromBody] UpdatePriceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null) return NotFound($"Product {request.ProductId} was not found.");

        product.Price = request.Price;
        await _context.SaveChangesAsync(ct);

        return Ok(new { productId = product.Id, price = product.Price });
    }

    [HttpPost("products")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> CreateProduct([FromForm] CreateProductRequest request, CancellationToken ct)
    {
        // Basic validations
        if (request.Image is not null)
        {
            var contentType = request.Image.ContentType?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/"))
            {
                ModelState.AddModelError(nameof(request.Image), "Image must be a valid image.");
            }
        }
        if (request.Document is not null)
        {
            var ext = Path.GetExtension(request.Document.FileName);
            var isPdf = string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.Document.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
            if (!isPdf)
            {
                ModelState.AddModelError(nameof(request.Document), "Document must be a PDF (.pdf).");
            }
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            Description = request.Description?.Trim(),
            Rating = 0,
            ExtraAmount = 0
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync(ct); // ensure product.Id is available for linking

        // File storage
        var webRoot = _env.WebRootPath ?? string.Empty;
        var imagesFolder = Path.Combine(webRoot, "img");
        var documentsFolder = Path.Combine(webRoot, "doc");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(documentsFolder);

        // Save document
        if (request.Document is not null && request.Document.Length > 0)
        {
            var docFile = CreateDocumentFileName(product.Name, ".pdf");
            var docPath = Path.Combine(documentsFolder, docFile);
            await using var stream = System.IO.File.Create(docPath);
            await request.Document.CopyToAsync(stream, ct);
            product.DocumentUrl = docFile;
        }

        // Save image
        if (request.Image is not null && request.Image.Length > 0)
        {
            var ext = Path.GetExtension(request.Image.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                var ctType = request.Image.ContentType?.ToLowerInvariant();
                ext = ctType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };
            }
            var imgFile = CreateImageFileName(product.Name, ext);
            var imgPath = Path.Combine(imagesFolder, imgFile);
            await using var imgStream = System.IO.File.Create(imgPath);
            await request.Image.CopyToAsync(imgStream, ct);
            product.ImageUrl = imgFile;
        }

        // Category link (optional)
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId.Value, ct);
            if (categoryExists)
            {
                _context.ProductCategories.Add(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = request.CategoryId.Value
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        return Created($"/api/admin/products/{product.Id}", new
        {
            productId = product.Id,
            name = product.Name,
            price = product.Price,
            imageUrl = product.ImageUrl,
            documentUrl = product.DocumentUrl,
            categoryId = request.CategoryId
        });
    }

    private static string CreateImageFileName(string productName, string extension)
    {
        extension = string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.') ? ".jpg" : extension.ToLowerInvariant();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(productName.Where(c => !char.IsWhiteSpace(c) && !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "urun";
        return sanitized + extension;
    }

    private static string CreateDocumentFileName(string productName, string extension)
    {
        extension = string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.') ? ".pdf" : extension.ToLowerInvariant();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(productName.Where(c => !char.IsWhiteSpace(c) && !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "urun";
        return sanitized + extension;
    }
}
