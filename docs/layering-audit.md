# Layering Audit — Controller → Service → Repository

**Goal of this pass:** move each piece of logic to the layer it belongs in. Nothing else
(renaming, rewriting algorithms, fixing bugs) is in scope yet — this doc only maps *what is
misplaced* and *where it should go*. A short "other issues, later" list is at the end so we don't
lose them.

## The contract we're enforcing

| Layer | Allowed to do | Must NOT do |
|-------|---------------|-------------|
| **Controller** | HTTP only: routing, model binding, auth/cookie → owner resolution, `ModelState`, calling one service, shaping the `IActionResult`/ViewModel | Pricing math, order/entity construction, EF queries, file I/O, SMTP, JSON parsing of gateway responses |
| **Service** | Business logic: pricing, discounts, order assembly, payment orchestration, email, caching, domain rules | Touch `HttpContext`, `Request`/`Response`, cookies, `IActionResult` |
| **Repository** | CRUD against `DbContext`, query composition, projections it owns | Business rules, pricing, domain status strings, gateway/HTTP concerns |

## Current reality (the headline problems)

1. **`SiparisController` is a 1521-line God controller** — nearly the entire checkout/payment
   business layer lives inside it.
2. **The core price formula is copy-pasted in 9 places** across 5 controllers, each slightly
   different. This is the highest-leverage fix.
3. **`AdminController` skips the repository layer** — it injects `BatteryShopContext` and runs raw
   EF + file I/O directly.
4. **`FavoritesService` is actually a repository** wearing a service name (it holds the `DbContext`).
5. **Owner-resolution (claims/cookie → `CartOwner`) is duplicated ~12 times.**
6. **The `ECommerceBatteryShop.Business` project is empty** — every "service" actually lives in the
   web (MVC) project, so there is no real business-layer boundary today.

---

## Findings by destination

### A. Pricing & discounts → new `IPricingService` (HIGHEST PRIORITY)

The formula `(usdPrice * fxRate + ExtraAmount) * (1 + KdvRate)`, followed by "find best active
discount, apply percentage, round", is duplicated with subtle differences everywhere:

| File | Location |
|------|----------|
| [UrunController.cs:130](ECommerceBatteryShop/Controllers/UrunController.cs:130) | list mapping |
| [UrunController.cs:268](ECommerceBatteryShop/Controllers/UrunController.cs:268), [:300](ECommerceBatteryShop/Controllers/UrunController.cs:300) | detail + related |
| [EvController.cs:72](ECommerceBatteryShop/Controllers/EvController.cs:72) | `Map()` |
| [SepetController.cs:117](ECommerceBatteryShop/Controllers/SepetController.cs:117) | cart Index |
| [SepetController.cs:213](ECommerceBatteryShop/Controllers/SepetController.cs:213) | checkout subtotal |
| [SepetController.cs:271](ECommerceBatteryShop/Controllers/SepetController.cs:271) | contract builder |
| [FavoriController.cs:86](ECommerceBatteryShop/Controllers/FavoriController.cs:86) | favorites |
| [SiparisController.cs:863](ECommerceBatteryShop/Controllers/SiparisController.cs:863) | `BuildBasketItems` |
| [SiparisController.cs:882](ECommerceBatteryShop/Controllers/SiparisController.cs:882) | `CalculateOrderTotal` |

`KdvRate = 0.20m` is re-declared as a local const in each of these files. `CartService.CartTotalPriceAsync`
([CartService.cs:63](ECommerceBatteryShop/Services/CartService.cs:63)) uses yet another variant (`* 1.2m`, no fx).

**Move to:** one `IPricingService` with methods like `decimal UnitPrice(Product, fxRate)` and
`PricedItem PriceLineItem(...)` returning `{ Final, Original, DiscountPct }`. Every controller/service
calls it. `KdvRate` and the discount-selection rule live there once.

### B. Checkout / payment orchestration → new `ICheckoutService` (+ payment helpers into `IyzicoPaymentService`)

Everything below is in `SiparisController` and is business logic:

