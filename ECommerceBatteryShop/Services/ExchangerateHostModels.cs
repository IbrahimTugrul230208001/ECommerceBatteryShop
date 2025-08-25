namespace ECommerceBatteryShop.Services
{
    public class ExchangerateHostModels
    {
        public sealed class LatestDto
        {
            public Dictionary<string, decimal>? Rates { get; set; }
            public string? Base { get; set; }
            public string? Date { get; set; }
        }
    }
}
