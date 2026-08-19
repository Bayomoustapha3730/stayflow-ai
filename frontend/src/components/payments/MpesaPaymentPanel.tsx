import {
  FormEvent,
  useMemo,
  useState
} from "react";
import type { Payment } from "../../models/payments";
import { isActivePaymentStatus } from "../../models/payments";
import { PaymentStatusBadge } from "./PaymentStatusBadge";

interface MpesaPaymentPanelProps {
  payments: Payment[];
  isLoading: boolean;
  isSubmitting: boolean;
  error: string | null;
  onRequestPayment: (input: {
    customerPhoneNumber: string;
    description?: string;
    idempotencyKey?: string;
  }) => Promise<Payment | null>;
  onRefresh: () => Promise<void>;
  onClearError: () => void;
}

function normalizeKenyanPhone(value: string): string | null {
  const digits = value.replace(/\D/g, "");

  if (/^2547\d{8}$/.test(digits)) {
    return digits;
  }

  if (/^07\d{8}$/.test(digits)) {
    return `254${digits.slice(1)}`;
  }

  if (/^7\d{8}$/.test(digits)) {
    return `254${digits}`;
  }

  return null;
}

function formatMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat("en-KE", {
      style: "currency",
      currency: currency || "KES",
      maximumFractionDigits: 2
    }).format(amount);
  } catch {
    return `${currency || "KES"} ${amount.toFixed(2)}`;
  }
}

function formatDate(value?: string | null) {
  if (!value) {
    return null;
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("en-KE", {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(date);
}

export function MpesaPaymentPanel({
  payments,
  isLoading,
  isSubmitting,
  error,
  onRequestPayment,
  onRefresh,
  onClearError
}: MpesaPaymentPanelProps) {
  const [phone, setPhone] = useState("");
  const [phoneError, setPhoneError] = useState<string | null>(null);

  const latestPayment = payments[0] ?? null;

  const hasActivePayment = useMemo(
    () => payments.some((payment) => isActivePaymentStatus(payment.status)),
    [payments]
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedPhone = normalizeKenyanPhone(phone);

    if (!normalizedPhone) {
      setPhoneError(
        "Enter a valid Kenyan Safaricom number, for example 0712345678 or 254712345678."
      );
      return;
    }

    setPhoneError(null);
    onClearError();

    const payment = await onRequestPayment({
      customerPhoneNumber: normalizedPhone,
      description: "StayFlow reservation payment",
      idempotencyKey: `stayflow-ui-${Date.now()}`
    });

    if (payment) {
      setPhone("");
    }
  }

  return (
    <section className="mpesa-payment-panel" aria-labelledby="mpesa-payment-title">
      <div className="mpesa-payment-header">
        <div>
          <p className="mpesa-payment-eyebrow">Guest payment</p>
          <h2 id="mpesa-payment-title">M-PESA</h2>
          <p>
            Send an STK Push directly to the guest&apos;s Safaricom phone.
          </p>
        </div>

        <button
          type="button"
          className="mpesa-payment-refresh"
          onClick={() => void onRefresh()}
          disabled={isLoading}
        >
          {isLoading ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {latestPayment ? (
        <div className="mpesa-payment-latest">
          <div className="mpesa-payment-summary-row">
            <div>
              <span className="mpesa-payment-label">Latest payment</span>
              <strong>
                {formatMoney(
                  latestPayment.amount,
                  latestPayment.currency
                )}
              </strong>
            </div>

            <PaymentStatusBadge status={latestPayment.status} />
          </div>

          <dl className="mpesa-payment-details">
            <div>
              <dt>Phone</dt>
              <dd>{latestPayment.customerPhoneNumber || "—"}</dd>
            </div>

            <div>
              <dt>Requested</dt>
              <dd>{formatDate(latestPayment.requestedAtUtc) || "—"}</dd>
            </div>

            <div>
              <dt>M-PESA receipt</dt>
              <dd>{latestPayment.providerTransactionId || "—"}</dd>
            </div>

            <div>
              <dt>Reference</dt>
              <dd>{latestPayment.internalReference || "—"}</dd>
            </div>
          </dl>

          {latestPayment.failureMessage ? (
            <div className="mpesa-payment-failure" role="alert">
              {latestPayment.failureMessage}
            </div>
          ) : null}

          {isActivePaymentStatus(latestPayment.status) ? (
            <p className="mpesa-payment-progress">
              StayFlow is waiting for M-PESA to finalize this transaction.
              The status will refresh automatically.
            </p>
          ) : null}
        </div>
      ) : (
        <div className="mpesa-payment-empty">
          No M-PESA payment has been requested for this reservation yet.
        </div>
      )}

      <form className="mpesa-payment-form" onSubmit={handleSubmit}>
        <label htmlFor="mpesa-phone">
          Guest Safaricom phone
        </label>

        <div className="mpesa-payment-input-row">
          <input
            id="mpesa-phone"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            placeholder="0712345678"
            value={phone}
            onChange={(event) => {
              setPhone(event.target.value);
              setPhoneError(null);
            }}
            disabled={isSubmitting || hasActivePayment}
          />

          <button
            type="submit"
            disabled={
              isSubmitting ||
              hasActivePayment ||
              !phone.trim()
            }
          >
            {isSubmitting
              ? "Sending…"
              : hasActivePayment
                ? "Payment in progress"
                : "Request M-PESA payment"}
          </button>
        </div>

        {phoneError ? (
          <p className="mpesa-payment-validation" role="alert">
            {phoneError}
          </p>
        ) : null}

        {error ? (
          <div className="mpesa-payment-error" role="alert">
            <span>{error}</span>
            <button type="button" onClick={onClearError}>
              Dismiss
            </button>
          </div>
        ) : null}
      </form>

      {payments.length > 1 ? (
        <div className="mpesa-payment-history">
          <h3>Payment history</h3>

          <ul>
            {payments.map((payment) => (
              <li key={payment.id}>
                <div>
                  <strong>
                    {formatMoney(payment.amount, payment.currency)}
                  </strong>
                  <span>{formatDate(payment.createdAt)}</span>
                </div>

                <PaymentStatusBadge status={payment.status} />
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}
