using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class ChangePasswordViewModel
{
 [Required(ErrorMessage = "Mevcut þifre gereklidir.")]
 [DataType(DataType.Password)]
 public string CurrentPassword { get; set; } = string.Empty;

 [Required(ErrorMessage = "Yeni þifre gereklidir.")]
 [MinLength(6, ErrorMessage = "Þifre en az 6 karakter olmalýdýr.")]
 [DataType(DataType.Password)]
 public string NewPassword { get; set; } = string.Empty;

 [Required(ErrorMessage = "Þifre tekrarý gereklidir.")]
 [DataType(DataType.Password)]
 [Compare("NewPassword", ErrorMessage = "Þifreler eþleþmiyor.")]
 public string ConfirmPassword { get; set; } = string.Empty;
}
