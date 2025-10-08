using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ECommerceBatteryShop.Models;

public class AdminProductEntryViewModel : IValidatableObject
{
    [Display(Name = "Ürün ID")]
    public int? ProductId { get; set; }

    [Display(Name = "Ürün Görseli")]
    public IFormFile? Image { get; set; }

    [Display(Name = "Mevcut Görsel")]
    public string? ExistingImageUrl { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur."), StringLength(120, ErrorMessage = "Ürün adı 120 karakterden uzun olamaz.")]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fiyat zorunludur."), Range(0.01, 100000, ErrorMessage = "Lütfen geçerli bir fiyat girin.")]
    [Display(Name = "Fiyat ($)")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Açıklama zorunludur."), StringLength(3000, ErrorMessage = "Açıklama 3000 karakterden uzun olamaz.")]
    [Display(Name = "Ürün Açıklaması")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Ürün Ara")]
    public string? SearchTerm { get; set; }
    [Display(Name= "Kategori Zorunludur")]
    public int CategoryId { get; set; }
    
    public IList<AdminProductSelectionItemViewModel> SearchResults { get; set; } = new List<AdminProductSelectionItemViewModel>();
    public List<CategorySelectionViewModel> Categories { get; set; } = new List<CategorySelectionViewModel>();
    public bool IsEditing => ProductId.HasValue;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ProductId.HasValue && Image is null)
        {
            yield return new ValidationResult("Yeni ürün oluşturmak için lütfen bir görsel yükleyin.", new[] { nameof(Image) });
        }
    }
}

public class AdminProductSelectionItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }
}
