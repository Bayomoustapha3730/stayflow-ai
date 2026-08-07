import { describe, expect, it, vi } from "vitest";
import { createOnboardingApi } from "../src/api/onboardingApi";

describe("onboardingApi", () => {
  it("calls onboarding status endpoint", async () => {
    const get = vi.fn().mockResolvedValue({ currentStep: "Welcome" });
    const api = createOnboardingApi({ get, post: vi.fn(), put: vi.fn(), delete: vi.fn() } as never);

    await api.getStatus();

    expect(get).toHaveBeenCalledWith("/api/onboarding/status");
  });

  it("calls onboarding step skip endpoint", async () => {
    const post = vi.fn().mockResolvedValue({ currentStep: "Review" });
    const api = createOnboardingApi({ get: vi.fn(), post, put: vi.fn(), delete: vi.fn() } as never);

    await api.skipStep("TeamInvitations", { reason: "later" });

    expect(post).toHaveBeenCalledWith("/api/onboarding/steps/TeamInvitations/skip", { reason: "later" });
  });
});
