import { useMemo, useState } from "react";
import { HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useOnboardingWizard } from "../hooks/useOnboardingWizard";
import "../styles/host-inbox.css";
import "../styles/onboarding.css";

type StepKey =
  | "welcome"
  | "organization"
  | "plan"
  | "property"
  | "team"
  | "whatsapp"
  | "ai"
  | "knowledge"
  | "demo"
  | "review"
  | "completed";

interface StepDefinition {
  key: StepKey;
  title: string;
  route: string;
  canSkip: boolean;
  requiredRole?: "Owner" | "Administrator";
}

const steps: StepDefinition[] = [
  { key: "welcome", title: "Welcome", route: "/onboarding/welcome", canSkip: false },
  { key: "organization", title: "Organization Profile", route: "/onboarding/organization", canSkip: false, requiredRole: "Administrator" },
  { key: "plan", title: "Plan Confirmation", route: "/onboarding/plan", canSkip: false, requiredRole: "Administrator" },
  { key: "property", title: "First Property", route: "/onboarding/property", canSkip: false, requiredRole: "Administrator" },
  { key: "team", title: "Team Invitations", route: "/onboarding/team", canSkip: true, requiredRole: "Administrator" },
  { key: "whatsapp", title: "WhatsApp Setup", route: "/onboarding/whatsapp", canSkip: true, requiredRole: "Administrator" },
  { key: "ai", title: "AI Provider", route: "/onboarding/ai", canSkip: false, requiredRole: "Administrator" },
  { key: "knowledge", title: "Knowledge Setup", route: "/onboarding/knowledge", canSkip: false, requiredRole: "Administrator" },
  { key: "demo", title: "Demo Data", route: "/onboarding/demo", canSkip: true, requiredRole: "Administrator" },
  { key: "review", title: "Review", route: "/onboarding/review", canSkip: false, requiredRole: "Administrator" },
  { key: "completed", title: "Completed", route: "/get-started", canSkip: false }
];

function parseStep(value: string | null | undefined): StepKey {
  const normalized = (value || "").toLowerCase();
  if (normalized.includes("organization")) {
    return "organization";
  }

  if (normalized.includes("plan")) {
    return "plan";
  }

  if (normalized.includes("property")) {
    return "property";
  }

  if (normalized.includes("team")) {
    return "team";
  }

  if (normalized.includes("whatsapp")) {
    return "whatsapp";
  }

  if (normalized.includes("ai")) {
    return "ai";
  }

  if (normalized.includes("knowledge")) {
    return "knowledge";
  }

  if (normalized.includes("demo")) {
    return "demo";
  }

  if (normalized.includes("review")) {
    return "review";
  }

  if (normalized.includes("complete")) {
    return "completed";
  }

  return "welcome";
}

function isAdminLike(role: string | null | undefined): boolean {
  return role === "Owner" || role === "Administrator";
}

interface OnboardingPageProps {
  routeStep?: StepKey;
}

