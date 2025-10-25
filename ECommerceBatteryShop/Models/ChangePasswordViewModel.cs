using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class ChangePasswordViewModel
{
 [Required(ErrorMessage = "Mevcut �ifre gereklidir.")]
 [DataType(DataType.Password)]
 public string CurrentPassword { get; set; } = string.Empty;

 [Required(ErrorMessage = "Yeni �ifre gereklidir.")]
 [MinLength(6, ErrorMessage = "�ifre en az 6 karakter olmal�d�r.")]
 [DataType(DataType.Password)]
 public string NewPassword { get; set; } = string.Empty;

 [Required(ErrorMessage = "�ifre tekrar� gereklidir.")]
 [DataType(DataType.Password)]
 [Compare("NewPassword", ErrorMessage = "�ifreler e�le�miyor.")]
 public string ConfirmPassword { get; set; } = string.Empty;
}
