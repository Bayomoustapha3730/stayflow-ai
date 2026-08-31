export type PaymentStatus =
  | "Pending"
  | "Processing"
  | "Paid"
  | "Failed"
  | "Cancelled"
  | "Expired";

export interface Payment {
  id: string;
  reservationId?: string | null;
  propertyId: string;
  guestId: string;
  amount: number;
  currency: string;
  provider: string;
  paymentMethod: string;
  status: PaymentStatus | string;
  providerTransactionId?: string | null;
  customerPhoneNumber?: string | null;
  internalReference?: string | null;
  failureMessage?: string | null;
  requestedAtUtc?: string | null;
  completedAtUtc?: string | null;
  failedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  createdAt: string;
}

/// Backend-grounded payment snapshot for a reservation. The backend is the source of truth for
/// paid/balance-due status; the UI must never recompute it from the raw payment list.
export interface ReservationPaymentSummary {
  reservationId: string;
  bookingAmount?: number | null;
  currency: string;
  totalPaid: number;
  remainingBalance?: number | null;
  hasSuccessfulPayment: boolean;
  paymentCount: number;
  latestPaymentStatus?: string | null;
  latestPaymentAmount?: number | null;
  latestPaymentRequestedAtUtc?: string | null;
  latestPaymentCompletedAtUtc?: string | null;
  latestProvider?: string | null;
  latestPaymentMethod?: string | null;
  latestReceiptNumber?: string | null;
  latestFailureMessage?: string | null;
}

export interface InitiateMpesaPaymentRequest {
  reservationId: string;
  customerPhoneNumber: string;
  description?: string;
  idempotencyKey?: string;
}

export function isPaidInFull(summary?: ReservationPaymentSummary | null): boolean {
  if (!summary || summary.bookingAmount == null || summary.remainingBalance == null) {
    return false;
  }

  return summary.hasSuccessfulPayment && summary.remainingBalance <= 0;
}

export function isActivePaymentStatus(status?: string | null): boolean {
  const normalized = status?.trim().toLowerCase();
  return normalized === "pending" || normalized === "processing";
}

export function isTerminalPaymentStatus(status?: string | null): boolean {
  const normalized = status?.trim().toLowerCase();

  return (
    normalized === "paid" ||
    normalized === "failed" ||
    normalized === "cancelled" ||
    normalized === "expired"
  );
}