| Code | Lines | Belongs in |
|------|-------|-----------|
| `PlaceOrder` order assembly (user & guest, IBAN & card branches) | [109–678](ECommerceBatteryShop/Controllers/SiparisController.cs:109) | `CheckoutService` |
| `FinalizeThreeDSOrderAsync` (dup user/guest order build) | [1316–1475](ECommerceBatteryShop/Controllers/SiparisController.cs:1316) | `CheckoutService` |
| `BuildBasketItems` / `CalculateOrderTotal` | [856–889](ECommerceBatteryShop/Controllers/SiparisController.cs:856) | `PricingService` (A) |
| `BuildBuyerContextAsync` / `BuildGuestBuyerContext` (domain → Iyzico models) | [930–1038](ECommerceBatteryShop/Controllers/SiparisController.cs:930) | `CheckoutService` |
| `TrySaveCardFromResponseAsync` (parse Iyzico JSON + write repo) | [1046–1095](ECommerceBatteryShop/Controllers/SiparisController.cs:1046) | `IyzicoPaymentService` + `SavedCardRepository` |
| `ExtractTransactionId`, `IsCardStorageNotEnabled` (parse gateway JSON) | [1214–1261](ECommerceBatteryShop/Controllers/SiparisController.cs:1214), [1477–1520](ECommerceBatteryShop/Controllers/SiparisController.cs:1477) | `IyzicoPaymentService` |
| `TryParseCard`, `TryParseExpiry`, `DeriveIdentityNumber`, `NormalizePhone`, `SanitizeDigits` | [1097–1212](ECommerceBatteryShop/Controllers/SiparisController.cs:1097) | payment/validation helper in service layer |
| `GenerateOrderNumber`, `SanitizeShipping`, `ResolveCarrier` | [58–67](ECommerceBatteryShop/Controllers/SiparisController.cs:58), [891–900](ECommerceBatteryShop/Controllers/SiparisController.cs:891) | `CheckoutService` |
| `Console.WriteLine(unitPriceTry)` debug leftover | [883](ECommerceBatteryShop/Controllers/SiparisController.cs:883) | delete |

**What stays in the controller:** reading the form, resolving the owner, calling
`_checkoutService.PlaceOrderAsync(...)`, and translating the result into `Ok/BadRequest`. The 3DS
callback/redirect *HTML plumbing* (`ThreeDSInit`, `RenderThreeDSRedirect`, `TryGetFromFormOrQuery`)
is genuinely HTTP and can stay — but the order finalization it calls moves out.

### C. Admin data access → repositories/services (stop using `DbContext` in the controller)

`AdminController` injects `BatteryShopContext` directly ([AdminController.cs:22](ECommerceBatteryShop/Controllers/AdminController.cs:22)) and does raw EF everywhere:

| Method | Lines | Move to |
|--------|-------|---------|
| `CreateProduct` — file upload (image/PDF to disk), slug generation, inventory upsert, category link | [371–570](ECommerceBatteryShop/Controllers/AdminController.cs:371) | `ProductService` (logic) + `ProductRepository` (persistence); file save → `IFileStorage` |
| `DeleteProduct` — EF removes + physical file delete | [210–239](ECommerceBatteryShop/Controllers/AdminController.cs:210) | `ProductService` + repo |
| `AssignCategoryAsync` | [528–570](ECommerceBatteryShop/Controllers/AdminController.cs:528) | `ProductRepository`/`CategoryRepository` |
| `DeleteCategory` | [258–281](ECommerceBatteryShop/Controllers/AdminController.cs:258) | `CategoryRepository` (+ service for the "has products?" rule) |
| `SepetAnalitikleri`, `FavoriAnalitikleri` — raw `_context.Carts/FavoriteLists` queries | [97–192](ECommerceBatteryShop/Controllers/AdminController.cs:97) | new repo methods (`ICartRepository`/`IFavoritesRepository`) |
| `StokPaneli`, `StokGuncelle`, `StockSearch`, `LoadStockItemsAsync` — inventory read/write | [284–369](ECommerceBatteryShop/Controllers/AdminController.cs:284), [572–604](ECommerceBatteryShop/Controllers/AdminController.cs:572) | `IInventoryRepository` (new) + service |
| `LoadCategoryItemsAsync`, `LoadProductSelectionItemsAsync`, `PopulateEntryViewModelAsync` | [606–726](ECommerceBatteryShop/Controllers/AdminController.cs:606) | repo query methods |
| Slug generation (Turkish char chain) | [435–437](ECommerceBatteryShop/Controllers/AdminController.cs:435) | shared `Slugifier` util (service layer) |

### D. `FavoritesService` is a repository → split it

