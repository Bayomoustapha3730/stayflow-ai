import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HostOrganizationSelector } from "../src/components/host/HostOrganizationSelector";

const refreshOrganizations = vi.fn().mockResolvedValue(undefined);
const switchOrganization = vi.fn().mockResolvedValue(true);

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
        role: "Administrator",
        membershipStatus: "Active",
        isActiveOrganization: false,
        organizationStatus: "Active",
        onboardingState: "Completed",
        propertyCount: 3,
        planName: "Growth",
        subscriptionStatus: "Active"
      }
    ],
    isLoading: false,
    error: null,
    refresh: refreshOrganizations
  })
}));

describe("HostOrganizationSelector", () => {
  it("renders the current organization and lists authorized organizations", async () => {
    render(
      <HostOrganizationSelector
        auth={{
          accessToken: "token",
          currentUser: {
            id: "user-1",
            companyId: "company-1",
            fullName: "Owner",
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
          createOrganization: vi.fn(),
          setCurrentUserProfile: vi.fn()
        }}
        organizationsHref="/host/organizations"
      />
    );

    expect(screen.getByRole("button", { name: /StayFlow KE/i })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /StayFlow KE/i }));

    expect(screen.getByText("Orbit Ops")).toBeInTheDocument();
    expect(screen.getByText("Admin")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Manage organizations/i })).toHaveAttribute("href", "/host/organizations");

    fireEvent.click(screen.getByRole("menuitem", { name: /Orbit Ops/i }));

    await waitFor(() => {
      expect(switchOrganization).toHaveBeenCalledTimes(1);
    });

    expect(switchOrganization).toHaveBeenCalledWith("company-2");
    expect(refreshOrganizations).toHaveBeenCalledTimes(1);
  });
});