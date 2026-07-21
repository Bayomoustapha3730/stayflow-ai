import { DemoPage } from "./pages/DemoPage";
import { HostInboxPage } from "./pages/HostInboxPage";

export default function App() {
  const path = window.location.pathname.toLowerCase();

  if (path === "/host" || path.startsWith("/host/conversations")) {
    return <HostInboxPage />;
  }

  return <DemoPage />;
}
