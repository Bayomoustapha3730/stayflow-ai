import { useEffect } from "react";
import { LEGAL_CONTACT_EMAIL } from "../config/legal";
import "../styles/legal-pages.css";

const PAGE_TITLE = "StayFlow Terms of Service";

export function TermsOfServicePage() {
  useEffect(() => {
    document.title = PAGE_TITLE;
  }, []);

  return (
    <main className="sf-legal-page" aria-labelledby="sf-terms-title">
      <article className="sf-legal-page-inner">
        <header>
          <p className="sf-legal-kicker">StayFlow</p>
          <h1 id="sf-terms-title">Terms of Service</h1>
          <p className="sf-legal-updated">Last updated: September 2026</p>
        </header>

        <div className="sf-legal-placeholder-notice" role="note">
          This page is a template Terms of Service and must be reviewed by a qualified legal advisor before
          production publication. It intentionally does not state a specific legal jurisdiction or
          corporate-registration details.
        </div>

        <section aria-labelledby="sf-terms-use">
          <h2 id="sf-terms-use">Use of StayFlow</h2>
          <p>
            StayFlow is a hospitality software-as-a-service platform that helps property managers and hosts
            manage guest conversations, reservations, concierge requests, WhatsApp messaging, and payment
            workflows. By creating an account or using StayFlow, you agree to these Terms of Service.
          </p>
        </section>

        <section aria-labelledby="sf-terms-account">
          <h2 id="sf-terms-account">Account Responsibilities</h2>
          <ul>
            <li>You are responsible for maintaining the confidentiality of your login credentials and sessions.</li>
            <li>You are responsible for the accuracy of the information you provide when creating or managing an organization, property, or user account.</li>
            <li>You must promptly notify StayFlow of any suspected unauthorized use of your account.</li>
          </ul>
        </section>

        <section aria-labelledby="sf-terms-hosts">
          <h2 id="sf-terms-hosts">Hospitality / Property-Manager Responsibilities</h2>
          <p>
            As a host organization, you are responsible for the guest, reservation, and property data you
            enter into StayFlow, for configuring your own WhatsApp Business and payment integrations correctly,
            and for managing user roles and permissions within your organization. You act as the party
            responsible for your guests&apos; personal data collected through StayFlow, and StayFlow processes
            that data on your behalf to provide the platform&apos;s features.
          </p>
        </section>

        <section aria-labelledby="sf-terms-acceptable-use">
          <h2 id="sf-terms-acceptable-use">Acceptable Use</h2>
          <ul>
            <li>Do not use StayFlow to send unlawful, deceptive, abusive, or unsolicited messages.</li>
            <li>Do not attempt to access another organization&apos;s tenant data or circumvent tenant isolation controls.</li>
            <li>Do not use StayFlow to violate Meta&apos;s WhatsApp Business Platform policies or any applicable messaging or payments regulation.</li>
            <li>Do not use the platform to interfere with its normal operation, including through excessive automated requests.</li>
          </ul>
        </section>

        <section aria-labelledby="sf-terms-whatsapp">
          <h2 id="sf-terms-whatsapp">WhatsApp / Meta Integration and Messaging Consent</h2>
          <p>
            StayFlow enables hosts to send and receive messages through the Meta WhatsApp Business Platform.
            Hosts are solely responsible for obtaining any consent required to message their guests, for the
            lawfulness of the content of their messages, and for complying with Meta&apos;s WhatsApp Business
            Platform policies and applicable messaging laws in the jurisdictions where they operate. StayFlow
            provides the technical integration but does not review or approve the substance of guest
            communications sent by hosts.
          </p>
        </section>

        <section aria-labelledby="sf-terms-third-party">
          <h2 id="sf-terms-third-party">Third-Party Services</h2>
          <p>
            StayFlow relies on third-party services, including Meta&apos;s WhatsApp Business Platform, payment
            processors, and AI providers, to deliver certain features. Availability and behavior of these
            features may depend on the continued availability of those third-party services, which are outside
            StayFlow&apos;s control.
          </p>
        </section>

        <section aria-labelledby="sf-terms-payments">
          <h2 id="sf-terms-payments">Payment Integrations</h2>
          <p>
            Subscription fees are billed through our payment processor. Where enabled, M-PESA payment
            workflows are provided to help hosts reconcile guest or booking payments; StayFlow is not a party
            to, and is not responsible for, the underlying mobile money transaction between a guest and the
            mobile network operator.
          </p>
        </section>

        <section aria-labelledby="sf-terms-availability">
          <h2 id="sf-terms-availability">Availability and Changes to the Service</h2>
          <p>
            StayFlow may modify, update, or discontinue features of the platform over time, and may perform
            maintenance that temporarily affects availability. We will make reasonable efforts to communicate
            material changes that affect existing functionality.
          </p>
        </section>

        <section aria-labelledby="sf-terms-ip">
          <h2 id="sf-terms-ip">Intellectual Property</h2>
          <p>
            StayFlow and its underlying software, design, and branding are the property of StayFlow or its
            licensors. Hosts retain ownership of the guest, reservation, and property data they submit to the
            platform.
          </p>
        </section>

        <section aria-labelledby="sf-terms-termination">
          <h2 id="sf-terms-termination">Termination</h2>
          <p>
            Either party may stop using or providing StayFlow. We may suspend or terminate access to an
            organization&apos;s account for violation of these Terms, non-payment, or activity that risks the
            security or integrity of the platform or other tenants.
          </p>
        </section>

        <section aria-labelledby="sf-terms-disclaimers">
          <h2 id="sf-terms-disclaimers">Disclaimers and Limitation of Liability</h2>
          <p>
            StayFlow is provided on an "as is" and "as available" basis without warranties of any kind, express
            or implied, except as required by applicable law. To the maximum extent permitted by applicable
            law, StayFlow will not be liable for indirect, incidental, or consequential damages arising from
            use of the platform. Nothing in these Terms is intended to state or limit rights that cannot
            lawfully be limited in your jurisdiction.
          </p>
        </section>

        <section aria-labelledby="sf-terms-contact">
          <h2 id="sf-terms-contact">Contact</h2>
          <p>
            Questions about these Terms of Service can be sent to{" "}
            <a href={`mailto:${LEGAL_CONTACT_EMAIL}`}>{LEGAL_CONTACT_EMAIL}</a>.
          </p>
        </section>
      </article>
    </main>
  );
}
