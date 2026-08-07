import { DemoPage } from "./pages/DemoPage";
import { AccountSettingsPage } from "./pages/AccountSettingsPage";
import { BillingDashboardPage } from "./pages/BillingDashboardPage";
import { CurrentSubscriptionPage } from "./pages/CurrentSubscriptionPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { HostCopilotWorkspacePage } from "./pages/HostCopilotWorkspacePage";
import { HostInboxPage } from "./pages/HostInboxPage";
import { InvitationDecisionPage } from "./pages/InvitationDecisionPage";
import { OnboardingPage } from "./pages/OnboardingPage";
import { OrganizationSettingsPage } from "./pages/OrganizationSettingsPage";
import { PlanComparisonPage } from "./pages/PlanComparisonPage";
import { PlatformAdminPage } from "./pages/PlatformAdminPage";
import { PropertyKnowledgePage } from "./pages/PropertyKnowledgePage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { VerifyEmailPage } from "./pages/VerifyEmailPage";
import { WhatsAppSettingsPage } from "./pages/WhatsAppSettingsPage";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "./utils/propertyRouting";

export default function App() {
  const path = window.location.pathname.toLowerCase();
  const configuredDemoPropertyId = normalizePropertyId(import.meta.env.VITE_DEMO_PROPERTY_ID);
  const indexPropertyId = resolvePropertyKnowledgePropertyId(null, configuredDemoPropertyId, import.meta.env.DEV);

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
    return <BillingDashboardPage />;
  }

  if (/^\/host\/settings\/billing\/subscription\/?$/.test(path)) {
    return <CurrentSubscriptionPage />;
  }

  if (/^\/host\/settings\/billing\/plans\/?$/.test(path)) {
    return <PlanComparisonPage />;
  }

  if (/^\/host\/settings\/whatsapp\/?$/.test(path)) {
    return <WhatsAppSettingsPage />;
  }

  if (/^\/host\/settings\/organization\/?$/.test(path)) {
    return <OrganizationSettingsPage />;
  }

  if (path === "/onboarding" || path === "/onboarding/") {
    return <OnboardingPage />;
  }

  if (/^\/onboarding\/welcome\/?$/.test(path)) {
    return <OnboardingPage routeStep="welcome" />;
  }

  if (/^\/onboarding\/organization\/?$/.test(path)) {
    return <OnboardingPage routeStep="organization" />;
  }

  if (/^\/onboarding\/plan\/?$/.test(path)) {
    return <OnboardingPage routeStep="plan" />;
  }

  if (/^\/onboarding\/property\/?$/.test(path)) {
    return <OnboardingPage routeStep="property" />;
  }

  if (/^\/onboarding\/team\/?$/.test(path)) {
    return <OnboardingPage routeStep="team" />;
  }

  if (/^\/onboarding\/whatsapp\/?$/.test(path)) {
    return <OnboardingPage routeStep="whatsapp" />;
  }

  if (/^\/onboarding\/ai\/?$/.test(path)) {
    return <OnboardingPage routeStep="ai" />;
  }

  if (/^\/onboarding\/knowledge\/?$/.test(path)) {
    return <OnboardingPage routeStep="knowledge" />;
  }

  if (/^\/onboarding\/demo\/?$/.test(path)) {
    return <OnboardingPage routeStep="demo" />;
  }

  if (/^\/onboarding\/review\/?$/.test(path)) {
    return <OnboardingPage routeStep="review" />;
  }

  if (/^\/get-started\/?$/.test(path)) {
    return <OnboardingPage routeStep="completed" />;
  }

  if (path === "/host/copilot" || path === "/host/copilot/") {
    return <HostCopilotWorkspacePage />;
  }

  if (path === "/platform-admin" || path.startsWith("/platform-admin/")) {
    return <PlatformAdminPage />;
  }

  if (path === "/host" || path.startsWith("/host/conversations")) {
    return <HostInboxPage />;
  }

  if (path === "/host/properties" || path === "/host/properties/") {
    return <PropertyKnowledgePage propertyId={indexPropertyId} />;
  }

  const knowledgeMatch = path.match(/^\/host\/properties\/([^/]+)\/knowledge(?:\/)?$/);
  if (knowledgeMatch) {
    return <PropertyKnowledgePage propertyId={decodeURIComponent(knowledgeMatch[1])} />;
  }

  return <DemoPage />;
}
