import { DemoPage } from "./pages/DemoPage";
import { HostInboxPage } from "./pages/HostInboxPage";
import { PropertyKnowledgePage } from "./pages/PropertyKnowledgePage";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "./utils/propertyRouting";

export default function App() {
  const path = window.location.pathname.toLowerCase();
  const configuredDemoPropertyId = normalizePropertyId(import.meta.env.VITE_DEMO_PROPERTY_ID);
  const indexPropertyId = resolvePropertyKnowledgePropertyId(null, configuredDemoPropertyId, import.meta.env.DEV);

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
