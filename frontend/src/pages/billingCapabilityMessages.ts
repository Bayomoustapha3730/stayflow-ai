import type { BillingSubscriptionResponse } from "../models/billing";

function normalizeSubscriptionStatus(value?: string | null): string {
  return (value ?? "").trim().toLowerCase();
}

export function getBillingCapabilityMessage(
  subscription?: BillingSubscriptionResponse | null,
  selectedPlanName?: string | null
): string {
  if (!subscription) {
    if (selectedPlanName) {
      return `Your selected plan is ${selectedPlanName}. Checkout is still required to activate it.`;
    }

    return "You’re on the Free plan. No checkout is required to keep using it.";
  }

  if (!subscription.capability.checkoutAvailable) {
    return subscription.capability.message;
  }

  const status = normalizeSubscriptionStatus(subscription.status);
  const isCancelAtPeriodEnd = subscription.cancelAtPeriodEnd || status.includes("cancelatperiodend") || status.includes("cancel_at_period_end") || (status.includes("scheduled") && status.includes("cancel"));

  if (status.includes("pastdue") || status.includes("past_due") || status.includes("overdue")) {
    return "Your payment is past due. Update your payment method to restore service.";
  }

  if (status.includes("trial")) {
    return "Your trial is active. Add a payment method before it ends to keep access.";
  }

  if (isCancelAtPeriodEnd) {
    return "Your subscription is scheduled to end at the close of the current billing period.";
  }

  if (status.includes("cancelled") || status.includes("canceled")) {
    return "Your subscription has been cancelled. You can review billing history or start a new plan when you’re ready.";
  }

  if (status.includes("suspend")) {
    return "Your subscription is suspended. Update billing details or contact support to restore access.";
  }

  if (!subscription.hasStripeCustomer && !subscription.hasStripeSubscription) {
    return "You’re on the Free plan. No checkout is required to keep using it.";
  }

  if (subscription.canResume) {
    return "Your subscription is paused and ready to resume.";
  }

  if (subscription.canCancel) {
    return "Your billing relationship is active. You can manage or adjust it from here.";
  }

  if (subscription.canOpenBillingPortal || subscription.canManagePaymentMethod) {
    return "Your billing relationship is active. Use the portal to review or update billing details.";
  }

  if (subscription.canStartCheckout) {
    return "You can start checkout for a paid plan when you’re ready.";
  }

  return "Billing is ready for your current subscription.";
}
