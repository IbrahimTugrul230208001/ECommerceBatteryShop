using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerceBatteryShop.DataAccess.Concrete;

public class AddressRepository : IAddressRepository
{
    private readonly BatteryShopContext _ctx;
    private readonly ILogger<AddressRepository> _logger;

    public AddressRepository(BatteryShopContext ctx, ILogger<AddressRepository> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Address>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        return await _ctx.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Title)
            .ToListAsync(ct);
    }

    public async Task<Address?> GetByIdAsync(int userId, int addressId, CancellationToken ct = default)
    {
        return await _ctx.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);
    }

    public async Task<Address> AddAsync(Address address, CancellationToken ct = default)
    {
        var hasAddress = await _ctx.Addresses.AnyAsync(a => a.UserId == address.UserId, ct);
        if (!hasAddress)
        {
            address.IsDefault = true;
        }

        _ctx.Addresses.Add(address);
        await _ctx.SaveChangesAsync(ct);
        _logger.LogInformation("Address {AddressId} added for user {UserId}", address.Id, address.UserId);
        return address;
    }

    public async Task<Address?> UpdateAsync(Address address, CancellationToken ct = default)
    {
        var existing = await _ctx.Addresses.FirstOrDefaultAsync(a => a.Id == address.Id && a.UserId == address.UserId, ct);
        if (existing is null)
        {
            _logger.LogWarning("Attempted to update address {AddressId} for user {UserId}, but it was not found", address.Id, address.UserId);
            return null;
        }

        existing.Title = address.Title;
        existing.Name = address.Name;
        existing.Surname = address.Surname;
        existing.PhoneNumber = address.PhoneNumber;
        existing.FullAddress = address.FullAddress;
        existing.City = address.City;
        existing.State = address.State;
        existing.Neighbourhood = address.Neighbourhood;
        existing.IsDefault = address.IsDefault;

        await _ctx.SaveChangesAsync(ct);
        _logger.LogInformation("Address {AddressId} updated for user {UserId}", address.Id, address.UserId);
        return existing;
    }

    public async Task<bool> SetDefaultAsync(int userId, int addressId, CancellationToken ct = default)
    {
        await using var transaction = await _ctx.Database.BeginTransactionAsync(ct);

        var target = await _ctx.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);
        if (target is null)
        {
            _logger.LogWarning("Attempted to set default address {AddressId} for user {UserId}, but it was not found", addressId, userId);
            await transaction.RollbackAsync(ct);
            return false;
        }

        await _ctx.Addresses
            .Where(a => a.UserId == userId && a.Id != addressId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

        target.IsDefault = true;
        await _ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Address {AddressId} set as default for user {UserId}", addressId, userId);
        return true;
    }
}
