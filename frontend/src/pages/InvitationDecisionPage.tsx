import { getRuntimeApiUrl } from "../runtimeConfig";
import { FormEvent, useMemo, useState } from "react";
import { createInvitationApi } from "../api/invitationApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import "../styles/public-auth.css";

export function InvitationDecisionPage() {
  const token = new URLSearchParams(window.location.search).get("token")?.trim() ?? "";
  const auth = useHostAuth();
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showRegistration, setShowRegistration] = useState(false);

  const anonymousApi = useMemo(() => createInvitationApi(new HttpClient({
    baseUrl: getRuntimeApiUrl()
  })), []);
  const authenticatedApi = useMemo(() => createInvitationApi(new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => auth.accessToken
  })), [auth.accessToken]);

  function runAction(action: "accept" | "reject") {
    if (!token) {
      setError("Invitation token is missing.");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setMessage(null);

    const request = { token };
    const promise = action === "accept"
      ? authenticatedApi.accept(request)
      : anonymousApi.reject(request);

    void promise
      .then(() => setMessage(action === "accept" ? "Invitation accepted." : "Invitation rejected."))
      .catch((failure) => {
        if (failure instanceof ApiError && failure.status === 401) {
          setError("Sign in to accept this invitation.");
          return;
        }

        setError(failure instanceof Error ? failure.message : "Unable to process invitation.");
      })
      .finally(() => setIsSubmitting(false));
  }

  async function register(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      setError("Invitation token is missing.");
      return;
    }

    const form = new FormData(event.currentTarget);
    setError(null);
    try {
      await auth.register({
        fullName: String(form.get("fullName") ?? ""),
        email: String(form.get("email") ?? ""),
        phoneNumber: String(form.get("phoneNumber") ?? ""),
        password: String(form.get("password") ?? ""),
        countryCode: "KE",
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
        invitationToken: token
      });
      setMessage("Account created and invitation accepted.");
    } catch {
      // The shared auth hook supplies the user-safe error state.
    }
  }

  return (
    <main className="sf-public-auth-shell">
      <section className="sf-public-auth-card">
        <h1>Organization Invitation</h1>
        <p>Review this organization invitation and choose whether to accept or reject it.</p>
        {!auth.isAuthenticated ? (
          <>
            {showRegistration ? (
              <form className="sf-host-login" onSubmit={register}>
                <label>Full name<input name="fullName" required /></label>
                <label>Email<input name="email" type="email" required /></label>
                <label>Phone number<input name="phoneNumber" type="tel" required /></label>
                <label>Password<input name="password" type="password" minLength={12} required /></label>
                <button type="submit" disabled={auth.isSigningIn}>Create account</button>
                <button type="button" onClick={() => setShowRegistration(false)}>I have an account</button>
              </form>
            ) : (
              <>
                <HostLoginPanel
                  isSigningIn={auth.isSigningIn}
                  error={auth.error}
                  onLogin={auth.login}
                  onClearError={auth.clearError}
                />
                <button type="button" onClick={() => setShowRegistration(true)}>Create account</button>
              </>
            )}
          </>
        ) : null}
        {error ? <div className="sf-public-auth-error">{error}</div> : null}
        {message ? <div className="sf-public-auth-status">{message}</div> : null}
        <div className="sf-public-auth-actions">
          <button type="button" disabled={isSubmitting || !auth.isAuthenticated} onClick={() => runAction("accept")}>{isSubmitting ? "Processing..." : "Accept invitation"}</button>
          <button type="button" disabled={isSubmitting} onClick={() => runAction("reject")}>Reject invitation</button>
        </div>
      </section>
    </main>
  );
}