[FavoritesService.cs](ECommerceBatteryShop/Services/FavoritesService.cs) injects `BatteryShopContext` and is pure CRUD
(`GetAsync`, `ToggleAsync`, `CountAsync`, `GetPricesAsync`). There is no `IFavoritesRepository`.

**Move to:** create `IFavoritesRepository` (the EF code) under `DataAccess`. Keep a thin
`FavoritesService` only if it carries real logic (owner handling / pricing via `IPricingService`);
otherwise the controller can use the repo. `GetPricesAsync` returning raw prices then priced in the
controller ([FavoriController.cs:86](ECommerceBatteryShop/Controllers/FavoriController.cs:86)) folds into (A).

### E. Owner resolution → one helper (`ICartOwnerResolver` or a base controller)

The "authenticated → `sub` claim, else `ANON_ID` cookie, create if missing" block is repeated in:
`SepetController` ×6 ([Index](ECommerceBatteryShop/Controllers/SepetController.cs:81), Siparis, Add, SetQuantity, Delete, DeleteAll),
`FavoriController` ×2, `UrunController.LoadFavoriteIdsAsync`, `EvController.LoadFavoriteIdsAsync`,
`SiparisController.TryResolveOwner`. Cookie expiry is inconsistent (3 months vs 1 year).

**Move to:** a single resolver used by every controller. Still controller-layer (it reads
`HttpContext`), just not copy-pasted. Standardize the cookie policy in one place.

### F. Auth/account business logic → `IAccountService` / `IEmailService`

`HesapController` mixes HTTP with business logic:

| Code | Lines | Move to |
|------|-------|---------|
| `SendPasswordResetEmailAsync` — MailKit SMTP send inside the controller | [443–479](ECommerceBatteryShop/Controllers/HesapController.cs:443) | `IEmailService` |
| Admin login by comparing to `appsettings` config inline | [164–187](ECommerceBatteryShop/Controllers/HesapController.cs:164) | `IAccountService` (auth policy) |
| `RegisterUser` — verification-code gen, validation orchestration | [101–142](ECommerceBatteryShop/Controllers/HesapController.cs:101) | `IAccountService` |
| Password-reset token orchestration (create/expire/consume) | [245–341](ECommerceBatteryShop/Controllers/HesapController.cs:245) | `IAccountService` |
| Claim building + cart merge | [196–216](ECommerceBatteryShop/Controllers/HesapController.cs:196) | service helper |

### G. Duplicated mappers

`MapAddress` / `MapToViewModel` (identical) appears in
[HesapController.cs:481](ECommerceBatteryShop/Controllers/HesapController.cs:481),
[SepetController.cs:375](ECommerceBatteryShop/Controllers/SepetController.cs:375),
[AdresController.cs:175](ECommerceBatteryShop/Controllers/AdresController.cs:175).
`SepetController.BuildContractViewModel` ([:253–373](ECommerceBatteryShop/Controllers/SepetController.cs:253)) is
120 lines of contract/invoice assembly + pricing = business logic → service.

**Move to:** one address mapper (static mapper class or extension). Contract building → a service.

---

## Suggested target structure

```
ECommerceBatteryShop.Business/Services/   (currently EMPTY — start using it)
  PricingService.cs            (A)  ← the one true price formula + KdvRate + discount rule
  CheckoutService.cs           (B)  ← PlaceOrder/3DS finalize/order assembly/buyer context
  ProductService.cs            (C)  ← admin product create/delete/slug, delegates persistence
  InventoryService.cs          (C)
  AccountService.cs            (F)
  EmailService.cs              (F)
  Slugifier.cs                 (C)  ← shared Turkish-safe slug util

ECommerceBatteryShop.DataAccess/
  Abstract/IFavoritesRepository.cs   (D)
  Abstract/IInventoryRepository.cs   (C)
  Concrete/FavoritesRepository.cs    (D)  ← moved out of "FavoritesService"
  Concrete/InventoryRepository.cs    (C)
  + new query methods on ICart/ICategory/IProduct repos for admin analytics (C)

ECommerceBatteryShop/ (web)
  Infrastructure/CartOwnerResolver.cs (E)
  Mapping/AddressMapper.cs            (G)
```

(If you'd rather keep services in the web project for now, that's fine — the *layer separation*
matters more than the assembly. But the empty `Business` project is the natural home.)

## Suggested sequencing

