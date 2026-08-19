import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MpesaPaymentPanel } from "../src/components/payments/MpesaPaymentPanel";
import type { Payment } from "../src/models/payments";

function createPayment(
  overrides: Partial<Payment> = {}
): Payment {
  return {
    id: "payment-1",
    reservationId: "reservation-1",
    propertyId: "property-1",
    guestId: "guest-1",
    amount: 5000,
    currency: "KES",
    provider: "M-PESA",
    paymentMethod: "STK Push",
    status: "Paid",
    providerTransactionId: null,
    customerPhoneNumber: "254711985343",
    internalReference: "STAYFLOW-001",
    failureMessage: null,
    requestedAtUtc: "2026-08-18T18:00:00Z",
    completedAtUtc: null,
    failedAtUtc: null,
    cancelledAtUtc: null,
    createdAt: "2026-08-18T18:00:00Z",
    ...overrides
  };
}

function renderPanel(
  payments: Payment[] = [],
  onRequestPayment = vi.fn().mockResolvedValue(null)
) {
  render(
    <MpesaPaymentPanel
      payments={payments}
      isLoading={false}
      isSubmitting={false}
      error={null}
      onRequestPayment={onRequestPayment}
      onRefresh={vi.fn().mockResolvedValue(undefined)}
      onClearError={vi.fn()}
    />
  );

  return { onRequestPayment };
}

describe("MpesaPaymentPanel", () => {
  it("normalizes a Kenyan local phone number before requesting payment", async () => {
    const onRequestPayment = vi.fn().mockResolvedValue(
      createPayment({
        status: "Pending"
      })
    );

    renderPanel([], onRequestPayment);

    const phone = screen.getByRole("textbox", {
      name: /guest safaricom phone/i
    });

    await userEvent.type(phone, "0711985343");

    await userEvent.click(
      screen.getByRole("button", {
        name: /request m-pesa payment/i
      })
    );

    expect(onRequestPayment).toHaveBeenCalledTimes(1);

    expect(onRequestPayment).toHaveBeenCalledWith(
      expect.objectContaining({
        customerPhoneNumber: "254711985343",
        description: "StayFlow reservation payment",
        idempotencyKey: expect.stringMatching(/^stayflow-ui-/)
      })
    );
  });

  it("rejects an invalid Kenyan phone number", async () => {
    const { onRequestPayment } = renderPanel();

    await userEvent.type(
      screen.getByRole("textbox", {
        name: /guest safaricom phone/i
      }),
      "12345"
    );

    await userEvent.click(
      screen.getByRole("button", {
        name: /request m-pesa payment/i
      })
    );

    expect(
      screen.getByText(/enter a valid kenyan safaricom number/i)
    ).toBeInTheDocument();

    expect(onRequestPayment).not.toHaveBeenCalled();
  });

  it.each([
    ["Pending", "Pending"],
    ["Processing", "Processing"],
    ["Paid", "Paid"],
    ["Failed", "Failed"],
    ["Cancelled", "Cancelled"],
    ["Expired", "Expired"]
  ])("renders %s payment status", (status, label) => {
    renderPanel([
      createPayment({
        status
      })
    ]);

    expect(
      screen.getByText(label, {
        selector: "[data-payment-status]"
      })
    ).toBeInTheDocument();
  });

  it("disables new payment requests while a payment is active", () => {
    renderPanel([
      createPayment({
        status: "Processing"
      })
    ]);

    expect(
      screen.getByRole("button", {
        name: /payment in progress/i
      })
    ).toBeDisabled();

    expect(
      screen.getByRole("textbox", {
        name: /guest safaricom phone/i
      })
    ).toBeDisabled();
  });

  it("shows provider failure details", () => {
    renderPanel([
      createPayment({
        status: "Failed",
        failureMessage: "DS timeout user cannot be reached."
      })
    ]);

    expect(
      screen.getByText("DS timeout user cannot be reached.")
    ).toBeInTheDocument();
  });

  it("shows the M-PESA receipt when payment succeeds", () => {
    renderPanel([
      createPayment({
        status: "Paid",
        providerTransactionId: "RKX1234567"
      })
    ]);

    expect(
      screen.getByText("RKX1234567")
    ).toBeInTheDocument();
  });
});
