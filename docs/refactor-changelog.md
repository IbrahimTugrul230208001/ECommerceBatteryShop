# Refactor Changelog — Layering Pass

**What this is:** a behavior-preserving layering refactor moving logic to where it belongs —
controllers do HTTP only, services hold business logic, repositories do data access. Companion to
[layering-audit.md](layering-audit.md) (the analysis/plan); this doc is the record of *what changed*,
for review and commit-writing.

**Status:** 5 of 7 chunks complete + 1 partial. **Build green (0 errors) after every chunk.**
Nothing committed — all changes are in the working tree.

**Guiding rule we held:** a layering move should not change behavior. Where a "move" unavoidably
touched numbers (pricing), it's called out below and needs testing. Real bugs found along the way
were **flagged, not fixed** (they change charged money and deserve their own tested change).

---

## Not part of this refactor (separate edits, noted for honesty)

- `ECommerceBatteryShop/appsettings.json` — `FallbackUsdTryRate` 46 → 47.2 (manual FX config edit).
- `ECommerceBatteryShop/curl_output.txt` — deleted manually (committed debug junk).

These show in `git status` but were not made by the refactor.

---

## Chunks

### ✅ A — PricingService (the keystone)
The one price formula `(ConvertUsdToTry(price) + ExtraAmount) × (1 + KDV)` + discount, previously
copy-pasted 9 times with subtle divergences, now lives in one place.
- **New:** `Services/PricingService.cs` — `PriceUnit()` (canonical display) and `ChargeLineTotal()`
  (payment path, reproduces the old checkout numbers exactly).
- **Modified:** `Program.cs` (DI), `UrunController`, `EvController`, `FavoriController`,
  `SepetController`, `SiparisController`.
- **Behavior deltas (TEST THESE):**
  - **Favorites now include `ExtraAmount`** (was dropped → prices there rise to correct value).
  - Cart/contract now round the USD→TRY step and use away-from-zero discount rounding — **sub-cent**.
  - Checkout charge amounts **unchanged** by design.

### ✅ E — Owner resolution
Claim-parsing + `ANON_ID` cookie read/create, previously duplicated ~12 times, centralized.
- **New:** `Infrastructure/AnonymousId.cs` — `AnonymousId.Read/Ensure` + `ClaimsPrincipal.GetUserId()`.
- **Modified:** `SepetController` (×6 sites), `FavoriController`, `UrunController`, `EvController`,
  `SiparisController.TryResolveOwner`.
- **Behavior delta:** new guest cookies use a standard **1-year expiry + SameSite=Lax + `N`-format id**
  (Sepet was 3-month/dashed). Existing cookies still read fine. `GetUserId()` uses `TryParse`
  (non-numeric `sub` → treated as guest instead of throwing).

### ✅ G — Address mapper
`Address → AddressViewModel`, previously three identical private methods, unified.
- **New:** `Mapping/AddressMapper.cs` — `Address.ToViewModel()`.
- **Modified:** `AdresController`, `HesapController`, `SepetController` (mappers removed).
- **Behavior:** none (pure move).

### ✅ D — Favorites split
`FavoritesService` was secretly a repository (held the `DbContext`). Split into a real repo +
thin service.
- **New:** `DataAccess/Abstract/IFavoritesRepository.cs`, `DataAccess/Concrete/FavoritesRepository.cs`
  (EF moved here, takes primitive `userId`/`anonId` like `CartRepository`).
- **Modified:** `FavoritesService.cs` (now thin — translates `FavoriteOwner`, wraps `ToggleResult`),
  `IFavoritesService.cs` (removed dead `GetPricesAsync`), `Program.cs` (DI), `CartService.cs` +
  `ICartService.cs` (removed dead `CartTotalPriceAsync`).
- **Behavior:** none (pure move + dead-code removal).

### ✅ F — Account/email (scoped)
- **New:** `Services/EmailService.cs` (MailKit/SMTP out of the controller),
  `Services/AccountService.cs` (`RegisterAsync` validation orchestration).
