import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => ({
    accessToken: "token",
    currentUser: {
      id: "user-1",
      companyId: "company-1",
      fullName: "Platform Admin",
      email: "admin@example.test",
      phoneNumber: "+10000000000",
      isEmailVerified: true,
      roles: ["PlatformAdmin"],
      permissions: ["platform.admin"]
    },
    isAuthenticated: true,
    isSigningIn: false,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    clearError: vi.fn()
  })
}));

describe("PlatformAdminPage", () => {
  it("renders page shell and section tabs", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers(),
      json: async () => ({
        success: true,
        data: {
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 50
        },
        message: "ok",
        errors: []
      })
    } as Response);

    const { PlatformAdminPage } = await import("../src/pages/PlatformAdminPage");

    render(<PlatformAdminPage />);

    expect(screen.getByRole("heading", { name: "Platform Admin Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "tenants" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "support" })).toBeInTheDocument();

    fetchMock.mockRestore();
  });
});
