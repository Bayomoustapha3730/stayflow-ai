import { useMemo, useState } from "react";
import { HostConsoleNav, HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useBillingDashboard } from "../hooks/useBillingDashboard";
import { billingPlanCards, getPlanRank } from "./billingPlans";
import { getBillingCapabilityMessage } from "./billingCapabilityMessages";
import "../styles/host-inbox.css";
import "../styles/organization-settings.css";
import "../styles/billing-dashboard.css";

function toDateLabel(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric"
  }).format(date);
}

function toMoney(cents: number, currency: string): string {
  const normalized = (currency || "usd").toUpperCase();
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: normalized,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(cents / 100);
}

function toPercent(used: number, limit: number | null | undefined, isUnlimited: boolean): number {
  if (isUnlimited || !limit || limit <= 0) {
    return 0;
  }

  return Math.min(100, Math.round((used / limit) * 100));
}

function badgeTone(status: string): "ok" | "warn" | "risk" {
  const normalized = status.toLowerCase();
  if (normalized.includes("past") || normalized.includes("unpaid")) {
    return "risk";
  }

  if (normalized.includes("trial") || normalized.includes("cancel")) {
    return "warn";
  }

  return "ok";
}

function planChangeLabel(currentPlan: string | null | undefined, targetPlan: string): "Upgrade" | "Downgrade" | "Change" {
  const currentRank = getPlanRank(currentPlan);
  const targetRank = getPlanRank(targetPlan);
  if (!currentRank || !targetRank || currentRank === targetRank) {
    return "Change";
  }

  return targetRank > currentRank ? "Upgrade" : "Downgrade";
}