export function OnboardingPage({ routeStep }: OnboardingPageProps) {
  const auth = useHostAuth();
  const onboarding = useOnboardingWizard({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  const [organizationName, setOrganizationName] = useState("");
  const [organizationSlug, setOrganizationSlug] = useState("");
  const [organizationContact, setOrganizationContact] = useState("");
  const [organizationTimeZone, setOrganizationTimeZone] = useState("Africa/Nairobi");
  const [organizationLocale, setOrganizationLocale] = useState("en");
  const [propertyName, setPropertyName] = useState("");
  const [propertyAddress, setPropertyAddress] = useState("");
  const [propertyCity, setPropertyCity] = useState("");
  const [propertyCountry, setPropertyCountry] = useState("KE");
  const [propertyTimeZone, setPropertyTimeZone] = useState("Africa/Nairobi");
  const [propertyDescription, setPropertyDescription] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("Host");
  const [knowledgeTitle, setKnowledgeTitle] = useState("House Rules");
  const [knowledgeContent, setKnowledgeContent] = useState("Quiet hours after 10 PM. Please avoid loud music.");

  const currentStep = routeStep ?? parseStep(onboarding.status?.currentStep);
  const step = steps.find((item) => item.key === currentStep) ?? steps[0];
  const isAdmin = isAdminLike(auth.currentUser?.organizationRole ?? null);
  const hasTenant = Boolean(auth.currentUser?.companyId);

  const blockers = useMemo(() => (onboarding.status?.blockers ?? []).filter((item) => parseStep(item.step) === currentStep), [currentStep, onboarding.status?.blockers]);

  function goToStep(target: StepDefinition) {
    window.history.pushState({}, "", target.route);
    window.dispatchEvent(new PopStateEvent("popstate"));
  }

  function handleStepNavigation(event: React.KeyboardEvent<HTMLDivElement>) {
    const index = steps.findIndex((item) => item.key === currentStep);
    if (index < 0) {
      return;
    }

    if (event.key === "ArrowRight" && index < steps.length - 1) {
      goToStep(steps[index + 1]);
    }

    if (event.key === "ArrowLeft" && index > 0) {
      goToStep(steps[index - 1]);
    }
  }

  if (!auth.isAuthenticated) {
    return (
      <div className="sf-host-login-shell">
        <HostLoginPanel
          isSigningIn={auth.isSigningIn}
          error={auth.error}
          onLogin={auth.login}
          onClearError={auth.clearError}
        />
      </div>
    );
  }

  if (!hasTenant) {
    return (
      <div className="sf-onboarding-shell">
        <section className="sf-onboarding-card">
          <h1>Organization Setup Required</h1>
          <p>Your user account is not linked to an organization yet. Follow the organization setup flow first.</p>
          <a href="/host/settings/organization">Go to Organization Settings</a>
        </section>
      </div>
    );
  }

  const requiresAdmin = step.requiredRole === "Administrator";
  const readOnly = requiresAdmin && !isAdmin;

  return (
    <div className="sf-onboarding-shell">
      <div className="sf-onboarding-header">
        <div>
          <p className="sf-host-kicker">StayFlow Setup</p>
          <h1>Customer Onboarding</h1>
          <p className="sf-host-muted-note">Resumable setup from organization profile to first guest-ready workflows.</p>
        </div>
        <div className="sf-onboarding-top-actions">
          <button type="button" onClick={() => void onboarding.refresh()} disabled={onboarding.isLoading || onboarding.isSaving}>Refresh</button>
          <a href="/host/conversations">Exit to Host Inbox</a>
        </div>
      </div>

      <div className="sf-onboarding-progress" role="progressbar" aria-valuenow={onboarding.status?.percentComplete ?? 0} aria-valuemin={0} aria-valuemax={100}>
        <span style={{ width: `${onboarding.status?.percentComplete ?? 0}%` }} />
      </div>
      <p className="sf-host-muted-note">Progress: {onboarding.status?.percentComplete ?? 0}%</p>

      <div className="sf-onboarding-step-tabs" role="tablist" aria-label="Onboarding steps" onKeyDown={handleStepNavigation} tabIndex={0}>
        {steps.map((item) => {
          const completed = onboarding.status?.completedSteps.some((entry) => parseStep(entry) === item.key);
          const skipped = onboarding.status?.skippedSteps.some((entry) => parseStep(entry) === item.key);
          return (
            <button
              role="tab"
              aria-selected={item.key === currentStep}
              key={item.key}
              className={item.key === currentStep ? "active" : ""}
              onClick={() => goToStep(item)}
              type="button"
            >
              {item.title}{completed ? " ✓" : skipped ? " (Skipped)" : ""}
            </button>
          );
        })}
      </div>

      {onboarding.error ? <div className="sf-host-inline-error" role="alert"><p>{onboarding.error}</p></div> : null}
      {onboarding.message ? <div className="sf-whatsapp-status" role="status"><p>{onboarding.message}</p></div> : null}
      {blockers.length > 0 ? (
        <div className="sf-onboarding-blockers" role="alert">
          {blockers.map((item) => <p key={`${item.step}-${item.code}`}>{item.message}</p>)}
        </div>
      ) : null}

      {readOnly ? (
        <section className="sf-onboarding-card">
          <h2>{step.title}</h2>
          <p>You have read-only access for this step. Ask an owner/administrator to complete it.</p>
        </section>
      ) : null}

      {!readOnly && currentStep === "welcome" ? (
        <section className="sf-onboarding-card">
          <h2>Welcome</h2>
          <p>Start onboarding now and resume later at any time.</p>
          <button type="button" onClick={() => void onboarding.start()} disabled={onboarding.isSaving}>Start Onboarding</button>
        </section>
      ) : null}

      {!readOnly && currentStep === "organization" ? (
        <section className="sf-onboarding-card">
          <h2>Organization Profile</h2>
          <label>Name<input value={organizationName} onChange={(event) => setOrganizationName(event.target.value)} /></label>
          <label>Slug<input value={organizationSlug} onChange={(event) => setOrganizationSlug(event.target.value)} /></label>
          <label>Support Contact<input value={organizationContact} onChange={(event) => setOrganizationContact(event.target.value)} /></label>
          <label>Time Zone<input value={organizationTimeZone} onChange={(event) => setOrganizationTimeZone(event.target.value)} /></label>
          <label>Locale<input value={organizationLocale} onChange={(event) => setOrganizationLocale(event.target.value)} /></label>
          <button
            type="button"
            disabled={onboarding.isSaving}
            onClick={() => void onboarding.saveOrganization({
              name: organizationName,
              slug: organizationSlug,
              supportContactEmail: organizationContact,
              timeZone: organizationTimeZone,
              locale: organizationLocale
            })}
          >
            Save and Continue
          </button>
        </section>
      ) : null}

      {!readOnly && currentStep === "plan" ? (
        <section className="sf-onboarding-card">
          <h2>Plan Confirmation</h2>
          <p>Current plan from trusted billing state: <strong>{onboarding.status?.selectedPlanName ?? "Unknown"}</strong></p>
          <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.confirmPlan({ planName: onboarding.status?.selectedPlanName ?? undefined })}>Confirm Plan</button>
          <a href="/host/settings/billing">Open Billing</a>
        </section>
      ) : null}

      {!readOnly && currentStep === "property" ? (
        <section className="sf-onboarding-card">
          <h2>First Property</h2>
          <label>Name<input value={propertyName} onChange={(event) => setPropertyName(event.target.value)} /></label>
          <label>Address<input value={propertyAddress} onChange={(event) => setPropertyAddress(event.target.value)} /></label>
          <label>City<input value={propertyCity} onChange={(event) => setPropertyCity(event.target.value)} /></label>
          <label>Country<input value={propertyCountry} onChange={(event) => setPropertyCountry(event.target.value)} /></label>
          <label>Time Zone<input value={propertyTimeZone} onChange={(event) => setPropertyTimeZone(event.target.value)} /></label>
          <label>Description<textarea value={propertyDescription} onChange={(event) => setPropertyDescription(event.target.value)} /></label>
          <button
            type="button"
            disabled={onboarding.isSaving}
            onClick={() => void onboarding.createProperty({
              name: propertyName,
              addressLine1: propertyAddress,
              city: propertyCity,
              countryCode: propertyCountry,
              timeZone: propertyTimeZone,
              description: propertyDescription,
              idempotencyKey: `${propertyName}:${propertyAddress}:${propertyCity}`
            })}
          >
            Save and Continue
          </button>
        </section>
      ) : null}

      {!readOnly && currentStep === "team" ? (
        <section className="sf-onboarding-card">
          <h2>Team Invitations</h2>
          <label>Email<input value={inviteEmail} onChange={(event) => setInviteEmail(event.target.value)} /></label>
          <label>Role
            <select value={inviteRole} onChange={(event) => setInviteRole(event.target.value)}>
              <option value="Host">Host</option>
              <option value="Manager">Manager</option>
              <option value="Support">Support</option>
              <option value="ReadOnly">ReadOnly</option>
            </select>
          </label>
          <div className="sf-onboarding-actions-row">
            <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.submitInvitations({ invitations: [{ email: inviteEmail, role: inviteRole }] })}>Send Invitation</button>
            <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.skipStep("TeamInvitations", "Invites deferred")}>Skip</button>
          </div>
        </section>
      ) : null}

      {!readOnly && currentStep === "whatsapp" ? (
        <section className="sf-onboarding-card">
          <h2>WhatsApp Setup</h2>
          <p>Run integration health checks and confirm readiness.</p>
          <div className="sf-onboarding-actions-row">
            <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.configureWhatsApp({ runHealthCheck: true })}>Validate and Continue</button>
            <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.skipStep("WhatsAppSetup", "Not needed currently")}>Skip</button>
          </div>
        </section>
      ) : null}

      {!readOnly && currentStep === "ai" ? (
        <section className="sf-onboarding-card">
          <h2>AI Provider Readiness</h2>
          <p>Confirm provider readiness or deterministic fallback mode.</p>
          <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.configureAi({ acknowledgeDeterministicFallback: true, skipIfDeterministicOnly: false })}>Confirm AI Readiness</button>
        </section>
      ) : null}

      {!readOnly && currentStep === "knowledge" ? (
        <section className="sf-onboarding-card">
          <h2>Knowledge Base Setup</h2>
          <label>Title<input value={knowledgeTitle} onChange={(event) => setKnowledgeTitle(event.target.value)} /></label>
          <label>Content<textarea value={knowledgeContent} onChange={(event) => setKnowledgeContent(event.target.value)} /></label>
          <button
            type="button"
            disabled={onboarding.isSaving}
            onClick={() => void onboarding.submitKnowledge({
              propertyId: onboarding.status?.firstPropertyId ?? undefined,
              title: knowledgeTitle,
              content: knowledgeContent,
              tags: ["onboarding"],
              idempotencyKey: `${knowledgeTitle}:${knowledgeContent}`
            })}
          >
            Save and Continue
          </button>
        </section>
      ) : null}

      {!readOnly && currentStep === "demo" ? (
        <section className="sf-onboarding-card">
          <h2>Demo Data</h2>
          <p>Create sample records for first-run exploration in non-production environments.</p>
          <div className="sf-onboarding-actions-row">
            <button
              type="button"
              disabled={onboarding.isSaving}
              onClick={() => void onboarding.generateDemoData({
                createSampleKnowledge: true,
                createSampleReservation: true,
                createSampleConversation: true,
                createSampleHostCopilotItem: true,
                idempotencyKey: "default"
              })}
            >
              Generate Demo Data
            </button>
            <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.skipStep("DemoData", "Skipped demo data")}>Skip</button>
          </div>
        </section>
      ) : null}

      {!readOnly && currentStep === "review" ? (
        <section className="sf-onboarding-card">
          <h2>Completion Checklist</h2>
          <ul className="sf-onboarding-checklist">
            {(onboarding.status?.checklist ?? []).map((item) => (
              <li key={item.key}>
                <strong>{item.key}</strong>: {item.status} - {item.recommendation}
              </li>
            ))}
          </ul>
          <button type="button" disabled={onboarding.isSaving} onClick={() => void onboarding.complete()}>Complete Onboarding</button>
        </section>
      ) : null}

      {currentStep === "completed" || onboarding.status?.isCompleted ? (
        <section className="sf-onboarding-card">
          <h2>You're Ready</h2>
          <p>Organization setup is complete. Continue with your first run.</p>
          <ul className="sf-onboarding-links">
            <li><a href="/host/conversations">Go to Host Inbox</a></li>
            <li><a href="/host/properties">Go to Property Knowledge</a></li>
            <li><a href="/host/settings/whatsapp">Open WhatsApp Settings</a></li>
            <li><a href="/host/settings/billing">Open Billing</a></li>
            <li><a href="/host/settings/organization">Open Team Settings</a></li>
          </ul>
        </section>
      ) : null}

      <div className="sf-onboarding-footer-actions">
        <button
          type="button"
          onClick={() => {
            const currentIndex = steps.findIndex((item) => item.key === currentStep);
            if (currentIndex > 0) {
              goToStep(steps[currentIndex - 1]);
            }
          }}
          disabled={onboarding.isSaving}
        >
          Back
        </button>
        <button
          type="button"
          onClick={() => {
            const currentIndex = steps.findIndex((item) => item.key === currentStep);
            if (currentIndex < steps.length - 1) {
              goToStep(steps[currentIndex + 1]);
            }
          }}
          disabled={onboarding.isSaving}
        >
          Resume Later
        </button>
      </div>
    </div>
  );
}
