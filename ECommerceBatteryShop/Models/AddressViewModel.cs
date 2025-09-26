using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public class AddressViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Neighbourhood { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public static AddressViewModel FromEntity(Address entity)
        {
            return new AddressViewModel
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Title = entity.Title,
                Name = entity.Name,
                Surname = entity.Surname,
                PhoneNumber = entity.PhoneNumber,
                FullAddress = entity.FullAddress,
                City = entity.City,
                State = entity.State,
                Country = entity.Country,
                Neighbourhood = entity.Neighbourhood,
                IsDefault = entity.IsDefault
            };
        }
    }
}
