import { useMemo, useState } from "react";
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

  const anonymousApi = useMemo(() => createInvitationApi(new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243"
  })), []);
  const authenticatedApi = useMemo(() => createInvitationApi(new HttpClient({
    baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
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

  return (
    <main className="sf-public-auth-shell">
      <section className="sf-public-auth-card">
        <h1>Organization Invitation</h1>
        <p>Review this organization invitation and choose whether to accept or reject it.</p>
        {!auth.isAuthenticated ? (
          <HostLoginPanel
            isSigningIn={auth.isSigningIn}
            error={auth.error}
            onLogin={auth.login}
            onClearError={auth.clearError}
          />
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