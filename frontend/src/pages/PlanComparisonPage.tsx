import { useMemo, useState } from "react";
import { HostConsoleNav, HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useBillingDashboard } from "../hooks/useBillingDashboard";
import { billingPlanCards, getPlanRank } from "./billingPlans";
import "../styles/host-inbox.css";
import "../styles/organization-settings.css";
import "../styles/billing-dashboard.css";

function actionLabel(currentPlan: string | null | undefined, targetPlan: string): "Upgrade" | "Downgrade" | "Change" {
  const currentRank = getPlanRank(currentPlan);
  const targetRank = getPlanRank(targetPlan);

  if (!currentRank || !targetRank || currentRank === targetRank) {
    return "Change";
  }

  return targetRank > currentRank ? "Upgrade" : "Downgrade";
}

export function PlanComparisonPage() {
  const auth = useHostAuth();
  const billing = useBillingDashboard({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });
  const [pendingPlan, setPendingPlan] = useState<string | null>(null);

  const subscription = billing.subscription;
  const isBusy = billing.isLoading || billing.isMutating;
  const currentPlan = subscription?.planName ?? null;
  const pendingAction = useMemo(() => pendingPlan ? actionLabel(currentPlan, pendingPlan) : "Change", [currentPlan, pendingPlan]);

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

  return (
    <div className="sf-host-page sf-billing-page">
      <div className="sf-host-page-top">
        <header className="sf-organization-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Plan Comparison</h1>
            <p className="sf-host-muted-note">Compare plans and run Stripe-backed upgrade or downgrade safely.</p>
          </div>
          <div className="sf-organization-header-actions">
            <a href="/host/settings/billing">Back to Dashboard</a>
            <button type="button" onClick={() => auth.logout()}>Sign out</button>
          </div>
        </header>

        <HostConsoleNav
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          accountSettingsHref="/host/settings/account"
          current="billing"
        />

        {billing.error ? <div className="sf-host-inline-error" role="alert"><p>{billing.error}</p></div> : null}
        {billing.message ? <div className="sf-whatsapp-status" role="status"><p>{billing.message}</p></div> : null}

        <section className="sf-billing-grid" aria-label="Plan comparison">
          <article className="sf-organization-card">
            <h2>Available Plans</h2>
            <p className="sf-host-muted-note">Current plan: <strong>{currentPlan ?? "None"}</strong></p>
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
                    <button
                      type="button"
                      onClick={() => setPendingPlan(plan.name)}
                      disabled={isBusy || !subscription?.hasStripeSubscription || plan.name.toLowerCase() === (currentPlan ?? "").toLowerCase()}>
                      {actionLabel(currentPlan, plan.name)} Plan
                    </button>
                    <button type="button" onClick={() => void billing.openCheckout(plan.name, plan.trialDays)} disabled={isBusy || !subscription?.canStartCheckout}>Start Trial / Checkout</button>
                  </div>
                </div>
              ))}
            </div>
          </article>
        </section>

        {pendingPlan ? (
          <dialog open className="sf-billing-dialog" aria-labelledby="plan-dialog-title">
            <h2 id="plan-dialog-title">Confirm {pendingAction}</h2>
            <p>
              You are about to {pendingAction.toLowerCase()} from <strong>{currentPlan ?? "no active plan"}</strong> to <strong>{pendingPlan}</strong>.
              Stripe proration will be applied automatically.
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
