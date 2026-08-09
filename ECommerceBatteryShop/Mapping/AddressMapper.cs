using ECommerceBatteryShop.DataAccess.Entities;
using ECommerceBatteryShop.Models;

namespace ECommerceBatteryShop.Mapping;

/// <summary>
/// Single Address → AddressViewModel projection. Previously duplicated identically in
/// Hesap/Sepet/Adres controllers.
/// </summary>
public static class AddressMapper
{
    public static AddressViewModel ToViewModel(this Address address) => new()
    {
        Id = address.Id,
        UserId = address.UserId,
        Title = address.Title,
        Name = address.Name,
        Surname = address.Surname,
        PhoneNumber = address.PhoneNumber,
        FullAddress = address.FullAddress,
        City = address.City,
        State = address.State,
        Neighbourhood = address.Neighbourhood,
        IsDefault = address.IsDefault
    };
}
