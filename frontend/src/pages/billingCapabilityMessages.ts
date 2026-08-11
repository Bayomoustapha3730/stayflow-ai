import type { BillingSubscriptionResponse } from "../models/billing";

export function getBillingCapabilityMessage(
  subscription?: BillingSubscriptionResponse | null,
  selectedPlanName?: string | null
): string {
  if (!subscription) {
    if (selectedPlanName) {
      return `Your selected plan is ${selectedPlanName}. Checkout is still required to activate it.`;
    }

    return "You’re on the Free plan. Start checkout when you’re ready to activate a paid subscription.";
  }

  if (!subscription.hasStripeCustomer && !subscription.hasStripeSubscription) {
    return "You’re on the Free plan. Start checkout when you’re ready to activate a paid subscription.";
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
