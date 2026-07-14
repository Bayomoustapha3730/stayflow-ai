import { StayFlowChatWidget } from "../components";

const demoGuestId = import.meta.env.VITE_DEMO_GUEST_ID ?? "44444444-4444-4444-4444-444444444444";
const demoReservationId = import.meta.env.VITE_DEMO_RESERVATION_ID;
const demoPropertyId = import.meta.env.VITE_DEMO_PROPERTY_ID;
const demoEmail = import.meta.env.VITE_DEMO_EMAIL;

export function DemoPage() {
  return (
    <div className="sf-demo-page">
      <main className="sf-demo-content">
        <section className="sf-demo-hero">
          <div>
            <span className="sf-demo-kicker">StayFlow AI</span>
            <h1>Guest concierge chat widget</h1>
            <p>
              A protected web chat experience for Airbnb-style stays, connected to the StayFlow conversation engine.
            </p>
          </div>
        </section>

        <section className="sf-demo-grid" aria-label="Demo property details">
          <article>
            <h2>Westlands Apartment</h2>
            <p>Fast answers for check-in, Wi-Fi, house rules, and host escalation.</p>
          </article>
          <article>
            <h2>Authenticated by design</h2>
            <p>The widget uses the existing JWT login flow and never sends tenant identifiers from the browser.</p>
          </article>
          <article>
            <h2>Human handoff</h2>
            <p>Guests can ask the host for support, and closed conversations stop accepting new messages.</p>
          </article>
        </section>
      </main>

      <StayFlowChatWidget
        const apiBaseUrl ={
    import.meta.env.VITE_STAYFLOW_API_URL ??
    "http://localhost:5243"}
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
    </div>
  );
}
