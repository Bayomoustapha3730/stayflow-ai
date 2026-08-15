import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const switchOrganization = vi.fn().mockResolvedValue(true);
const createOrganization = vi.fn().mockResolvedValue(true);
const refreshOrganizations = vi.fn().mockResolvedValue(undefined);

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => ({
    accessToken: "token",
    currentUser: {
      id: "user-1",
      companyId: "company-1",
      fullName: "Owner User",
      email: "owner@test",
      phoneNumber: "+254700000001",
      preferredLanguage: "en",
      timeZone: "UTC",
      isEmailVerified: true,
      emailNotificationsEnabled: true,
      securityNotificationsEnabled: true,
      productUpdatesEnabled: false,
      organizationRole: "Owner",
      roles: ["Owner"],
      permissions: []
    },
    isAuthenticated: true,
    isSigningIn: false,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    clearError: vi.fn(),
    refreshCurrentUser: vi.fn(),
    switchOrganization,
    createOrganization,
    setCurrentUserProfile: vi.fn()
  })
}));

vi.mock("../src/hooks/useAuthorizedOrganizations", () => ({
  useAuthorizedOrganizations: () => ({
    organizations: [
      {
        companyId: "company-1",
        name: "StayFlow KE",
        slug: "stayflow-ke",
        role: "Owner",
        membershipStatus: "Active",
        isActiveOrganization: true,
        organizationStatus: "Active",
        onboardingState: "Completed",
        propertyCount: 1,
        planName: "Free",
        subscriptionStatus: "Active"
      },
      {
        companyId: "company-2",
        name: "Orbit Ops",
        slug: "orbit-ops",
        role: "Host",
        membershipStatus: "Active",
        isActiveOrganization: false,
        organizationStatus: "Active",
        onboardingState: "OrganizationProfile",
        propertyCount: 2,
        planName: "Growth",
        subscriptionStatus: "Active"
      }
    ],
    isLoading: false,
    error: null,
    refresh: refreshOrganizations
  })
}));

vi.mock("../src/components/host", async () => {
  const actual = await vi.importActual<typeof import("../src/components/host")>("../src/components/host");
  return {
    ...actual,
    HostConsoleNav: () => <div data-testid="host-console-nav" />,
    HostLoginPanel: () => <div data-testid="host-login-panel" />
  };
});

import { MyOrganizationsPage } from "../src/pages/MyOrganizationsPage";

describe("MyOrganizationsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders memberships and hides restricted actions for non-admin roles", () => {
    render(<MyOrganizationsPage />);

    expect(screen.getByText("StayFlow KE (Current)")).toBeInTheDocument();
    expect(screen.getByText("Orbit Ops")).toBeInTheDocument();
    expect(screen.getByText("Growth")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Switch" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Settings" })).toBeInTheDocument();
    expect(screen.queryAllByRole("link", { name: "Manage Team" })).toHaveLength(1);
  });

  it("submits create organization and refreshes memberships", async () => {
    render(<MyOrganizationsPage />);

    fireEvent.change(screen.getByLabelText("Organization Name"), { target: { value: "Neptune Test 2" } });
    fireEvent.change(screen.getByLabelText("Support Contact Email"), { target: { value: "support@neptune.test" } });
    fireEvent.change(screen.getByLabelText("Country Code"), { target: { value: "KE" } });
    fireEvent.change(screen.getByLabelText("Time Zone"), { target: { value: "Africa/Nairobi" } });

    fireEvent.click(screen.getByRole("button", { name: "Create Organization" }));

    await waitFor(() => {
      expect(createOrganization).toHaveBeenCalledTimes(1);
    });

    expect(createOrganization).toHaveBeenCalledWith({
      name: "Neptune Test 2",
      supportContactEmail: "support@neptune.test",
      countryCode: "KE",
      timeZone: "Africa/Nairobi"
    });
    expect(refreshOrganizations).toHaveBeenCalledTimes(1);
  });

  it("refreshes organization-scoped data after switching workspaces", async () => {
    render(<MyOrganizationsPage />);

    fireEvent.click(screen.getByRole("button", { name: "Switch" }));

    await waitFor(() => {
      expect(switchOrganization).toHaveBeenCalledWith("company-2");
      expect(refreshOrganizations).toHaveBeenCalledTimes(1);
    });
  });
});