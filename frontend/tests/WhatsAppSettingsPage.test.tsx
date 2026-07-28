import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";

function apiSuccess<T>(data: T) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: "ok",
      data,
      errors: [],
      correlationId: "cid"
    })
  };
}

function apiFailure(message = "Request failed", status = 500) {
  return {
    ok: false,
    status,
    json: async () => ({
      success: false,
      message,
      errors: [message],
      correlationId: "cid"
    })
  };
}

const integration = {
  id: "11111111-1111-4111-8111-111111111111",
  displayName: "Demo WhatsApp",
  businessPhoneNumberMasked: "+1******1234",
  isActive: true,
  isProductionEnabled: false,
  mode: "Development",
  healthStatus: "ConfigurationIncomplete",
  lastHealthCheckAt: "2026-07-24T08:00:00Z",
  lastSuccessfulHealthCheckAt: "2026-07-23T08:00:00Z",
  lastTemplateSyncAt: "2026-07-24T09:00:00Z",
  lastErrorSummary: "Configuration is incomplete."
};

const templates = [
  {
    id: "aaaaaaa1-aaaa-4aaa-8aaa-aaaaaaaaaaa1",
    name: "booking_reminder",
    languageCode: "en",
    category: "UTILITY",
    status: "APPROVED",
    isActive: true,
    isApproved: true,
    variableCount: 2,
    lastSyncedAt: "2026-07-24T09:00:00Z"
  },
  {
    id: "bbbbbbb2-bbbb-4bbb-8bbb-bbbbbbbbbbb2",
    name: "check_in_pending",
    languageCode: "fr",
    category: "MARKETING",
    status: "PENDING",
    isActive: true,
    isApproved: false,
    variableCount: 1,
    lastSyncedAt: "2026-07-24T09:00:00Z"
  },
  {
    id: "ccccccc3-cccc-4ccc-8ccc-ccccccccccc3",
    name: "policy_notice",
    languageCode: "en",
    category: "AUTHENTICATION",
    status: "REJECTED",
    isActive: false,
    isApproved: false,
    variableCount: 0,
    lastSyncedAt: "2026-07-24T09:00:00Z"
  }
];

const templateDetails = {
  [templates[0].id]: {
    ...templates[0],
    headerType: "TEXT",
    bodyText: "Hello {{1}}, your check-in starts at {{2}}.",
    footerText: "Reply STOP to opt out",
    variables: [
      { position: 1, placeholder: "{{1}}" },
      { position: 2, placeholder: "{{2}}" }
    ]
  },
  [templates[1].id]: {
    ...templates[1],
    headerType: null,
    bodyText: "Bonjour {{1}}",
    footerText: null,
    variables: [{ position: 1, placeholder: "{{1}}" }]
  },
  [templates[2].id]: {
    ...templates[2],
    headerType: null,
    bodyText: "Policy update",
    footerText: null,
    variables: []
  }
};

function listTemplatesFromQuery(url: URL) {
  let filtered = [...templates];

  const search = url.searchParams.get("search")?.trim();
  const status = url.searchParams.get("status")?.trim();
  const language = url.searchParams.get("language")?.trim();
  const category = url.searchParams.get("category")?.trim();
  const approvedOnly = url.searchParams.get("approvedOnly") === "true";
  const page = Number(url.searchParams.get("page") ?? "1");
  const pageSize = Number(url.searchParams.get("pageSize") ?? "20");

  if (search) {
    filtered = filtered.filter((item) => item.name.toLowerCase().includes(search.toLowerCase()));
  }

  if (status) {
    filtered = filtered.filter((item) => item.status === status);
  }

  if (language) {
    filtered = filtered.filter((item) => item.languageCode === language);
  }

  if (category) {
    filtered = filtered.filter((item) => item.category === category);
  }

  if (approvedOnly) {
    filtered = filtered.filter((item) => item.status === "APPROVED");
  }

  const totalCount = filtered.length;
  const start = (page - 1) * pageSize;
  const items = filtered.slice(start, start + pageSize);

  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages: Math.max(1, Math.ceil(totalCount / pageSize))
  };
}

