import { FormEvent, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { HttpClient } from "../api/httpClient";
import "../styles/public-auth.css";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const api = useMemo(() => createAuthApi(new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243"
  })), []);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setMessage(null);

    void api.requestPasswordReset(email)
      .then(() => setMessage("If the account exists, a password reset email has been sent."))
      .catch((failure) => setError(failure instanceof Error ? failure.message : "Unable to request password reset."))
      .finally(() => setIsSubmitting(false));
  }

  return (
    <main className="sf-public-auth-shell">
      <form className="sf-public-auth-card" onSubmit={handleSubmit}>
        <h1>Forgot Password</h1>
        <p>Enter your email to request a password reset link.</p>
        <label>
          Email
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required autoComplete="email" />
        </label>
        {error ? <div className="sf-public-auth-error">{error}</div> : null}
        {message ? <div className="sf-public-auth-status">{message}</div> : null}
        <button type="submit" disabled={isSubmitting}>{isSubmitting ? "Submitting..." : "Send reset link"}</button>
      </form>
    </main>
  );
}