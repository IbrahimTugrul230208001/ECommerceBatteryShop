
using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class GuestCheckoutViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Surname { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string State { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Neighbourhood { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string FullAddress { get; set; } = string.Empty;
}
