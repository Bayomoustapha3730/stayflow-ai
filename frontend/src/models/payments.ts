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

export interface InitiateMpesaPaymentRequest {
  reservationId: string;
  customerPhoneNumber: string;
  description?: string;
  idempotencyKey?: string;
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
