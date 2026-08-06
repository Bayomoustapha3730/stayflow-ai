import { useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { HttpClient } from "../api/httpClient";
import "../styles/public-auth.css";

export function VerifyEmailPage() {
  const token = new URLSearchParams(window.location.search).get("token")?.trim() ?? "";
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const api = useMemo(() => createAuthApi(new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243"
  })), []);

  return (
    <main className="sf-public-auth-shell">
      <section className="sf-public-auth-card">
        <h1>Verify Email</h1>
        <p>Confirm your email address to complete your StayFlow identity setup.</p>
        {error ? <div className="sf-public-auth-error">{error}</div> : null}
        {message ? <div className="sf-public-auth-status">{message}</div> : null}
        <button
          type="button"
          disabled={isSubmitting || !token}
          onClick={() => {
            if (!token) {
              setError("Verification token is missing.");
              return;
            }

            setIsSubmitting(true);
            setError(null);
            setMessage(null);
            void api.confirmEmailVerification({ token })
              .then(() => setMessage("Email verified successfully."))
              .catch((failure) => setError(failure instanceof Error ? failure.message : "Unable to verify email."))
              .finally(() => setIsSubmitting(false));
          }}
        >
          {isSubmitting ? "Verifying..." : "Verify email"}
        </button>
      </section>
    </main>
  );
}