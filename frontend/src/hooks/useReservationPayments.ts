import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState
} from "react";
import { getRuntimeApiUrl } from "../runtimeConfig";
import { createPaymentsApi } from "../api/paymentsApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type {
  InitiateMpesaPaymentRequest,
  Payment
} from "../models/payments";
import { isActivePaymentStatus } from "../models/payments";

interface UseReservationPaymentsOptions {
  reservationId: string | null | undefined;
  accessToken: string | null;
  onUnauthorized: () => void;
  pollingIntervalMs?: number;
}

export interface UseReservationPaymentsResult {
  payments: Payment[];
  isLoading: boolean;
  isSubmitting: boolean;
  error: string | null;
  sessionExpired: boolean;
  hasActivePayment: boolean;
  refresh: () => Promise<void>;
  initiateMpesaPayment: (
    request: Omit<InitiateMpesaPaymentRequest, "reservationId">
  ) => Promise<Payment | null>;
  clearError: () => void;
}

export function useReservationPayments({
  reservationId,
  accessToken,
  onUnauthorized,
  pollingIntervalMs = 5000
}: UseReservationPaymentsOptions): UseReservationPaymentsResult {
  const [payments, setPayments] = useState<Payment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  const requestVersion = useRef(0);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: getRuntimeApiUrl(),
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createPaymentsApi(http), [http]);

  const handleFailure = useCallback(
    (failure: unknown, fallback: string) => {
      if (failure instanceof ApiError && failure.status === 401) {
        setSessionExpired(true);
        onUnauthorized();
        return;
      }

      setError(
        failure instanceof Error
          ? failure.message
          : fallback
      );
    },
    [onUnauthorized]
  );

  const loadPayments = useCallback(async () => {
    if (!reservationId || !accessToken) {
      setPayments([]);
      setError(null);
      setSessionExpired(false);
      return;
    }

    const version = ++requestVersion.current;

    setIsLoading(true);
    setError(null);

    try {
      const result =
        await api.listReservationPayments(reservationId);

      if (version !== requestVersion.current) {
        return;
      }

      setPayments(Array.isArray(result) ? result : []);
    } catch (failure) {
      if (version !== requestVersion.current) {
        return;
      }

      handleFailure(
        failure,
        "Unable to load reservation payments."
      );
    } finally {
      if (version === requestVersion.current) {
        setIsLoading(false);
      }
    }
  }, [
    accessToken,
    api,
    handleFailure,
    reservationId
  ]);

  useEffect(() => {
    void loadPayments();
  }, [loadPayments]);

  const hasActivePayment = payments.some((payment) =>
    isActivePaymentStatus(payment.status)
  );

  useEffect(() => {
    if (
      !reservationId ||
      !accessToken ||
      !hasActivePayment
    ) {
      return;
    }

    const timer = window.setInterval(() => {
      void loadPayments();
    }, pollingIntervalMs);

    return () => window.clearInterval(timer);
  }, [
    accessToken,
    hasActivePayment,
    loadPayments,
    pollingIntervalMs,
    reservationId
  ]);

  const initiateMpesaPayment = useCallback(
    async (
      request: Omit<
        InitiateMpesaPaymentRequest,
        "reservationId"
      >
    ): Promise<Payment | null> => {
      if (!reservationId || !accessToken) {
        return null;
      }

      setIsSubmitting(true);
      setError(null);

      try {
        const payment = await api.initiateMpesaPayment({
          ...request,
          reservationId
        });

        setPayments((current) => [
          payment,
          ...current.filter(
            (existing) => existing.id !== payment.id
          )
        ]);

        return payment;
      } catch (failure) {
        handleFailure(
          failure,
          "Unable to request the M-PESA payment."
        );

        return null;
      } finally {
        setIsSubmitting(false);
      }
    },
    [
      accessToken,
      api,
      handleFailure,
      reservationId
    ]
  );

  const refresh = useCallback(async () => {
    await loadPayments();
  }, [loadPayments]);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  return {
    payments,
    isLoading,
    isSubmitting,
    error,
    sessionExpired,
    hasActivePayment,
    refresh,
    initiateMpesaPayment,
    clearError
  };
}
