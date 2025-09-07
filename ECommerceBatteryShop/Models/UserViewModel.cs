namespace ECommerceBatteryShop.Models
{
    public class UserViewModel
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? NewPassword { get; set; }
        public string? NewPasswordAgain { get; set; }
        public string? VerificationCode { get; set; }
    }
}
