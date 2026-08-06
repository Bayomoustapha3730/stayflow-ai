import type {
  BillingSubscriptionResponse,
  CancelSubscriptionRequest,
  ChangeSubscriptionPlanRequest,
  CreateBillingPortalSessionResponse,
  CreateCheckoutSessionRequest,
  CreateCheckoutSessionResponse,
  TenantInvoiceDto,
  UsageSummaryResponse
} from "../models/billing";
import type { HttpClient } from "./httpClient";

export function createBillingApi(http: HttpClient) {
  return {
    getSubscription() {
      return http.get<BillingSubscriptionResponse>("/api/billing/subscription");
    },
    getInvoices() {
      return http.get<TenantInvoiceDto[]>("/api/billing/invoices");
    },
    getUsageSummary() {
      return http.get<UsageSummaryResponse>("/api/billing/usage");
    },
    createCheckoutSession(request: CreateCheckoutSessionRequest) {
      return http.post<CreateCheckoutSessionResponse>("/api/billing/checkout", request);
    },
    createBillingPortalSession() {
      return http.post<CreateBillingPortalSessionResponse>("/api/billing/portal");
    },
    createPaymentMethodPortalSession() {
      return http.post<CreateBillingPortalSessionResponse>("/api/billing/portal/payment-method");
    },
    changePlan(request: ChangeSubscriptionPlanRequest) {
      return http.post<BillingSubscriptionResponse>("/api/billing/subscription/change-plan", request);
    },
    cancelSubscription(request: CancelSubscriptionRequest) {
      return http.post<BillingSubscriptionResponse>("/api/billing/subscription/cancel", request);
    },
    resumeSubscription() {
      return http.post<BillingSubscriptionResponse>("/api/billing/subscription/resume");
    }
  };
}
