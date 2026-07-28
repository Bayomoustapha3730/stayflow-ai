import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/httpClient";

const useHostAuthMock = vi.fn();
const usePropertyKnowledgeMock = vi.fn();

vi.mock("../src/hooks/useHostAuth", () => ({
  useHostAuth: () => useHostAuthMock()
}));

vi.mock("../src/hooks/usePropertyKnowledge", () => ({
  usePropertyKnowledge: (options: unknown) => usePropertyKnowledgeMock(options)
}));

import { PropertyKnowledgePage } from "../src/pages/PropertyKnowledgePage";

function authState() {
  return {
    isAuthenticated: true,
    isSigningIn: false,
    accessToken: "host-token",
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    clearError: vi.fn()
  };
}

function summaryItem(id = "k-1") {
  return {
    id,
    propertyId: "p-1",
    propertyName: "Westlands Apartment",
    category: 0,
    categoryLabel: "Wi-Fi",
    title: "Wi-Fi",
    summary: "Wi-Fi details",
    tags: ["wifi"],
    priority: 10,
    isApproved: true,
    isActive: true,
    approvedAt: "2026-07-22T00:00:00Z",
    approvedBy: "Host",
    createdAt: "2026-07-22T00:00:00Z",
    updatedAt: "2026-07-22T00:00:00Z",
    canBeUsedByAI: true
  };
}

function baseKnowledgeState(overrides: Record<string, unknown> = {}) {
  return {
    propertyName: "Westlands Apartment",
    response: null,
    selectedKnowledge: null,
    selectedKnowledgeId: null,
    isLoading: false,
    isRefreshing: false,
    isLoadingKnowledge: false,
    error: null,
    selectedKnowledgeError: null,
    search: "",
    category: undefined,
    approvalFilter: "all",
    activeFilter: "all",
    page: 1,
    pageSize: 10,
    isCreating: false,
    isUpdating: false,
    isApproving: false,
    isActivating: false,
    isDeleting: false,
    setSearch: vi.fn(),
    setCategory: vi.fn(),
    setApprovalFilter: vi.fn(),
    setActiveFilter: vi.fn(),
    setPage: vi.fn(),
    setPageSize: vi.fn(),
    refresh: vi.fn(),
    retry: vi.fn(),
    clearError: vi.fn(),
    clearSelectedKnowledge: vi.fn(),
    selectKnowledge: vi.fn().mockResolvedValue(undefined),
    createKnowledge: vi.fn(),
    updateKnowledge: vi.fn(),
    approveKnowledge: vi.fn(),
    unapproveKnowledge: vi.fn(),
    activateKnowledge: vi.fn(),
    deactivateKnowledge: vi.fn(),
    deleteKnowledge: vi.fn(),
    ...overrides
  };
}

describe("PropertyKnowledgePage", () => {
  beforeEach(() => {
    useHostAuthMock.mockReturnValue(authState());
    usePropertyKnowledgeMock.mockReturnValue(baseKnowledgeState());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows property-specific list errors and keeps the conversation 404 text out of the page", () => {
    usePropertyKnowledgeMock.mockReturnValue(baseKnowledgeState({
      error: "Property not found."
    }));

    render(<PropertyKnowledgePage propertyId="p-1" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Property not found.");
    expect(
      screen.queryByText((content) => content.includes("This conversation") && content.includes("no longer available"))
    ).not.toBeInTheDocument();
  });

  it("shows guidance when no property ID is available", () => {
    render(<PropertyKnowledgePage propertyId={null} />);

    expect(screen.getByText(/select a conversation with a property first\./i)).toBeInTheDocument();
  });

  it("resolves tenant property and enables creation when route property is missing", async () => {
    usePropertyKnowledgeMock.mockImplementation((options?: { propertyId?: string | null }) => {
      const resolvedId = options?.propertyId ?? null;
      return baseKnowledgeState({
        propertyName: resolvedId ? "Demo Nairobi Apartment" : null,
        response: resolvedId ? {
          items: [summaryItem("k-1")],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 1,
          totalPages: 1
        } : null
      });
    });

    vi.stubGlobal("fetch", vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        message: "ok",
        data: {
          items: [
            {
              id: "22222222-2222-2222-2222-222222222222",
              name: "Demo Nairobi Apartment"
            }
          ],
          pageNumber: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1
        },
        errors: [],
        correlationId: "cid"
      })
    })));

    render(<PropertyKnowledgePage propertyId={null} />);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /create knowledge/i })).toBeEnabled();
    });

    expect(screen.getByText("Demo Nairobi Apartment")).toBeInTheDocument();
  });

  it("shows item-specific errors and keeps the conversation 404 text out of the page", () => {
    usePropertyKnowledgeMock.mockReturnValue(baseKnowledgeState({
      response: {
        items: [summaryItem()],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10,
        totalPages: 1
      },
      selectedKnowledgeId: "k-1",
      selectedKnowledgeError: "This knowledge item is no longer available."
    }));

    render(<PropertyKnowledgePage propertyId="p-1" />);

    expect(screen.getByRole("alert")).toHaveTextContent("This knowledge item is no longer available.");
    expect(
      screen.queryByText((content) => content.includes("This conversation") && content.includes("no longer available"))
    ).not.toBeInTheDocument();
  });

  it("shows backend validation messages when saving knowledge", async () => {
    const user = userEvent.setup();
    const createKnowledge = vi.fn().mockRejectedValue(new ApiError("Title is required.", 400, ["Title is required."]));

    usePropertyKnowledgeMock.mockReturnValue(baseKnowledgeState({
      createKnowledge
    }));

    render(<PropertyKnowledgePage propertyId="p-1" />);

    await user.click(screen.getByRole("button", { name: /create knowledge/i }));
    await user.type(screen.getByLabelText(/title/i), "Wi-Fi");
    await user.type(screen.getByRole("textbox", { name: /content/i }), "Wi-Fi details");
    await user.click(screen.getByRole("button", { name: /^create$/i }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Title is required."));
    expect(createKnowledge).toHaveBeenCalledTimes(1);
  });

  it("shows concise action errors for approval changes", async () => {
    const user = userEvent.setup();
    const approveKnowledge = vi.fn().mockRejectedValue(new Error("Request failed."));

    usePropertyKnowledgeMock.mockReturnValue(baseKnowledgeState({
      response: {
        items: [
          {
            ...summaryItem("k-1"),
            isApproved: false
          }
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10,
        totalPages: 1
      },
      approveKnowledge,
      selectedKnowledgeId: null
    }));

    render(<PropertyKnowledgePage propertyId="p-1" />);

    await user.click(screen.getByRole("button", { name: /approve/i }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Unable to approve the knowledge item."));
    expect(approveKnowledge).toHaveBeenCalledTimes(1);
  });
});
