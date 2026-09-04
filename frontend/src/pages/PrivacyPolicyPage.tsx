import { useEffect } from "react";
import { LEGAL_CONTACT_EMAIL, DATA_DELETION_ROUTE } from "../config/legal";
import "../styles/legal-pages.css";

const PAGE_TITLE = "StayFlow Privacy Policy";

export function PrivacyPolicyPage() {
  useEffect(() => {
    document.title = PAGE_TITLE;
  }, []);

  return (
    <main className="sf-legal-page" aria-labelledby="sf-privacy-title">
      <article className="sf-legal-page-inner">
        <header>
          <p className="sf-legal-kicker">StayFlow</p>
          <h1 id="sf-privacy-title">Privacy Policy</h1>
          <p className="sf-legal-updated">Last updated: September 2026</p>
        </header>

        <section aria-labelledby="sf-privacy-overview">
          <h2 id="sf-privacy-overview">Overview</h2>
          <p>
            StayFlow is a multi-tenant hospitality software-as-a-service platform ("StayFlow", "we", "us")
            used by property managers and hospitality businesses ("hosts", "customers") to manage guest
            conversations, reservations, concierge requests, and payment workflows, including messaging over
            the Meta WhatsApp Business Platform. This Privacy Policy explains what information StayFlow
            processes, why, and the choices available to hosts and guests.
          </p>
          <p>
            Each host organization operates as an isolated tenant within StayFlow. Guest and reservation data
            entered by a host is scoped to that host&apos;s organization and is not shared with other tenants.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-info-we-process">
          <h2 id="sf-privacy-info-we-process">Information StayFlow Processes</h2>
          <ul>
            <li>
              <strong>Guest contact information:</strong> names, phone numbers, WhatsApp identifiers, and
              email addresses used to identify guests and deliver messages.
            </li>
            <li>
              <strong>Reservation and property information:</strong> stay dates, property and room details,
              reservation status, and related booking metadata provided by hosts or their booking sources.
            </li>
            <li>
              <strong>WhatsApp message content and messaging metadata:</strong> the text of guest and host
              messages, message timestamps, delivery/read status, and template usage associated with
              conversations routed through the WhatsApp Business Platform.
            </li>
            <li>
              <strong>Service and concierge requests:</strong> guest requests, host responses, internal notes,
              and AI-assisted draft replies generated to support hosts in responding to guests.
            </li>
            <li>
              <strong>Host/account information:</strong> host user names, email addresses, phone numbers,
              organization membership, roles/permissions, and authentication session data.
            </li>
            <li>
              <strong>Payment-related information:</strong> subscription billing details processed through
              our payment processor, and, where a host has configured it, M-PESA transaction references used
              to reconcile guest or booking payments. StayFlow does not store full payment card numbers.
            </li>
            <li>
              <strong>Usage and diagnostic information:</strong> API request activity, feature usage, and
              operational logs used to operate and secure the platform.
            </li>
          </ul>
        </section>

        <section aria-labelledby="sf-privacy-how-used">
          <h2 id="sf-privacy-how-used">How Information Is Used</h2>
          <ul>
            <li>To operate guest messaging, reservation management, and concierge workflows on behalf of hosts.</li>
            <li>To send and receive WhatsApp messages through the Meta WhatsApp Business Platform on a host&apos;s behalf.</li>
            <li>To generate AI-assisted drafts and recommendations for hosts responding to guests.</li>
            <li>To process subscription billing and, where applicable, reconcile M-PESA payment references.</li>
            <li>To maintain platform security, prevent abuse, and enforce tenant isolation between organizations.</li>
            <li>To provide customer support and troubleshoot reported issues.</li>
          </ul>
        </section>

        <section aria-labelledby="sf-privacy-tenant-responsibilities">
          <h2 id="sf-privacy-tenant-responsibilities">Tenant / Customer Responsibilities</h2>
          <p>
            Hosts using StayFlow act as the data controller for the guest information they collect and enter
            into the platform. Hosts are responsible for having a lawful basis to collect and message guests,
            for the accuracy of the information they submit, and for responding appropriately to guest privacy
            requests relating to their own reservations and stays. StayFlow acts as a data processor / service
            provider that processes this information on behalf of hosts to deliver the platform&apos;s features.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-meta-whatsapp">
          <h2 id="sf-privacy-meta-whatsapp">Meta / WhatsApp Business Platform Integration</h2>
          <p>
            StayFlow integrates with the Meta WhatsApp Business Platform (Graph API) to send and receive
            messages, message templates, and delivery status updates on behalf of hosts. Message content and
            delivery metadata pass through Meta&apos;s infrastructure as part of that integration and are subject
            to Meta&apos;s own platform terms and policies in addition to this Privacy Policy. StayFlow configures
            WhatsApp Business integrations per host organization and does not use guest WhatsApp message
            content to train third-party advertising products.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-payments">
          <h2 id="sf-privacy-payments">Payment Processing and M-PESA</h2>
          <p>
            Subscription billing for hosts is processed through a third-party payment processor. Where a host
            enables M-PESA payment workflows, StayFlow processes M-PESA transaction references and status
            callbacks to reconcile guest or booking payments for that host&apos;s properties. StayFlow does not
            control the M-PESA network and is not responsible for the mobile network operator&apos;s own
            processing of payment data.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-service-providers">
          <h2 id="sf-privacy-service-providers">Service Providers and Subprocessors</h2>
          <p>
            StayFlow relies on service providers to operate the platform, which may include cloud hosting,
            messaging (Meta WhatsApp Business Platform), payment processing, and AI processing providers used
            to generate concierge draft responses. These providers process information only as necessary to
            provide their service to StayFlow and are bound by contractual confidentiality and data protection
            obligations.
          </p>
          <p>
            StayFlow does not sell personal information to third parties.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-retention">
          <h2 id="sf-privacy-retention">Data Retention</h2>
          <p>
            StayFlow retains guest, reservation, and conversation information for as long as needed to provide
            the platform to the relevant host organization, or as otherwise required by the host&apos;s own
            configuration and applicable record-keeping obligations. Specific retention periods are not fixed
            in this policy and depend on host configuration, active subscription status, and legitimate
            operational or legal record-keeping needs. See the Data Deletion section below to request removal
            of personal data.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-security">
          <h2 id="sf-privacy-security">Security</h2>
          <p>
            StayFlow applies technical and organizational measures intended to protect information processed
            on the platform, including tenant isolation between host organizations, role-based access controls,
            and authenticated API access. No method of transmission or storage is completely secure, and
            StayFlow cannot guarantee absolute security.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-international">
          <h2 id="sf-privacy-international">International Processing</h2>
          <p>
            StayFlow and its service providers, including Meta and payment processors, may process information
            in countries other than the country where a host or guest is located. Where this occurs, StayFlow
            relies on its service providers&apos; own safeguards for cross-border processing.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-rights">
          <h2 id="sf-privacy-rights">Your Privacy Rights</h2>
          <p>
            Depending on your location and applicable law, you may have rights to access, correct, or request
            deletion of your personal information, or to object to certain processing. Guests should generally
            direct these requests to the host/property they interacted with, since StayFlow processes guest
            data on behalf of that host. See{" "}
            <a href={DATA_DELETION_ROUTE}>StayFlow Data Deletion Instructions</a> for how to submit a deletion
            request.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-children">
          <h2 id="sf-privacy-children">Children and Minors</h2>
          <p>
            StayFlow is a business-to-business hospitality platform intended for use by hosts, property staff,
            and adult guests booking accommodation. StayFlow does not knowingly direct the service to children,
            and account registration is intended for adults acting on behalf of a hospitality business or as a
            guest of legal age to book a reservation.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-changes">
          <h2 id="sf-privacy-changes">Changes to This Policy</h2>
          <p>
            We may update this Privacy Policy from time to time to reflect changes to StayFlow&apos;s features or
            legal requirements. Material changes will be reflected by updating the "Last updated" date above.
          </p>
        </section>

        <section aria-labelledby="sf-privacy-contact">
          <h2 id="sf-privacy-contact">Contact</h2>
          <p>
            Questions about this Privacy Policy can be sent to{" "}
            <a href={`mailto:${LEGAL_CONTACT_EMAIL}`}>{LEGAL_CONTACT_EMAIL}</a>.
          </p>
        </section>
      </article>
    </main>
  );
}
