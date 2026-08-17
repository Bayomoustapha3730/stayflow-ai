# M-PESA Integration

## Architecture

Guest and reservation payments use the provider-neutral `Payment` domain. SaaS subscription billing remains in the existing subscription/billing domain and is not reused for reservation payments.

`PaymentsController` validates the authenticated tenant and reservation before creating a payment. `MpesaApiClient` owns Daraja OAuth and STK Push HTTP calls. `MpesaWebhookController` is anonymous because Safaricom callbacks do not carry StayFlow authentication; it correlates only by the persisted `CheckoutRequestID`.

## Sandbox and production

`Mpesa:Environment` must be `Sandbox` or `Production`, and `Mpesa:BaseUrl` must be the corresponding HTTPS Daraja endpoint. Production cannot enable `DevelopmentMode`. Development fallback credentials are available only when the host environment is Development and `Mpesa:DevelopmentMode` is explicitly enabled.

Example non-secret configuration:

```json
{
  "Mpesa": {
    "Enabled": true,
    "Environment": "Sandbox",
    "BaseUrl": "https://sandbox.safaricom.co.ke",
    "DefaultCredentialReference": "default",
    "ShortCode": "YOUR_SANDBOX_SHORTCODE",
    "TransactionType": "CustomerPayBillOnline",
    "CallbackBaseUrl": "https://your-public-https-host",
    "DevelopmentMode": false
  }
}
```

Credential values are injected outside source control:

- `STAYFLOW_MPESA_DEFAULT_CONSUMER_KEY`
- `STAYFLOW_MPESA_DEFAULT_CONSUMER_SECRET`
- `STAYFLOW_MPESA_DEFAULT_PASSKEY`

Do not expose these variables to the browser, logs, health responses, tests, or documentation.

## OAuth and STK Push

1. StayFlow resolves credential references at runtime.
2. `MpesaApiClient` requests a Daraja OAuth token and caches it until shortly before expiry.
3. The authenticated host submits a reservation ID and guest phone to `POST /api/payments/mpesa/stk`.
4. StayFlow derives the active company from the authenticated tenant context, loads the reservation server-side, validates the guest/property relationship, derives the KES amount from the reservation, normalizes the Kenyan phone number, and persists the payment before calling Daraja.
5. Daraja returns merchant and checkout request IDs. StayFlow stores them for callback correlation.
6. The guest approves or declines the prompt on the phone.

## Callback and status mapping

Safaricom calls `POST /webhooks/mpesa/stk`. The endpoint is size-limited and rate-limited. It does not trust a tenant identifier from the callback. The checkout request ID locates the persisted payment, which supplies the tenant and reservation context.

- `Pending`: Daraja accepted the request and the customer decision is pending.
- `Processing`: the local transaction is being initiated.
- `Paid`: `ResultCode` is zero and receipt metadata is present.
- `Cancelled`: Daraja result code `1032` (customer cancelled).
- `Failed`: other non-zero result codes or provider/transport failure.
- `Expired`: reserved for future expiry handling.

A paid transaction is terminal. Duplicate callbacks are recorded once by the provider/event ID uniqueness constraint and do not repeat payment mutation or audit side effects.

## Tenant isolation and idempotency

Every authenticated payment read and initiation includes the active `CompanyId`; request bodies cannot override it. Reservation, property, and guest ownership are checked server-side. Anonymous callbacks derive tenant ownership only after matching a persisted checkout request ID.

Clients may send an idempotency key with initiation. The key is stored as the payment external reference and is resolved within the active tenant. Provider callback events are deduplicated by `(Provider, EventId)` and database uniqueness protects concurrent retries.

## Health check

The M-PESA check is included in `/health/ready` and `/health`. It reports `Disabled`, `ConfigurationMissing`, `ProviderReachable`, or `ProviderUnavailable`. It validates configuration and probes OAuth reachability; it never initiates an STK Push and never exposes credential values.

## Sandbox setup

1. Create or sign into a Safaricom Daraja developer account.
2. Create a sandbox application and obtain its consumer key and consumer secret.
3. Obtain the sandbox shortcode and passkey for the selected STK product.
4. Inject the three credential environment variables into the backend runtime.
5. Configure a publicly reachable HTTPS callback base URL ending at the StayFlow webhook route.
6. Enable the sandbox configuration and initiate a payment for a supported test phone number.
7. Inspect the safe payment status and callback result in StayFlow logs and the host API, without logging raw credentials or full callback payloads.

## Production go-live checklist

- [ ] Daraja production application verified and approved.
- [ ] Production credentials and passkey stored in a secret manager.
- [ ] Production shortcode configured separately from sandbox.
- [ ] Stable HTTPS callback domain reachable by Safaricom.
- [ ] Payment migration applied to the production database.
- [ ] OAuth and callback monitoring and alerting enabled.
- [ ] Timeout and retry behavior reviewed with operations.
- [ ] Payment support and reconciliation procedures defined.
- [ ] Privacy, retention, and Kenya regulatory review completed as applicable.

StayFlow does not claim regulatory compliance automatically. Daraja availability, account approval, callback delivery, and settlement remain external Safaricom dependencies.

## Troubleshooting

- `ConfigurationMissing`: verify the enabled flag, HTTPS base/callback URLs, shortcode, and runtime credential variables.
- `ProviderUnavailable`: check outbound HTTPS access, Daraja status, DNS, and timeout settings.
- Unknown checkout callback: verify the initiation response was persisted and the callback uses the same environment.
- Repeated callback logs: expected provider retries are safely ignored after the first event.
- Failed or cancelled payment: inspect the sanitized provider result code/message and allow a new payment attempt rather than changing a paid transaction.
