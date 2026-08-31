import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MpesaPaymentPanel } from "../src/components/payments/MpesaPaymentPanel";
import type { Payment, ReservationPaymentSummary } from "../src/models/payments";

const reservationId = "55555555-5555-5555-5555-555555555556";

function summary(overrides: Partial<ReservationPaymentSummary> = {}): ReservationPaymentSummary {
  return {
    reservationId,
    bookingAmount: 4000,
    currency: "KES",
    totalPaid: 4000,
    remainingBalance: 0,
    hasSuccessfulPayment: true,
    paymentCount: 1,
    ...overrides
  };
}

function paidPayment(): Payment {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    reservationId,
    propertyId: "22222222-2222-2222-2222-222222222222",
    guestId: "44444444-4444-4444-4444-444444444444",
    amount: 4000,
    currency: "KES",
    provider: "M-PESA",
    paymentMethod: "STKPush",
    status: "Paid",
    providerTransactionId: "QWE123456",
    createdAt: new Date().toISOString()
  };
}

function renderPanel(
  paymentSummary: ReservationPaymentSummary | null,
  payments: Payment[] = []
) {
  return render(
    <MpesaPaymentPanel
      payments={payments}
      summary={paymentSummary}
      isLoading={false}
      isSubmitting={false}
      error={null}
      onRequestPayment={async () => null}
      onRefresh={async () => {}}
      onClearError={() => {}}
    />
  );
}

describe("MpesaPaymentPanel reservation payment summary", () => {
  it("renders PAID IN FULL with zero remaining balance", () => {
    renderPanel(summary(), [paidPayment()]);

    expect(screen.getByTestId("reservation-payment-status")).toHaveTextContent("PAID IN FULL");
    expect(screen.getByTestId("reservation-remaining-balance")).toHaveTextContent("0");
    expect(screen.getByTestId("reservation-total-paid")).toHaveTextContent("4,000");
    expect(screen.getByTestId("reservation-booking-total")).toHaveTextContent("4,000");
  });

  it("blocks a further payment request once paid in full", () => {
    renderPanel(summary(), [paidPayment()]);

    expect(screen.getByRole("button", { name: "Paid in full" })).toBeDisabled();
  });

  it("renders Balance Due with the grounded remaining balance for a partial payment", () => {
    renderPanel(
      summary({ totalPaid: 1000, remainingBalance: 3000 }),
      [{ ...paidPayment(), amount: 1000 }]
    );

    expect(screen.getByTestId("reservation-payment-status")).toHaveTextContent("Balance Due");
    expect(screen.getByTestId("reservation-total-paid")).toHaveTextContent("1,000");
    expect(screen.getByTestId("reservation-remaining-balance")).toHaveTextContent("3,000");
  });

  it("does not infer paid status when the backend summary is unavailable", () => {
    renderPanel(null, [paidPayment()]);

    expect(screen.queryByTestId("reservation-payment-status")).not.toBeInTheDocument();
  });

  it("does not render a summary when the reservation has no booking amount", () => {
    renderPanel(summary({ bookingAmount: null, remainingBalance: null }));

    expect(screen.queryByTestId("reservation-payment-status")).not.toBeInTheDocument();
  });

  it("reconciles to PAID IN FULL when a refreshed summary arrives", () => {
    const { rerender } = renderPanel(summary({ totalPaid: 1000, remainingBalance: 3000 }));

    expect(screen.getByTestId("reservation-payment-status")).toHaveTextContent("Balance Due");

    const paidSummary = summary();

    rerender(
      <MpesaPaymentPanel
        payments={[paidPayment()]}
        summary={paidSummary}
        isLoading={false}
        isSubmitting={false}
        error={null}
        onRequestPayment={async () => null}
        onRefresh={async () => {}}
        onClearError={() => {}}
      />
    );

    expect(screen.getByTestId("reservation-payment-status")).toHaveTextContent("PAID IN FULL");
    expect(screen.getByTestId("reservation-remaining-balance")).toHaveTextContent("0");

    // A duplicate realtime-driven update carrying the same grounded snapshot must not corrupt the UI.
    rerender(
      <MpesaPaymentPanel
        payments={[paidPayment()]}
        summary={{ ...paidSummary }}
        isLoading={false}
        isSubmitting={false}
        error={null}
        onRequestPayment={async () => null}
        onRefresh={async () => {}}
        onClearError={() => {}}
      />
    );

    expect(screen.getByTestId("reservation-payment-status")).toHaveTextContent("PAID IN FULL");
    expect(screen.getAllByTestId("reservation-payment-status")).toHaveLength(1);
    expect(screen.getByTestId("reservation-total-paid")).toHaveTextContent("4,000");
  });
});
