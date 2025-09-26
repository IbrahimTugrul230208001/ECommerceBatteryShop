using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models
{
    public class AddressInputModel
    {
        public int Id { get; set; }

        [Required, MaxLength(128)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Surname { get; set; } = string.Empty;

        [Required, MaxLength(32)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MaxLength(512)]
        public string FullAddress { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string State { get; set; } = string.Empty;

        [MaxLength(128)]
        public string Country { get; set; } = string.Empty;

        [Required, MaxLength(256)]
        public string Neighbourhood { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
    }
}