- **Modified:** `HesapController` (no longer references MailKit/MimeKit/`IUserService`/`SmtpOptions`;
  `RegisterUser` is a thin call; dead verification-code writes + `Console.WriteLine` catch removed),
  `Program.cs` (DI).
- **Deliberately left in the controller** (HTTP/auth-interleaved, sensitive): admin-vs-config login
  policy + claims/`SignInAsync`, and password-reset **token** orchestration (the email *send* now
  goes through `IEmailService`).

### 🟡 C — Admin off DbContext (PARTIAL)
- **Done:** `DataAccess/Abstract/IInventoryRepository.cs` + `Concrete/InventoryRepository.cs` (stock
  search + quantity upsert); category ops added to `ICategoryRepository`/`CategoryRepository`
  (`GetByIdAsync`/`HasProductsAsync`/`DeleteAsync`/`GetAssignableAsync`). `AdminController` stock
  methods, `DeleteCategory`, and `LoadCategoryItemsAsync` now use repos.
- **STILL RAW `_context` in `AdminController` (~27 calls) — NOT done:** `CreateProduct` (file upload +
  slug + inventory), `DeleteProduct` (file delete), `AssignCategoryAsync`, product-selection listing,
  `UrunPaneli` category lookup, and the two analytics screens (`SepetAnalitikleri`/`FavoriAnalitikleri`).
  Until these move, `AdminController` still injects `BatteryShopContext`.

### ⬜ B — CheckoutService (NOT STARTED)
The 1521-line `SiparisController` order/payment orchestration. Largest and payment-critical;
requires a real test checkout (incl. 3DS) before shipping.

---

## Bugs fixed (behavior changes — MUST test-checkout before push)

1. ✅ **Checkout now applies discounts.** `ChargeLineTotal` now routes through `PriceUnit`, so the
   charged line = the displayed discounted price × qty. Both charge methods (`BuildBasketItems` for
   card, `CalculateOrderTotal` for IBAN/3DS) pass `Product.Discounts`. Customers are now billed what
   the cart shows. **Charged totals drop for any product with an active discount** — verify with a
   sandbox payment.
2. ✅ **Live-vs-snapshot mismatch resolved.** `CalculateOrderTotal` now prices snapshot `item.UnitPrice`
   (was live `Product.Price`), matching `BuildBasketItems` and the cart display. Card and IBAN totals
   agree again.
   - Side effect: the charge path now also rounds the USD→TRY step (via `PriceUnit`) — sub-cent, and
     it makes charge == display exact.

**Still open / out of scope:** `OrderItem.UnitPrice` is stored as the USD snapshot base (not the TRY
discounted price charged) — a data-representation inconsistency in the order record, not a charge bug.
Plus the smaller smells in [layering-audit.md](layering-audit.md): `OrderRepository` dead method,
magic status strings, `UserService` mutable state, likely-broken `ChangePassword` view path.

---

## How to review

Net so far: **~211 insertions vs ~548 deletions** of real code (excluding the curl_output.txt junk) —
the point of the exercise: less code, no duplication.

Suggested order (easiest → meatiest): **G → E → D → F → A → C**. For each, read the *one new file*
that carries the idea, then one example call site; the rest are mechanical repeats.

**Commit caveat:** because chunks were done sequentially without committing, some files carry edits
from *multiple* chunks (e.g. `SepetController` = A + E + G; several controllers = A + E). Clean
one-commit-per-chunk therefore needs hunk-level staging (`git add -p`), or commit by logical grouping
(e.g. "pricing", "owner+mapper", "favorites+account", "admin-partial"). Do NOT bundle the rename
(discussed separately) into these commits.

## Test checklist before shipping any of this
- [ ] Cart, favorites, product list/detail render correct prices (esp. **favorites went up** by
      `ExtraAmount` — confirm that's the intended number).
- [ ] Add-to-cart / favorite toggle still work for both logged-in and guest (new cookie policy).
- [ ] Admin stock panel search + save; category delete.
- [ ] Register / password-reset email still send.
- [ ] `dotnet build` clean (currently 0 errors).
