namespace ECommerceBatteryShop.Services
{
    public class ExchangerateHostModels
    {
        public sealed class LatestDto
        {
            public Dictionary<string, decimal>? Rates { get; set; }
        }
    }
}
