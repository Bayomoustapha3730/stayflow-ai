import { getRuntimeApiUrl } from "../runtimeConfig";
import { StayFlowChatWidget } from "../components";
import { DATA_DELETION_ROUTE, PRIVACY_POLICY_ROUTE, TERMS_OF_SERVICE_ROUTE } from "../config/legal";
import "../styles/legal-pages.css";

const demoGuestId =
  import.meta.env.VITE_DEMO_GUEST_ID ??
  "44444444-4444-4444-4444-444444444444";

const defaultDemoReservationId =
  import.meta.env.VITE_DEMO_RESERVATION_ID ??
  "55555555-5555-5555-5555-555555555555";

const demoPropertyId =
  import.meta.env.VITE_DEMO_PROPERTY_ID;

const demoEmail =
  import.meta.env.VITE_DEMO_EMAIL;

const apiBaseUrl = getRuntimeApiUrl();

const DEMO_PAYMENT_RESERVATION_REFERENCE = "DEMO-PAY-002";
const DEMO_PAYMENT_RESERVATION_ID = "55555555-5555-5555-5555-555555555556";

export function resolveDemoReservationId(): string {
  const params = new URLSearchParams(window.location.search);
  const reservationOverride = params.get("reservation")?.trim();

  if (!reservationOverride) {
    return defaultDemoReservationId;
  }

  const normalized = reservationOverride.toUpperCase();
  if (normalized === DEMO_PAYMENT_RESERVATION_REFERENCE) {
    return DEMO_PAYMENT_RESERVATION_ID;
  }

  return reservationOverride;
}

export function DemoPage() {
  const demoReservationId = resolveDemoReservationId();

  return (
    <div className="sf-demo-page">
      <main className="sf-demo-content">
        <section className="sf-demo-hero">
          <div>
            <span className="sf-demo-kicker">
              StayFlow AI
            </span>

            <h1>Guest concierge chat widget</h1>

            <p>
              A protected web chat experience for Airbnb-style
              stays, connected to the StayFlow conversation
              engine.
            </p>
          </div>
        </section>

        <section
          className="sf-demo-grid"
          aria-label="Demo property details"
        >
          <article>
            <h2>Westlands Apartment</h2>
            <p>
              Fast answers for check-in, Wi-Fi, house rules,
              and host escalation.
            </p>
          </article>

          <article>
            <h2>Authenticated by design</h2>
            <p>
              The widget uses the existing JWT login flow and
              never sends tenant identifiers from the browser.
            </p>
          </article>

          <article>
            <h2>Human handoff</h2>
            <p>
              Guests can ask the host for support, and closed
              conversations stop accepting new messages.
            </p>
          </article>
        </section>
      </main>

      <StayFlowChatWidget
        apiBaseUrl={apiBaseUrl}
        guestId={demoGuestId}
        reservationId={demoReservationId}
        propertyId={demoPropertyId}
        demoEmail={demoEmail}
        theme={{
          propertyDisplayName: "Westlands Apartment",
          primaryColor: "#0F3D3E",
          accentColor: "#F2A65A",
          guestBubbleColor: "#0F3D3E",
          assistantBubbleColor: "#F6F8F7"
        }}
      />
      <footer className="sf-public-footer">
        <a href={PRIVACY_POLICY_ROUTE}>Privacy Policy</a>
        <a href={TERMS_OF_SERVICE_ROUTE}>Terms of Service</a>
        <a href={DATA_DELETION_ROUTE}>Data Deletion Instructions</a>
      </footer>    </div>
  );
}