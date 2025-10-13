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
    // 3DS callback endpoint (server-to-server or browser POST)
    public string? ThreeDSCallbackUrl { get; set; }
}
public sealed class IyzicoDefaults
{
    // Iyzipay expects English country names like "Turkey"
    public string Country { get; init; } = "Turkey";
}
