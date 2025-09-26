using System;
using System.Security.Claims;
using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly BatteryShopContext _db;

        public AddressController(BatteryShopContext db)
        {
            _db = db;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new InvalidOperationException("Authenticated user must have an identifier.");
            }

            if (!int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new InvalidOperationException("User identifier is not a valid integer value.");
            }

            return userId;
        }

        private static AddressViewModel ToViewModel(Address entity) => AddressViewModel.FromEntity(entity);

        private PartialViewResult RenderAddressList(IEnumerable<Address> addresses)
        {
            var viewModel = new AddressListViewModel
            {
                ContainerId = "address-list",
                Addresses = addresses
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.Id)
                    .Select(ToViewModel)
                    .ToList()
            };

            return PartialView("~/Views/Address/_AddressList.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Upsert(AddressInputModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = GetUserId();

            Address? entity = null;
            if (model.Id > 0)
            {
                entity = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == model.Id && a.UserId == userId, ct);
                if (entity is null)
                {
                    return NotFound();
                }
            }

            if (entity is null)
            {
                entity = new Address
                {
                    UserId = userId
                };
                _db.Addresses.Add(entity);
            }

            entity.Title = model.Title.Trim();
            entity.Name = model.Name.Trim();
            entity.Surname = model.Surname.Trim();
            entity.PhoneNumber = model.PhoneNumber.Trim();
            entity.FullAddress = model.FullAddress.Trim();
            entity.City = model.City.Trim();
            entity.State = model.State.Trim();
            entity.Country = string.IsNullOrWhiteSpace(model.Country) ? "Türkiye" : model.Country.Trim();
            entity.Neighbourhood = model.Neighbourhood.Trim();

            if (model.IsDefault)
            {
                var otherAddresses = await _db.Addresses
                    .Where(a => a.UserId == userId && a.Id != entity.Id)
                    .ToListAsync(ct);

                foreach (var address in otherAddresses)
                {
                    address.IsDefault = false;
                }

                entity.IsDefault = true;
            }
            else
            {
                entity.IsDefault = false;

                var hasOtherDefault = await _db.Addresses
                    .AnyAsync(a => a.UserId == userId && a.IsDefault && a.Id != entity.Id, ct);

                if (!hasOtherDefault)
                {
                    // Ensure there is always a default address. If none exists, mark the current one.
                    entity.IsDefault = true;
                }
            }

            await _db.SaveChangesAsync(ct);

            var addresses = await _db.Addresses
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToListAsync(ct);

            return RenderAddressList(addresses);
        }

        [HttpPost]
        public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
        {
            var userId = GetUserId();

            var addresses = await _db.Addresses.Where(a => a.UserId == userId).ToListAsync(ct);
            if (!addresses.Any())
            {
                return NotFound();
            }

            var target = addresses.FirstOrDefault(a => a.Id == id);
            if (target is null)
            {
                return NotFound();
            }

            foreach (var address in addresses)
            {
                address.IsDefault = address.Id == id;
            }

            await _db.SaveChangesAsync(ct);

            return RenderAddressList(addresses);
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var userId = GetUserId();
            var addresses = await _db.Addresses
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToListAsync(ct);

            return RenderAddressList(addresses);
        }
    }
}
