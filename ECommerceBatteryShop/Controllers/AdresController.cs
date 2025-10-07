using System.Security.Claims;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers;

[Authorize]
public class AdresController : Controller
{
    private readonly IAddressRepository _addressRepository;
    private readonly ILogger<AdresController> _logger;

    public AdresController(IAddressRepository addressRepository, ILogger<AdresController> logger)
    {
        _addressRepository = addressRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var addresses = await _addressRepository.GetByUserAsync(userId.Value, ct);
        return PartialView("_AddressListPartial", addresses.Select(MapToViewModel).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(AddressInputModel model, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            var addresses = await _addressRepository.GetByUserAsync(userId.Value, ct);
            return PartialView("_AddressListPartial", addresses.Select(MapToViewModel).ToList());
        }

        var shouldSetDefault = model.IsDefault;
        Address? saved;

        if (model.Id.HasValue)
        {
            var updated = await _addressRepository.UpdateAsync(new Address
            {
                Id = model.Id.Value,
                UserId = userId.Value,
                Title = model.Title.Trim(),
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                FullAddress = model.FullAddress.Trim(),
                City = model.City.Trim(),
                State = model.State.Trim(),
                Neighbourhood = model.Neighbourhood.Trim(),
                IsDefault = model.IsDefault
            }, ct);

            if (updated is null)
            {
                return NotFound();
            }

            saved = updated;
        }
        else
        {
            var created = await _addressRepository.AddAsync(new Address
            {
                UserId = userId.Value,
                Title = model.Title.Trim(),
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                FullAddress = model.FullAddress.Trim(),
                City = model.City.Trim(),
                State = model.State.Trim(),
                Neighbourhood = model.Neighbourhood.Trim(),
                IsDefault = model.IsDefault
            }, ct);

            saved = created;
            shouldSetDefault = created.IsDefault || shouldSetDefault;
        }

        if (saved is not null && shouldSetDefault)
        {
            await _addressRepository.SetDefaultAsync(userId.Value, saved.Id, ct);
        }

        var refreshed = await _addressRepository.GetByUserAsync(userId.Value, ct);
        if (!refreshed.Any(a => a.IsDefault) && refreshed.Count > 0)
        {
            var fallback = refreshed.First();
            await _addressRepository.SetDefaultAsync(userId.Value, fallback.Id, ct);
            refreshed = await _addressRepository.GetByUserAsync(userId.Value, ct);
        }

        return PartialView("_AddressListPartial", refreshed.Select(MapToViewModel).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _addressRepository.SetDefaultAsync(userId.Value, id, ct);
        if (!result)
        {
            return NotFound();
        }

        var addresses = await _addressRepository.GetByUserAsync(userId.Value, ct);
        return PartialView("_AddressListPartial", addresses.Select(MapToViewModel).ToList());
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        if (int.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        _logger.LogWarning("Could not parse user id claim value {Claim}", claim.Value);
        return null;
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _addressRepository.DeleteAsync(id, ct);
        if (!result)
        {
            return NotFound();
        }
        var addresses = await _addressRepository.GetByUserAsync(userId.Value, ct);
        if (!addresses.Any(a => a.IsDefault) && addresses.Count > 0)
        {
            var fallback = addresses.First();
            await _addressRepository.SetDefaultAsync(userId.Value, fallback.Id, ct);
            addresses = await _addressRepository.GetByUserAsync(userId.Value, ct);
        }
        return PartialView("_AddressListPartial", addresses.Select(MapToViewModel).ToList());
    }
    private static AddressViewModel MapToViewModel(Address address)
    {
        return new AddressViewModel
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
}
