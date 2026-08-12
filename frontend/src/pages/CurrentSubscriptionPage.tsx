import { HostConsoleNav, HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useBillingDashboard } from "../hooks/useBillingDashboard";
import "../styles/host-inbox.css";
import "../styles/organization-settings.css";
import "../styles/billing-dashboard.css";
import { getBillingCapabilityMessage } from "./billingCapabilityMessages";

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

export function CurrentSubscriptionPage() {
  const auth = useHostAuth();
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

  return (
    <div className="sf-host-page sf-billing-page">
      <div className="sf-host-page-top">
        <header className="sf-organization-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Current Subscription</h1>
            <p className="sf-host-muted-note">Authoritative subscription state synchronized from Stripe.</p>
          </div>
          <div className="sf-organization-header-actions">
            <a href="/host/settings/billing">Back to Dashboard</a>
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

        <section className="sf-billing-grid" aria-label="Current subscription details">
          <article className="sf-organization-card sf-billing-overview">
            <h2>Subscription Snapshot</h2>
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
        </section>
      </div>
    </div>
  );
}
