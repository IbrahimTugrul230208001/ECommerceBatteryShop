namespace ECommerceBatteryShop.DataAccess.Abstract
{
    public interface IInventoryRepository
    {
        /// <summary>Search products by id or name, returning current stock quantity. Empty term → empty.</summary>
        Task<IReadOnlyList<(int ProductId, string ProductName, int Quantity)>> SearchAsync(
            string? searchTerm, int take, CancellationToken ct = default);

        /// <summary>Upsert inventory quantities for the given products (ignores unknown product ids).</summary>
        Task UpdateQuantitiesAsync(IReadOnlyCollection<(int ProductId, int Quantity)> updates,
            CancellationToken ct = default);
    }
}
