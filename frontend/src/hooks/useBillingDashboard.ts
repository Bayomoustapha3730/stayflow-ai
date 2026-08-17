import { useCallback, useEffect, useMemo, useState } from "react";
import { createBillingApi } from "../api/billingApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import type {
  BillingPaymentOptionResponse,
  BillingSubscriptionResponse,
  TenantInvoiceDto,
  UsageSummaryResponse
} from "../models/billing";

export interface UseBillingDashboardOptions {
  accessToken: string | null;
  onUnauthorized?: () => void;
}

export function useBillingDashboard({ accessToken, onUnauthorized }: UseBillingDashboardOptions) {
  const [subscription, setSubscription] = useState<BillingSubscriptionResponse | null>(null);
  const [paymentOptions, setPaymentOptions] = useState<BillingPaymentOptionResponse[]>([]);
  const [invoices, setInvoices] = useState<TenantInvoiceDto[]>([]);
  const [usage, setUsage] = useState<UsageSummaryResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isMutating, setIsMutating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);

  const api = useMemo(() => createBillingApi(http), [http]);

  const handleFailure = useCallback((failure: unknown, fallback: string) => {
    if (failure instanceof ApiError && failure.status === 401) {
      onUnauthorized?.();
    }

    setError(failure instanceof Error ? failure.message : fallback);
  }, [onUnauthorized]);

  const refresh = useCallback(async () => {
    if (!accessToken) {
      setSubscription(null);
      setInvoices([]);
      setUsage(null);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const [subscriptionData, paymentOptionsData, invoiceData, usageData] = await Promise.all([
        api.getSubscription(),
        api.getPaymentOptions(),
        api.getInvoices(),
        api.getUsageSummary()
      ]);
      setSubscription(subscriptionData);
      setPaymentOptions(paymentOptionsData);
      setInvoices(invoiceData);
      setUsage(usageData);
    } catch (failure) {
      handleFailure(failure, "Unable to load billing data.");
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, api, handleFailure]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const withMutation = useCallback(async <T,>(action: () => Promise<T>, successMessage: string): Promise<T | null> => {
    if (!accessToken) {
      return null;
    }

    setIsMutating(true);
    setError(null);
    setMessage(null);

    try {
      const result = await action();
      setMessage(successMessage);
      return result;
    } catch (failure) {
      handleFailure(failure, "Billing action failed.");
      return null;
    } finally {
      setIsMutating(false);
    }
  }, [accessToken, handleFailure]);

  const openCheckout = useCallback(async (planName: string, trialDays?: number) => {
    const result = await withMutation(
      () => api.createCheckoutSession({ planName, trialDays }),
      `${planName} checkout session ready.`
    );

    if (result?.checkoutUrl) {
      window.location.assign(result.checkoutUrl);
    }
  }, [api, withMutation]);

  const openBillingPortal = useCallback(async () => {
    const result = await withMutation(
      () => api.createBillingPortalSession(),
      "Billing portal session created."
    );

    if (result?.portalUrl) {
      window.location.assign(result.portalUrl);
    }
  }, [api, withMutation]);

  const openPaymentMethodPortal = useCallback(async () => {
    const result = await withMutation(
      () => api.createPaymentMethodPortalSession(),
      "Payment method portal session created."
    );

    if (result?.portalUrl) {
      window.location.assign(result.portalUrl);
    }
  }, [api, withMutation]);

  const changePlan = useCallback(async (planName: string) => {
    const result = await withMutation(
      () => api.changePlan({ planName }),
      `Plan updated to ${planName}.`
    );

    if (result) {
      setSubscription(result);
    }
  }, [api, withMutation]);

  const cancelSubscription = useCallback(async (atPeriodEnd: boolean) => {
    const result = await withMutation(
      () => api.cancelSubscription({ atPeriodEnd }),
      atPeriodEnd ? "Subscription will cancel at period end." : "Subscription canceled immediately."
    );

    if (result) {
      setSubscription(result);
    }
  }, [api, withMutation]);

  const resumeSubscription = useCallback(async () => {
    const result = await withMutation(
      () => api.resumeSubscription(),
      "Subscription resumed."
    );

    if (result) {
      setSubscription(result);
    }
  }, [api, withMutation]);

  return {
    subscription,
    paymentOptions,
    invoices,
    usage,
    isLoading,
    isMutating,
    error,
    message,
    refresh,
    openCheckout,
    openBillingPortal,
    openPaymentMethodPortal,
    changePlan,
    cancelSubscription,
    resumeSubscription
  };
}
