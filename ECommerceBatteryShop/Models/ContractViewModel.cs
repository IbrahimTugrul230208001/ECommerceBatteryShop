using ECommerceBatteryShop.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models
{
    public sealed class ContractViewModel
    {
        // --- Alıcı ---
        [Required] public string BuyerName { get; set; } = "";
        [Required] public string BuyerAddress { get; set; } = "";
        [Required] public string BuyerPhone { get; set; } = "";
        [Required, EmailAddress] public string BuyerEmail { get; set; } = "";

        // --- (Opsiyonel) Sipariş Veren (Alıcıdan farklı ise doldur) ---
        public string? OrdererName { get; set; }
        public string? OrdererAddress { get; set; }
        public string? OrdererPhone { get; set; }
        [EmailAddress] public string? OrdererEmail { get; set; }

        // --- Satın alma kalemleri ---
        [MinLength(1)] public List<OrderItem> Items { get; set; } = new();

        // --- Ücret kalemleri ---
        [Range(0, double.MaxValue)] public decimal ShippingFee { get; set; } = 0m;

        // Hesaplamalar
        public decimal Subtotal => Items.Sum(i => i.Total);
        public decimal GrandTotal => Subtotal + ShippingFee;

        // --- Fatura bilgileri ---
        [Required] public string InvoiceTitle { get; set; } = "";          // Ad-Soyad/Unvan
        [Required] public string InvoiceTax { get; set; } = "";            // VD/VKN ya da TCKN
        [Required] public string InvoiceAddress { get; set; } = "";
        [Required] public string InvoicePhone { get; set; } = "";
        [Required, EmailAddress] public string InvoiceEmail { get; set; } = "";

        // --- İade kanalı (site içi path) ---
        [Required] public string ReturnPath { get; set; } = "/hesabim/iade";

        // --- Meta (opsiyonel) ---
        public DateTime OrderDate { get; set; } = DateTime.Now;            // Görselde kullanılmıyor ama faydalı
    }
    public sealed class OrderItem
    {
        [Required] public string Description { get; set; } = "";
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal UnitPrice { get; set; }

        public decimal Total => UnitPrice * Quantity;
    }
}