1. **`IPricingService` (A)** — biggest duplication, unblocks cleanup in 5 controllers + `SiparisController`.
2. **Owner resolver (E)** and **address mapper (G)** — small, mechanical, low-risk wins.
3. **`FavoritesService` → repository split (D)** — clarifies the pattern before bigger moves.
4. **`AdminController` → repos/services (C)** — removes `DbContext` from the controller.
5. **`CheckoutService` (B)** — largest, do last, after pricing/owner helpers exist to lean on.
6. **Account/email (F)** — independent, can slot in anytime.

---

## Progress log

- **Chunk A (PricingService) — DONE.** Added `IPricingService`/`PricingService`
  ([Services/PricingService.cs](ECommerceBatteryShop/Services/PricingService.cs)), registered in DI. All display sites (Urun list/detail/
  related, Ev home, Cart display, checkout subtotal, contract builder, Favorites) now call
  `PriceUnit(...)`; the checkout charge path (`BuildBasketItems`, `CalculateOrderTotal`) calls
  `ChargeLineTotal(...)`. Build green, 0 errors.
  - **Behavior deltas to test:** (1) **Favorites now include `ExtraAmount`** (was missing → prices
    rise to correct value). (2) Cart/contract now round the USD→TRY step via `ConvertUsdToTry` and
    use away-from-zero discount rounding — **sub-cent** shifts only. (3) Charge path numbers
    **unchanged** by design.

- **Chunk E (owner resolution) — DONE.** Added [Infrastructure/AnonymousId.cs](ECommerceBatteryShop/Infrastructure/AnonymousId.cs):
  `AnonymousId.Read/Ensure` (one cookie policy) + `ClaimsPrincipal.GetUserId()`. Replaced the
  copy-pasted claim-parse + `ANON_ID` read/create blocks in Sepet (×6), Favori (×2), Urun, Ev, and
  `SiparisController.TryResolveOwner`. Build green.
  - **Behavior deltas:** new guest cookies now use a **standard 1-year expiry + SameSite=Lax +
    `N`-format id** (Sepet previously 3-month, dashed). Existing cookies still read fine. `GetUserId()`
    uses `TryParse` (a non-numeric `sub` like admin → treated as guest instead of throwing).
  - **Left for Chunk F:** `HesapController` login/Google cart-merge `ANON_ID` reads (different flow).

- **Chunk G (address mapper) — DONE.** Added [Mapping/AddressMapper.cs](ECommerceBatteryShop/Mapping/AddressMapper.cs)
  (`Address.ToViewModel()`); removed the three identical private mappers from Hesap/Sepet/Adres.
  Build green. Contract-builder move deferred into Chunk B (checkout-domain logic).

- **Chunk D (favorites split) — DONE.** Added [IFavoritesRepository](ECommerceBatteryShop.DataAccess/Abstract/IFavoritesRepository.cs) +
  [FavoritesRepository](ECommerceBatteryShop.DataAccess/Concrete/FavoritesRepository.cs) (EF moved here, takes primitive userId/anonId like
  `CartRepository`). `FavoritesService` is now a thin translator (`FavoriteOwner` → primitives,
  wraps `ToggleResult`); no longer holds `DbContext`. Registered repo in DI. Removed dead
  `GetPricesAsync` and dead `CartService.CartTotalPriceAsync`. Build green.

- **Chunk F (account/email) — DONE (scoped).** Added [Services/EmailService.cs](ECommerceBatteryShop/Services/EmailService.cs)
  (MailKit/SMTP out of the controller) and [Services/AccountService.cs](ECommerceBatteryShop/Services/AccountService.cs)
  (`RegisterAsync` validation orchestration). `HesapController` no longer references MailKit/MimeKit/
  `IUserService`/`SmtpOptions`; `RegisterUser` is now a thin call; dead verification-code writes and
  `Console.WriteLine` catch removed. Registered both in DI. Build green.
  - **Deliberately left in the controller** (HTTP/auth-interleaved, sensitive — own future pass):
    admin-vs-config login policy + claims/`SignInAsync`, and the password-reset **token**
    orchestration (the email SEND now goes through `IEmailService`; token create/validate/consume
    still in-controller). `IUserService`/`UserService` remain registered but unused — mutable-state
    smell already flagged below.

