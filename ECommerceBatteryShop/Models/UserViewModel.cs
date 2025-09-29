namespace ECommerceBatteryShop.Models
{
    public class UserViewModel
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? VerificationCode { get; set; }
    }
}
