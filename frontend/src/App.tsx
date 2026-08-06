import { DemoPage } from "./pages/DemoPage";
import { AccountSettingsPage } from "./pages/AccountSettingsPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { HostCopilotWorkspacePage } from "./pages/HostCopilotWorkspacePage";
import { HostInboxPage } from "./pages/HostInboxPage";
import { InvitationDecisionPage } from "./pages/InvitationDecisionPage";
import { OrganizationSettingsPage } from "./pages/OrganizationSettingsPage";
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

  if (/^\/host\/settings\/whatsapp\/?$/.test(path)) {
    return <WhatsAppSettingsPage />;
  }

  if (/^\/host\/settings\/organization\/?$/.test(path)) {
    return <OrganizationSettingsPage />;
  }

  if (path === "/host/copilot" || path === "/host/copilot/") {
    return <HostCopilotWorkspacePage />;
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
