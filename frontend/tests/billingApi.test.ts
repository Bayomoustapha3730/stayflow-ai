import { describe, expect, it, vi } from "vitest";
import { createBillingApi } from "../src/api/billingApi";

describe("billingApi", () => {
  it("calls subscription endpoint", async () => {
    const get = vi.fn().mockResolvedValue({ status: "Active" });
    const api = createBillingApi({ get, post: vi.fn(), put: vi.fn(), delete: vi.fn() } as never);

    await api.getSubscription();

    expect(get).toHaveBeenCalledWith("/api/billing/subscription");
  });

  it("calls plan change endpoint", async () => {
    const post = vi.fn().mockResolvedValue({ status: "Active" });
    const api = createBillingApi({ get: vi.fn(), post, put: vi.fn(), delete: vi.fn() } as never);

    await api.changePlan({ planName: "Growth" });

    expect(post).toHaveBeenCalledWith("/api/billing/subscription/change-plan", { planName: "Growth" });
  });
});
