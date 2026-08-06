# Stripe Configuration

## Required Backend Settings

Configure the `Billing` section in environment-specific configuration or secret store.

```json
{
  "Billing": {
    "Provider": "Stripe",
    "StripeSecretKey": "sk_live_...",
    "StripeWebhookSigningSecret": "whsec_...",
    "CheckoutSuccessUrl": "https://app.example.com/host/settings/billing?checkout=success",
    "CheckoutCancelUrl": "https://app.example.com/host/settings/billing?checkout=cancel",
    "BillingPortalReturnUrl": "https://app.example.com/host/settings/billing",
    "PlanPriceIds": {
      "Starter": "price_...",
      "Growth": "price_...",
      "Scale": "price_..."
    },
    "WebhookToleranceSeconds": 300,
    "WebhookMaxBodyBytes": 262144
  }
}
```

## Stripe Dashboard Setup

1. Create recurring prices for each supported plan.
2. Configure a webhook endpoint:

- URL: `https://<api-host>/api/billing/webhook/stripe`
- Events:
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `invoice.paid`
  - `invoice.payment_failed`

3. Copy endpoint signing secret into `Billing:StripeWebhookSigningSecret`.
4. Enable Billing Portal in Stripe Dashboard.
5. Ensure allowed portal flows include payment method update.

## Secrets Management

- Store `StripeSecretKey` and `StripeWebhookSigningSecret` in secret manager, not source control.
- Rotate secrets with operational procedure and verify post-rotation health.
- Use distinct Stripe projects/accounts or API keys for non-production and production.

## Post-Deployment Verification

1. Trigger checkout from billing dashboard.
2. Confirm webhook deliveries are successful in Stripe dashboard.
3. Verify subscription state and invoices appear in billing APIs.
4. Validate duplicate webhook replay is treated as duplicate without side effects.
