using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Options;

public class IyzicoOptions
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    public string? CallbackUrl { get; set; }
}
public sealed class IyzicoDefaults
{
    public string Country { get; init; } = "Türkiye";
}