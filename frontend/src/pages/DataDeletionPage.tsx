import { useEffect } from "react";
import { LEGAL_CONTACT_EMAIL, PRIVACY_POLICY_ROUTE } from "../config/legal";
import "../styles/legal-pages.css";

const PAGE_TITLE = "StayFlow Data Deletion Instructions";

export function DataDeletionPage() {
  useEffect(() => {
    document.title = PAGE_TITLE;
  }, []);

  return (
    <main className="sf-legal-page" aria-labelledby="sf-data-deletion-title">
      <article className="sf-legal-page-inner">
        <header>
          <p className="sf-legal-kicker">StayFlow</p>
          <h1 id="sf-data-deletion-title">Data Deletion Instructions</h1>
          <p className="sf-legal-updated">Last updated: September 2026</p>
        </header>

        <section aria-labelledby="sf-data-deletion-how">
          <h2 id="sf-data-deletion-how">How to Request Deletion</h2>
          <p>
            To request deletion of personal data processed by StayFlow, send a request to{" "}
            <a href={`mailto:${LEGAL_CONTACT_EMAIL}`}>{LEGAL_CONTACT_EMAIL}</a> and include the details below.
          </p>
        </section>

        <section aria-labelledby="sf-data-deletion-info">
          <h2 id="sf-data-deletion-info">Information to Include</h2>
          <ul>
            <li>Your full name and the email address or phone number associated with the account or guest record.</li>
            <li>Whether you are a StayFlow host/account holder or a guest of a specific property.</li>
            <li>If known, the name of the host organization or property involved.</li>
            <li>A description of the data you want deleted (for example: account data, conversation/WhatsApp message history, or reservation records).</li>
          </ul>
        </section>

        <section aria-labelledby="sf-data-deletion-verification">
          <h2 id="sf-data-deletion-verification">Identity Verification</h2>
          <p>
            To protect against fraudulent deletion requests, StayFlow may require you to verify your identity,
            such as confirming the phone number, email address, or account details on file, before processing
            a deletion request.
          </p>
        </section>

        <section aria-labelledby="sf-data-deletion-guests">
          <h2 id="sf-data-deletion-guests">Requests Involving a Guest Stay</h2>
          <p>
            StayFlow is a multi-tenant platform: guest and reservation data is entered and controlled by the
            host/property a guest interacted with. Where your request relates to a guest stay, StayFlow may
            need to coordinate with the relevant host organization to locate, verify, and action the request,
            since that host is responsible for the underlying guest data.
          </p>
        </section>

        <section aria-labelledby="sf-data-deletion-limits">
          <h2 id="sf-data-deletion-limits">Limits on Deletion</h2>
          <p>
            Deletion may be limited where StayFlow or a host organization must legitimately retain certain
            records, for example completed payment/billing records needed for financial reconciliation or
            dispute handling, or records that a host is otherwise required to keep. Where full deletion is not
            possible, StayFlow will restrict further use of the data where feasible and explain the limitation.
          </p>
        </section>

        <section aria-labelledby="sf-data-deletion-more">
          <h2 id="sf-data-deletion-more">Related Information</h2>
          <p>
            See the <a href={PRIVACY_POLICY_ROUTE}>StayFlow Privacy Policy</a> for more information about what
            data StayFlow processes and how it is used.
          </p>
        </section>
      </article>
    </main>
  );
}