export function BillingDashboardPage() {
  const auth = useHostAuth();
  const [pendingPlan, setPendingPlan] = useState<string | null>(null);
  const billing = useBillingDashboard({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  if (!auth.isAuthenticated) {
    return (
      <div className="sf-host-login-shell">
        <HostLoginPanel
          isSigningIn={auth.isSigningIn}
          error={auth.error}
          onLogin={auth.login}
          onClearError={auth.clearError}
        />
      </div>
    );
  }

  const subscription = billing.subscription;
  const isBusy = billing.isLoading || billing.isMutating;
  const pendingAction = useMemo(
    () => pendingPlan ? planChangeLabel(subscription?.planName, pendingPlan) : "Change",
    [pendingPlan, subscription?.planName]);

  return (
    <div className="sf-host-page sf-billing-page">
      <div className="sf-host-page-top">
        <header className="sf-organization-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Billing & Subscription</h1>
            <p className="sf-host-muted-note">Secure self-service billing with Stripe-backed subscription state.</p>
          </div>
          <div className="sf-organization-header-actions">
            <button type="button" onClick={() => void billing.refresh()} disabled={isBusy}>Refresh</button>
            <button type="button" onClick={() => auth.logout()}>Sign out</button>
          </div>
        </header>

        <HostConsoleNav
          auth={auth}
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          organizationsHref="/host/organizations"
          accountSettingsHref="/host/settings/account"
          current="billing"
        />

        {billing.error ? <div className="sf-host-inline-error" role="alert"><p>{billing.error}</p></div> : null}
        {billing.message ? <div className="sf-whatsapp-status" role="status"><p>{billing.message}</p></div> : null}

        {subscription?.trialEndsAtUtc ? (
          <section className="sf-trial-banner" aria-label="Trial status">
            <p>
              Trial active until <strong>{toDateLabel(subscription.trialEndsAtUtc)}</strong>. Add a payment method before expiry to avoid service interruption.
            </p>
            {subscription?.canManagePaymentMethod ? (
              <button type="button" onClick={() => void billing.openPaymentMethodPortal()} disabled={isBusy}>Add Payment Method</button>
            ) : null}
          </section>
        ) : null}

        <section className="sf-billing-grid" aria-label="Subscription overview">
          <article className="sf-organization-card sf-billing-overview">
            <h2>Current Subscription</h2>
            <p className="sf-host-muted-note">More details: <a href="/host/settings/billing/subscription">Current subscription page</a></p>
            <div className="sf-billing-key-value">
              <span>Plan</span>
              <strong>{subscription?.planName ?? "No active plan"}</strong>
            </div>
            <div className="sf-billing-key-value">
              <span>Status</span>
              <strong className={`sf-status-pill sf-status-${badgeTone(subscription?.status ?? "active")}`}>{subscription?.status ?? "Unknown"}</strong>
            </div>
            <div className="sf-billing-key-value">
              <span>Current period</span>
              <strong>{toDateLabel(subscription?.currentPeriodStartUtc)} - {toDateLabel(subscription?.currentPeriodEndUtc)}</strong>
            </div>
            <div className="sf-billing-key-value">
              <span>Cancel at period end</span>
              <strong>{subscription?.cancelAtPeriodEnd ? "Yes" : "No"}</strong>
            </div>
            <div className="sf-billing-key-value">
              <span>Billing status</span>
              <strong>{getBillingCapabilityMessage(subscription)}</strong>
            </div>
            {(subscription?.canOpenBillingPortal || subscription?.canManagePaymentMethod) ? (
              <div className="sf-billing-actions">
                {subscription?.canOpenBillingPortal ? (
                  <button type="button" onClick={() => void billing.openBillingPortal()} disabled={isBusy}>Open Billing Portal</button>
                ) : null}
                {subscription?.canManagePaymentMethod ? (
                  <button type="button" onClick={() => void billing.openPaymentMethodPortal()} disabled={isBusy}>Manage Payment Method</button>
                ) : null}
              </div>
            ) : null}
            {(subscription?.canCancel || subscription?.canResume) ? (
              <div className="sf-billing-actions">
                {subscription?.canCancel ? (
                  <>
                    <button type="button" onClick={() => void billing.cancelSubscription(true)} disabled={isBusy}>Cancel at Period End</button>
                    <button type="button" onClick={() => void billing.cancelSubscription(false)} disabled={isBusy}>Cancel Now</button>
                  </>
                ) : null}
                {subscription?.canResume ? (
                  <button type="button" onClick={() => void billing.resumeSubscription()} disabled={isBusy}>Resume</button>
                ) : null}
              </div>
            ) : null}
          </article>

          <article className="sf-organization-card">
            <h2>Plan Comparison</h2>
            <p className="sf-host-muted-note">Open dedicated page: <a href="/host/settings/billing/plans">Plan comparison page</a></p>
            <p className="sf-host-muted-note">Upgrade or downgrade with proration handled by Stripe.</p>
            {!subscription?.capability.checkoutAvailable ? (
              <p className="sf-host-muted-note">{subscription?.capability.message}</p>
            ) : null}
            <div className="sf-plan-grid">
              {billingPlanCards.map((plan) => (
                <div key={plan.name} className="sf-plan-card">
                  <h3>{plan.name}</h3>
                  <p className="sf-plan-price">{plan.monthlyPrice}<span>/month</span></p>
                  <p>{plan.copy}</p>
                  <ul>
                    {plan.highlights.map((item) => <li key={item}>{item}</li>)}
                  </ul>
                  <div className="sf-plan-actions">
                    <button type="button" onClick={() => setPendingPlan(plan.name)} disabled={isBusy || !subscription?.hasStripeSubscription || !subscription?.capability?.stripeConfigured}>{planChangeLabel(subscription?.planName, plan.name)} Plan</button>
                    <button type="button" onClick={() => void billing.openCheckout(plan.name, plan.trialDays)} disabled={isBusy || !subscription?.canStartCheckout}>Start Trial / Checkout</button>
                  </div>
                </div>
              ))}
            </div>
          </article>
        </section>

        {(billing.paymentOptions ?? []).length ? (
          <section className="sf-billing-grid" aria-label="Accepted payment methods">
            <article className="sf-organization-card">
              <h2>Accepted payment methods</h2>
              <div className="sf-payment-method-list" role="list">
                {(billing.paymentOptions ?? []).map((option) => (
                  <div key={option.key} className="sf-payment-method-item" role="listitem">
                    <strong>{option.label}</strong>
                    <p>{option.description}</p>
                  </div>
                ))}
              </div>
            </article>
          </section>
        ) : null}

        <section className="sf-billing-grid" aria-label="Usage and invoices">
          <article className="sf-organization-card">
            <h2>Usage Summary</h2>
            {billing.usage?.metrics?.length ? (
              <div className="sf-usage-list" role="list">
                {billing.usage.metrics.map((metric) => {
                  const percent = toPercent(metric.used, metric.limit, metric.isUnlimited);
                  return (
                    <div className="sf-usage-row" role="listitem" key={`${metric.metric}-${metric.entitlementKey}`}>
                      <div className="sf-usage-headline-row">
                        <strong>{metric.metric}</strong>
                        <span>{metric.isUnlimited ? `${metric.used.toLocaleString()} ${metric.unit}` : `${metric.used.toLocaleString()} / ${(metric.limit ?? 0).toLocaleString()} ${metric.unit}`}</span>
                      </div>
                      <div className="sf-usage-meter" aria-hidden="true">
                        <span style={{ width: `${percent}%` }} />
                      </div>
                      <p className="sf-host-muted-note">Period: {toDateLabel(metric.periodStartUtc)} - {toDateLabel(metric.periodEndUtc)}</p>
                    </div>
                  );
                })}
              </div>
            ) : (
              <p>{getBillingCapabilityMessage(subscription)} No usage data is available yet.</p>
            )}
          </article>

          <article className="sf-organization-card">
            <h2>Invoice History</h2>
            {billing.invoices.length === 0 ? (
              <p>{getBillingCapabilityMessage(subscription)} No invoices are available yet.</p>
            ) : (
              <div className="sf-invoice-table-wrap">
                <table className="sf-invoice-table">
                  <thead>
                    <tr>
                      <th>Invoice</th>
                      <th>Status</th>
                      <th>Amount Due</th>
                      <th>Amount Paid</th>
                      <th>Created</th>
                    </tr>
                  </thead>
                  <tbody>
                    {billing.invoices.map((invoice) => (
                      <tr key={invoice.id}>
                        <td>{invoice.externalInvoiceId}</td>
                        <td>{invoice.status}</td>
                        <td>{toMoney(invoice.amountDue, invoice.currency)}</td>
                        <td>{toMoney(invoice.amountPaid, invoice.currency)}</td>
                        <td>{toDateLabel(invoice.createdAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </article>
        </section>

        {pendingPlan ? (
          <dialog open className="sf-billing-dialog" aria-labelledby="dashboard-plan-dialog-title">
            <h2 id="dashboard-plan-dialog-title">Confirm {pendingAction}</h2>
            <p>
              Confirm {pendingAction.toLowerCase()} from <strong>{subscription?.planName ?? "no active plan"}</strong> to <strong>{pendingPlan}</strong>.
              {subscription?.hasStripeSubscription ? "Stripe will handle proration automatically." : "Checkout will activate the selected plan once completed."}
            </p>
            <div className="sf-billing-dialog-actions">
              <button type="button" onClick={() => setPendingPlan(null)} disabled={isBusy}>Keep Current Plan</button>
              <button
                type="button"
                onClick={async () => {
                  await billing.changePlan(pendingPlan);
                  setPendingPlan(null);
                }}
                disabled={isBusy}>
                Confirm {pendingAction}
              </button>
            </div>
          </dialog>
        ) : null}
      </div>
    </div>
  );
}