- **Chunk C (Admin off DbContext) — PARTIAL.** Done: [IInventoryRepository](ECommerceBatteryShop.DataAccess/Abstract/IInventoryRepository.cs)/
  [InventoryRepository](ECommerceBatteryShop.DataAccess/Concrete/InventoryRepository.cs) (stock search + quantity upsert) and category
  ops added to `ICategoryRepository` (`GetByIdAsync`/`HasProductsAsync`/`DeleteAsync`/`GetAssignableAsync`).
  `AdminController` stock methods (`StokPaneli`/`StokGuncelle`/`StockSearch`/`LoadStockItemsAsync`),
  `DeleteCategory`, and `LoadCategoryItemsAsync` now use repos. Build green.
  - **STILL RAW `_context` in AdminController (~27 calls) — REMAINING:**
    - `CreateProduct` (file upload image/PDF, slug generation, product add/update, inventory) →
      needs a `ProductAdminService` (web) + `Slugifier` + product-write repo methods.
    - `DeleteProduct` (remove product+inventory+links, delete image file) → service + repo.
    - `AssignCategoryAsync` (product↔category linking) → repo.
    - `PopulateEntryViewModelAsync` / `LoadProductSelectionItemsAsync` (product search listing) → repo.
    - `UrunPaneli` single-category lookup → repo.
    - `SepetAnalitikleri` / `FavoriAnalitikleri` (raw `Carts`/`FavoriteLists` analytics queries) →
      repo methods on `ICartRepository`/`IFavoritesRepository`.
    Until these move, `AdminController` still injects `BatteryShopContext`.

- **Chunk B (CheckoutService) — NOT STARTED.** The 1521-line `SiparisController` payment/order
  orchestration. Largest and payment-critical; needs a real test checkout (incl. 3DS) before shipping.

## Confirmed bugs found during Chunk A (flagged, NOT fixed — for the later "bugs" chunk)

- ~~**Checkout charge path applies NO discount.**~~ **FIXED.** `ChargeLineTotal` now routes through
  `PriceUnit` and both charge methods pass `Product.Discounts`, so the customer is charged the
  discounted price shown in the cart. **Behavior change — test with a sandbox checkout** (discounted
  items now bill lower).
- ~~**`CalculateOrderTotal` prices live `Product.Price`; `BuildBasketItems` prices snapshot
  `item.UnitPrice`.**~~ **FIXED.** `CalculateOrderTotal` now uses snapshot `item.UnitPrice`, matching
  `BuildBasketItems` and the cart display.
- **Now-dead after Chunk A:** `IFavoritesService.GetPricesAsync` (no callers left) and
  `CartService.CartTotalPriceAsync` (its own `* 1.2m`, no fx/ExtraAmount variant) — remove during
  Chunk D.

## Out of scope now — record so we don't forget (bugs & smells, NOT relocation)

- `OrderRepository.GetOrderByUserIdAsync` throws `NotImplementedException`; two confusingly
  overloaded `GetOrdersByUserIdAsync`; `CancelOrder` lacks a `CancellationToken`
  ([OrderRepository.cs:27](ECommerceBatteryShop.DataAccess/Concrete/OrderRepository.cs:27), [:49](ECommerceBatteryShop.DataAccess/Concrete/OrderRepository.cs:49), [:68](ECommerceBatteryShop.DataAccess/Concrete/OrderRepository.cs:68)).
- Order **status strings** ("Sipariş alındı", "Ödeme alındı", "İptal edildi") are magic strings in
  controller + repo → an enum/constants.
- `UserService` holds mutable `VerificationCode`/`Email`/`Password` as shared state
  ([UserService.cs](ECommerceBatteryShop/Services/UserService.cs)) — concurrency hazard if singleton; the verification code is set but
  never actually verified.
- `HesapController.ChangePassword` returns views at `~/Views/Profil/Ayarlar.cshtml` but the file is
  under `~/Views/Hesap/` — likely a broken path ([HesapController.cs:67](ECommerceBatteryShop/Controllers/HesapController.cs:67)).
- `AdminController.DeleteProduct` dereferences `product`/`productCategory` without null checks
  ([:218](ECommerceBatteryShop/Controllers/AdminController.cs:218)).
- Interface placement is inconsistent (repos in `Abstract/`, but `ICategoryService`/
  `IIyzicoPaymentService` sit inside their impl files).
- `curl_output.txt` is committed inside `Controllers/`.
