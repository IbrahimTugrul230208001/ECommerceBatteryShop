using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;
}
