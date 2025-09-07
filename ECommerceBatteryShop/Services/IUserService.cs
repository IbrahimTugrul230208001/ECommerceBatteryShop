namespace ECommerceBatteryShop.Services
{
    public interface IUserService
    {
        string Email { get; set; }
        string VerificationCode { get; set; }
        string Password { get; set; }
        int UserId { get; set; }
    }
}