function createFetchMock(options?: {
  healthFails?: boolean;
  syncFails?: boolean;
  templatesEmpty?: boolean;
  integrationHealthStatus?: string;
  integrationErrorSummary?: string;
}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(typeof input === "string" ? input : input.toString(), "http://localhost:5243");

    if (url.pathname === "/whatsapp/integrations" && (init?.method ?? "GET") === "GET") {
      return apiSuccess([
        {
          ...integration,
          healthStatus: options?.integrationHealthStatus ?? integration.healthStatus,
          lastErrorSummary: options?.integrationErrorSummary ?? integration.lastErrorSummary
        }
      ]);
    }

    if (url.pathname === `/whatsapp/integrations/${integration.id}/health` && (init?.method ?? "GET") === "GET") {
      if (options?.healthFails) {
        return apiFailure("Health check unavailable", 503);
      }

      return apiSuccess({
        integrationId: integration.id,
        status: "Healthy",
        message: "Validated successfully.",
        isSendCapable: true,
        checkedAt: "2026-07-25T10:00:00Z"
      });
    }

    if (url.pathname === `/whatsapp/integrations/${integration.id}/templates/sync` && (init?.method ?? "GET") === "POST") {
      if (options?.syncFails) {
        return apiFailure("Sync unavailable", 503);
      }

      return apiSuccess({
        added: 1,
        updated: 2,
        unchanged: 3,
        disabled: 1,
        failed: 0,
        syncedAt: "2026-07-25T10:01:00Z",
        status: "Completed",
        message: "Template synchronization completed."
      });
    }

    if (url.pathname === `/whatsapp/integrations/${integration.id}/templates` && (init?.method ?? "GET") === "GET") {
      if (options?.templatesEmpty) {
        return apiSuccess({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 1 });
      }

      return apiSuccess(listTemplatesFromQuery(url));
    }

    if (url.pathname.startsWith(`/whatsapp/integrations/${integration.id}/templates/`) && (init?.method ?? "GET") === "GET") {
      const id = url.pathname.split("/").pop() ?? "";
      return apiSuccess(templateDetails[id as keyof typeof templateDetails] ?? templateDetails[templates[0].id]);
    }

    return apiFailure("Unexpected request", 404);
  });

  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("WhatsAppSettingsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    window.history.pushState({}, "", "/host/settings/whatsapp");
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it("renders host login when unauthenticated", () => {
    createFetchMock();

    render(<App />);

    expect(screen.getByRole("heading", { name: "Host Sign In" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "WhatsApp Settings" })).not.toBeInTheDocument();
  });

  it("renders WhatsApp settings route and not guest demo", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    expect(await screen.findByRole("heading", { name: "WhatsApp Settings" })).toBeInTheDocument();
    expect(screen.queryByText(/guest concierge/i)).not.toBeInTheDocument();
  });

  it("shows integration metadata and hides credentials/provider ids", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    expect(await screen.findByText("Demo WhatsApp")).toBeInTheDocument();
    expect(screen.getByText("+1******1234")).toBeInTheDocument();
    expect(screen.getByText("ConfigurationIncomplete")).toBeInTheDocument();
    expect(screen.getByText("No")).toBeInTheDocument();

    expect(screen.queryByText(/access token/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/app secret/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/verify token/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/credential-ref/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/demo-phone-number-id/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/demo-waba-id/i)).not.toBeInTheDocument();
  });

  it("runs health check and updates success feedback", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    await screen.findByRole("heading", { name: "WhatsApp Settings" });
    await userEvent.click(screen.getByRole("button", { name: "Check Health" }));

    expect(await screen.findByText("Health check completed.")).toBeInTheDocument();
    expect(screen.getByText("Healthy")).toBeInTheDocument();
  });

  it("shows safe error when health check fails", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock({ healthFails: true });

    render(<App />);

    await screen.findByRole("heading", { name: "WhatsApp Settings" });
    await userEvent.click(screen.getByRole("button", { name: "Check Health" }));

    expect(await screen.findByText("Health check unavailable")).toBeInTheDocument();
  });

  it("renders template list and preview", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    expect(await screen.findByText("booking_reminder")).toBeInTheDocument();
    expect(screen.getAllByText("PENDING").length).toBeGreaterThan(0);
    expect(screen.getAllByText("REJECTED").length).toBeGreaterThan(0);

    const bookingTemplate = await screen.findByText("booking_reminder");
    await userEvent.click(bookingTemplate.closest("button")!);

    expect(await screen.findByRole("heading", { name: "Preview" })).toBeInTheDocument();
    expect(screen.getByText("Hello {{1}}, your check-in starts at {{2}}.")).toBeInTheDocument();
  });

  it("applies search and status/language/category/approved filters", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    expect(await screen.findByText("booking_reminder")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Search"), "policy");
    await waitFor(() => {
      expect(screen.getByText("policy_notice")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "" } });
    await waitFor(() => {
      expect(screen.getByText("booking_reminder")).toBeInTheDocument();
      expect(screen.getByText("check_in_pending")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText("Language"), { target: { value: "fr" } });
    await waitFor(() => {
      expect(screen.getByText("check_in_pending")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText("Language"), { target: { value: "" } });

    fireEvent.change(screen.getByLabelText("Status"), { target: { value: "APPROVED" } });
    await waitFor(() => {
      expect(screen.getByText("booking_reminder")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText("Status"), { target: { value: "" } });
    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "AUTHENTICATION" } });
    await waitFor(() => {
      expect(screen.getByText("policy_notice")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "" } });
    await userEvent.click(screen.getByLabelText("Approved only"));

    await waitFor(() => {
      expect(screen.getByText("booking_reminder")).toBeInTheDocument();
    });
  });

  it("shows empty state when template list is empty", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock({ templatesEmpty: true });

    render(<App />);

    expect(await screen.findByText("No templates found")).toBeInTheDocument();
  });

  it("runs sync and shows result counts", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock();

    render(<App />);

    await screen.findByRole("heading", { name: "WhatsApp Settings" });
    await userEvent.click(screen.getByRole("button", { name: "Sync Templates" }));

    expect(await screen.findByText("Template synchronization completed.")).toBeInTheDocument();
    expect(screen.getByText("Added")).toBeInTheDocument();
    expect(screen.getByText("Updated")).toBeInTheDocument();
    expect(screen.getByText("Unchanged")).toBeInTheDocument();
    expect(screen.getByText("Disabled")).toBeInTheDocument();
    expect(screen.getByText("Failed")).toBeInTheDocument();
  });

  it("shows sync failure feedback safely", async () => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock({ syncFails: true });

    render(<App />);

    await screen.findByRole("heading", { name: "WhatsApp Settings" });
    await userEvent.click(screen.getByRole("button", { name: "Sync Templates" }));

    expect(await screen.findByText("Sync unavailable")).toBeInTheDocument();
  });

  it.each([
    ["AuthenticationFailed", "Token rejected"],
    ["AuthorizationFailed", "Permission missing"],
    ["RateLimited", "Too many requests"],
    ["ProviderUnavailable", "Provider unavailable"],
    ["DevelopmentOnly", "Production sending not enabled"]
  ])("renders production health state %s", async (status, summary) => {
    sessionStorage.setItem("stayflow.host.accessToken", "token");
    createFetchMock({ integrationHealthStatus: status, integrationErrorSummary: summary });

    render(<App />);

    await screen.findByRole("heading", { name: "WhatsApp Settings" });
    expect(screen.getByText(status)).toBeInTheDocument();
    expect(screen.getByText(summary)).toBeInTheDocument();

    expect(screen.queryByText(/access token/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/app secret/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/credential reference/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/phone number id/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/waba/i)).not.toBeInTheDocument();
  });

});
