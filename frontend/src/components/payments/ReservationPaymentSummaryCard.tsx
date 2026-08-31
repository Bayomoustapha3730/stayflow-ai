import type { ReservationPaymentSummary } from "../../models/payments";
import { isPaidInFull } from "../../models/payments";

interface ReservationPaymentSummaryCardProps {
  summary: ReservationPaymentSummary | null;
  formatMoney: (amount: number, currency: string) => string;
}

export function ReservationPaymentSummaryCard({
  summary,
  formatMoney
}: ReservationPaymentSummaryCardProps) {
  if (!summary || summary.bookingAmount == null) {
    return null;
  }

  const currency = summary.currency || "KES";
  const paidInFull = isPaidInFull(summary);
  const remaining = summary.remainingBalance ?? 0;

  return (
    <div
      className={`reservation-payment-summary reservation-payment-summary-${paidInFull ? "paid" : "due"}`}
      data-payment-summary-status={paidInFull ? "paid-in-full" : "balance-due"}
    >
      <div className="reservation-payment-summary-row">
        <span className="mpesa-payment-label">Reservation balance</span>

        <span
          className={`reservation-payment-status-badge reservation-payment-status-${paidInFull ? "paid" : "due"}`}
          data-testid="reservation-payment-status"
        >
          {paidInFull ? "PAID IN FULL" : "Balance Due"}
        </span>
      </div>

      <dl className="reservation-payment-summary-figures">
        <div>
          <dt>Booking total</dt>
          <dd data-testid="reservation-booking-total">
            {formatMoney(summary.bookingAmount, currency)}
          </dd>
        </div>

        <div>
          <dt>Paid</dt>
          <dd data-testid="reservation-total-paid">
            {formatMoney(summary.totalPaid, currency)}
          </dd>
        </div>

        <div>
          <dt>Remaining</dt>
          <dd data-testid="reservation-remaining-balance">
            {formatMoney(remaining, currency)}
          </dd>
        </div>
      </dl>
    </div>
  );
}
