using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ECommerceBatteryShop.Models;

public class AdminProductEntryViewModel
{
    [Display(Name = "Ürün Görseli")]
    [Required(ErrorMessage = "Lütfen bir ürün görseli yükleyin.")]
    public IFormFile? Image { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur."), StringLength(120, ErrorMessage = "Ürün adı 120 karakterden uzun olamaz.")]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fiyat zorunludur."), Range(0.01, 100000, ErrorMessage = "Lütfen geçerli bir fiyat girin.")]
    [Display(Name = "Fiyat (₺)")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Açıklama zorunludur."), StringLength(1000, ErrorMessage = "Açıklama 1000 karakterden uzun olamaz.")]
    [Display(Name = "Ürün Açıklaması")]
    public string Description { get; set; } = string.Empty;
}
