import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const updateMemberRole = vi.fn();

const authState = {
  accessToken: "token",
  currentUser: {
    id: "owner-1",
    companyId: "company-1",
    fullName: "Owner User",
    email: "owner@test",
    phoneNumber: "+254700000001",
    preferredLanguage: "en",
    timeZone: "UTC",
    isEmailVerified: true,
    emailNotificationsEnabled: true,
    securityNotificationsEnabled: true,
    productUpdatesEnabled: true,
    organizationRole: "Owner",
    roles: ["Owner"],
    permissions: []
  },
  isAuthenticated: true,
  isSigningIn: false,
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  clearError: vi.fn()
};

const settingsState = {
  organization: {
    id: "org-1",
    name: "StayFlow",
    slug: "stayflow",
    status: "Active",
    ownerUserId: "owner-1",
    createdAt: "2026-08-01T00:00:00Z",
    updatedAt: "2026-08-01T00:00:00Z"
  },
  members: [
    {
      userId: "owner-1",
      fullName: "Owner User",
      email: "owner@test",
      role: "Owner",
      status: "Active",
      joinedAt: "2026-08-01T00:00:00Z"
    },
    {
      userId: "member-2",
      fullName: "Team Member",
      email: "member@test",
      role: "Host",
      status: "Active",
      joinedAt: "2026-08-01T00:00:00Z"
    }
  ],
  isLoading: false,
  isSaving: false,
  error: null,
  message: null,
  refresh: vi.fn(),
  updateOrganization: vi.fn(),
  updateMemberRole,
  removeMember: vi.fn()
};

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => authState
}));

vi.mock("../src/hooks/useOrganizationSettings", () => ({
  useOrganizationSettings: () => settingsState
}));

vi.mock("../src/components/host", () => ({
  HostLoginPanel: () => <div data-testid="host-login-panel" />
}));

vi.mock("../src/components/host/HostConsoleNav", () => ({
  HostConsoleNav: () => <div data-testid="host-console-nav" />
}));

import { OrganizationSettingsPage } from "../src/pages/OrganizationSettingsPage";

describe("OrganizationSettingsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("disables self owner role changes and keeps other member role updates available", () => {
    render(<OrganizationSettingsPage />);

    expect(screen.getByText("Your owner role cannot be changed from this account.")).toBeInTheDocument();

    const roleSelectors = screen.getAllByRole("combobox");
    expect(roleSelectors).toHaveLength(3);

    const selfRoleSelector = roleSelectors[1];
    const otherMemberSelector = roleSelectors[2];

    expect(selfRoleSelector).toBeDisabled();
    expect(otherMemberSelector).not.toBeDisabled();

    fireEvent.change(otherMemberSelector, { target: { value: "Manager" } });

    expect(updateMemberRole).toHaveBeenCalledTimes(1);
    expect(updateMemberRole).toHaveBeenCalledWith("member-2", "Manager");
  });
});
