using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.DataAccess.Abstract;

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> GetByUserAsync(int userId, CancellationToken ct = default);
    Task<Address?> GetByIdAsync(int userId, int addressId, CancellationToken ct = default);
    Task<Address> AddAsync(Address address, CancellationToken ct = default);
    Task<Address?> UpdateAsync(Address address, CancellationToken ct = default);
    Task<bool> SetDefaultAsync(int userId, int addressId, CancellationToken ct = default);
}
