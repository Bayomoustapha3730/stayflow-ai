export interface CreateCheckoutSessionRequest {
  planName: string;
  trialDays?: number;
  paymentMethod?: string;
}

export interface CreateCheckoutSessionResponse {
  checkoutUrl: string;
  provider: string;
}

export interface CreateBillingPortalSessionResponse {
  portalUrl: string;
  provider: string;
}

export interface BillingSubscriptionResponse {
  companyId: string;
  status: string;
  cancelAtPeriodEnd: boolean;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  trialEndsAtUtc?: string | null;
  planName?: string | null;
  externalSubscriptionId?: string | null;
  externalPriceId?: string | null;
}

export interface BillingPlanResponse {
  name: string;
  displayName: string;
  description: string;
  sortOrder: number;
  isEnterprise: boolean;
  isCurrentPlan: boolean;
  currency: string;
  monthlyAmountMinor?: number | null;
  trialDays?: number | null;
  propertyLimit?: number | null;
  teamLimit?: number | null;
  aiRequestLimit?: number | null;
  whatsAppMessageLimit?: number | null;
}

export interface BillingPaymentOptionResponse {
  key: string;
  label: string;
  description: string;
}

export interface TenantInvoiceDto {
  id: string;
  externalInvoiceId: string;
  status: string;
  amountDue: number;
  amountPaid: number;
  currency: string;
  periodStartUtc?: string | null;
  periodEndUtc?: string | null;
  paidAtUtc?: string | null;
  failedAtUtc?: string | null;
  createdAt: string;
}

export interface UsageMetricSummaryDto {
  metric: string;
  entitlementKey: string;
  used: number;
  limit?: number | null;
  remaining?: number | null;
  isUnlimited: boolean;
  unit: string;
  periodStartUtc: string;
  periodEndUtc: string;
}

export interface UsageSummaryResponse {
  companyId: string;
  generatedAtUtc: string;
  metrics: UsageMetricSummaryDto[];
}

export interface ChangeSubscriptionPlanRequest {
  planName: string;
}

export interface CancelSubscriptionRequest {
  atPeriodEnd: boolean;
}
