# Iyzico integration health-check notes

This project already wires Iyzipay via `IyzicoPaymentService`, and the latest changes
close the loop so that a full integrity test for payments, callbacks and order placement
is now possible.

## What currently works

* `Program.cs` binds the `Iyzico` configuration section to `IyzicoOptions` and enforces that
  `ApiKey`, `SecretKey` and `BaseUrl` are present. The snippet you shared matches those
  expectations (`https://sandbox-api.iyzipay.com` and a callback URL pointing to
  `/Payment/iyzicoCallback`).
* `CheckoutController.PlaceOrder` builds an `IyzicoPaymentModel` and calls
  `IyzicoPaymentService.CreatePaymentAsync`. The service forwards the request to Iyzipay and
  honours the optional `CallbackUrl` when supplied.

## Callback and order persistence

* `PaymentController.IyzicoCallback` listens on `/Payment/iyzicoCallback`, verifies the
  Iyzi signature (using the configured `SecretKey`) and logs the outcome for traceability.
* `CheckoutController.PlaceOrder` now requires an authenticated user for card payments,
  persists the order together with an `iyzico` payment transaction, and returns the
  generated order number once the API responds with success.

With these steps in place the project can exercise the full payment → callback → order
creation workflow when running against the Iyzipay sandbox.
