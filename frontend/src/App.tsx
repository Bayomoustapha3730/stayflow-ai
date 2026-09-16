import { useEffect, useState } from "react";
import { DemoPage } from "./pages/DemoPage";
import { AccountSettingsPage } from "./pages/AccountSettingsPage";
import { BillingDashboardPage } from "./pages/BillingDashboardPage";
import { CurrentSubscriptionPage } from "./pages/CurrentSubscriptionPage";
import { DataDeletionPage } from "./pages/DataDeletionPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { HostCopilotWorkspacePage } from "./pages/HostCopilotWorkspacePage";
import { HostInboxPage } from "./pages/HostInboxPage";
import { InvitationDecisionPage } from "./pages/InvitationDecisionPage";
import { MyOrganizationsPage } from "./pages/MyOrganizationsPage";
import { OnboardingPage } from "./pages/OnboardingPage";
import { OrganizationSettingsPage } from "./pages/OrganizationSettingsPage";
import { PlanComparisonPage } from "./pages/PlanComparisonPage";
import { PlatformAdminPage } from "./pages/PlatformAdminPage";
import { PrivacyPolicyPage } from "./pages/PrivacyPolicyPage";
import { PropertyKnowledgePage } from "./pages/PropertyKnowledgePage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { TermsOfServicePage } from "./pages/TermsOfServicePage";
import { VerifyEmailPage } from "./pages/VerifyEmailPage";
import { WhatsAppSettingsPage } from "./pages/WhatsAppSettingsPage";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "./utils/propertyRouting";
import { OnboardingGate } from "./routing/OnboardingGate";
import { HostAuthProvider } from "./providers/HostAuthProvider";

export default function App() {
  return (
    <HostAuthProvider>
      <AppRoutes />
    </HostAuthProvider>
  );
}

function AppRoutes() {
  const [path, setPath] = useState(() => window.location.pathname.toLowerCase());
  const configuredDemoPropertyId = normalizePropertyId(import.meta.env.VITE_DEMO_PROPERTY_ID);
  const indexPropertyId = resolvePropertyKnowledgePropertyId(null, configuredDemoPropertyId, import.meta.env.DEV);

  useEffect(() => {
    const updatePath = () => setPath(window.location.pathname.toLowerCase());
    window.addEventListener("popstate", updatePath);
    return () => window.removeEventListener("popstate", updatePath);
  }, []);

  if (/^\/privacy\/?$/.test(path)) {
    return <PrivacyPolicyPage />;
  }

  if (/^\/terms\/?$/.test(path)) {
    return <TermsOfServicePage />;
  }

  if (/^\/data-deletion\/?$/.test(path)) {
    return <DataDeletionPage />;
  }

  if (/^\/auth\/forgot-password\/?$/.test(path)) {
    return <ForgotPasswordPage />;
  }

  if (/^\/auth\/reset-password\/?$/.test(path)) {
    return <ResetPasswordPage />;
  }

  if (/^\/auth\/verify-email\/?$/.test(path)) {
    return <VerifyEmailPage />;
  }

  if (/^\/invitation\/respond\/?$/.test(path)) {
    return <InvitationDecisionPage />;
  }

  if (/^\/host\/settings\/account\/?$/.test(path)) {
    return <AccountSettingsPage />;
  }

  if (/^\/host\/settings\/billing\/?$/.test(path)) {
    return <OnboardingGate path={path}><BillingDashboardPage /></OnboardingGate>;
  }

  if (/^\/host\/settings\/billing\/subscription\/?$/.test(path)) {
    return <OnboardingGate path={path}><CurrentSubscriptionPage /></OnboardingGate>;
  }

  if (/^\/host\/settings\/billing\/plans\/?$/.test(path)) {
    return <OnboardingGate path={path}><PlanComparisonPage /></OnboardingGate>;
  }

  if (/^\/host\/settings\/whatsapp\/?$/.test(path)) {
    return <OnboardingGate path={path}><WhatsAppSettingsPage /></OnboardingGate>;
  }

  if (/^\/host\/settings\/organization\/?$/.test(path)) {
    return <OnboardingGate path={path}><OrganizationSettingsPage /></OnboardingGate>;
  }

  if (/^\/host\/organizations\/?$/.test(path)) {
    return <MyOrganizationsPage />;
  }

  if (path === "/onboarding" || path === "/onboarding/") {
    return <OnboardingGate path={path}><OnboardingPage /></OnboardingGate>;
  }

  if (/^\/onboarding\/welcome\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="welcome" /></OnboardingGate>;
  }

  if (/^\/onboarding\/organization\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="organization" /></OnboardingGate>;
  }

  if (/^\/onboarding\/plan\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="plan" /></OnboardingGate>;
  }

  if (/^\/onboarding\/property\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="property" /></OnboardingGate>;
  }

  if (/^\/onboarding\/team\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="team" /></OnboardingGate>;
  }

  if (/^\/onboarding\/whatsapp\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="whatsapp" /></OnboardingGate>;
  }

  if (/^\/onboarding\/ai\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="ai" /></OnboardingGate>;
  }

  if (/^\/onboarding\/knowledge\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="knowledge" /></OnboardingGate>;
  }

  if (/^\/onboarding\/demo\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="demo" /></OnboardingGate>;
  }

  if (/^\/onboarding\/review\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage routeStep="review" /></OnboardingGate>;
  }

  if (/^\/get-started\/?$/.test(path)) {
    return <OnboardingGate path={path}><OnboardingPage /></OnboardingGate>;
  }

  if (path === "/host/copilot" || path === "/host/copilot/") {
    return <OnboardingGate path={path}><HostCopilotWorkspacePage /></OnboardingGate>;
  }

  if (path === "/platform-admin" || path.startsWith("/platform-admin/")) {
    return <PlatformAdminPage />;
  }

  if (path === "/host" || path.startsWith("/host/conversations")) {
    return <OnboardingGate path={path}><HostInboxPage /></OnboardingGate>;
  }

  if (path === "/host/properties" || path === "/host/properties/") {
    return <OnboardingGate path={path}><PropertyKnowledgePage propertyId={indexPropertyId} /></OnboardingGate>;
  }

  const knowledgeMatch = path.match(/^\/host\/properties\/([^/]+)\/knowledge(?:\/)?$/);
  if (knowledgeMatch) {
    return <OnboardingGate path={path}><PropertyKnowledgePage propertyId={decodeURIComponent(knowledgeMatch[1])} /></OnboardingGate>;
  }

  return <DemoPage />;
}
