import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ActionConfirmationCard } from "../src/components/ActionConfirmationCard";
import { ConciergeActionConfirmationRequirement, ConciergeActionType, PendingConciergeActionStatus } from "../src/models/chat";

describe("ActionConfirmationCard", () => {
  it("renders prompt and fires confirm/cancel actions", async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    const user = userEvent.setup();

    render(
      <ActionConfirmationCard
        pendingAction={{
          actionId: "11111111-1111-1111-1111-111111111111",
          actionType: ConciergeActionType.RequestEarlyCheckIn,
          status: PendingConciergeActionStatus.AwaitingGuestConfirmation,
          confirmationRequirement: ConciergeActionConfirmationRequirement.Both,
          prompt: "I can submit an early check-in request for 12:00. Should I submit it?",
          requiresHostApproval: true,
          expiresAt: "2026-01-01T10:00:00Z"
        }}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    expect(screen.getByText(/early check-in request/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /confirm/i }));
    await user.click(screen.getByRole("button", { name: /cancel/i }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("disables buttons when disabled", () => {
    render(
      <ActionConfirmationCard
        pendingAction={{
          actionId: "11111111-1111-1111-1111-111111111111",
          actionType: ConciergeActionType.NotifyHost,
          status: PendingConciergeActionStatus.AwaitingGuestConfirmation,
          confirmationRequirement: ConciergeActionConfirmationRequirement.ExplicitGuestConfirmation,
          prompt: "Should I notify the host?",
          requiresHostApproval: false,
          expiresAt: "2026-01-01T10:00:00Z"
        }}
        disabled
        onConfirm={() => undefined}
        onCancel={() => undefined}
      />
    );

    expect(screen.getByRole("button", { name: /confirm/i })).toBeDisabled();
    expect(screen.getByRole("button", { name: /cancel/i })).toBeDisabled();
  });
});
