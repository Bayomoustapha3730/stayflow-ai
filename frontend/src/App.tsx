import { DemoPage } from "./pages/DemoPage";
import { HostCopilotWorkspacePage } from "./pages/HostCopilotWorkspacePage";
import { HostInboxPage } from "./pages/HostInboxPage";
import { OrganizationSettingsPage } from "./pages/OrganizationSettingsPage";
import { PlatformAdminPage } from "./pages/PlatformAdminPage";
import { PropertyKnowledgePage } from "./pages/PropertyKnowledgePage";
import { WhatsAppSettingsPage } from "./pages/WhatsAppSettingsPage";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "./utils/propertyRouting";

export default function App() {
  const path = window.location.pathname.toLowerCase();
  const configuredDemoPropertyId = normalizePropertyId(import.meta.env.VITE_DEMO_PROPERTY_ID);
  const indexPropertyId = resolvePropertyKnowledgePropertyId(null, configuredDemoPropertyId, import.meta.env.DEV);

  if (/^\/host\/settings\/whatsapp\/?$/.test(path)) {
    return <WhatsAppSettingsPage />;
  }

  if (/^\/host\/settings\/organization\/?$/.test(path)) {
    return <OrganizationSettingsPage />;
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
