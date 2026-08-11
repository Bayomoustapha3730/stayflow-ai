import { getRuntimeApiUrl } from "../runtimeConfig";
import { FormEvent, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { HttpClient } from "../api/httpClient";
import "../styles/public-auth.css";

export function ResetPasswordPage() {
  const token = new URLSearchParams(window.location.search).get("token")?.trim() ?? "";
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const api = useMemo(() => createAuthApi(new HttpClient({
    baseUrl: getRuntimeApiUrl()
  })), []);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      setError("Reset token is missing.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Password confirmation does not match.");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setMessage(null);

    void api.confirmPasswordReset({ token, newPassword })
      .then(() => setMessage("Password reset successfully."))
      .catch((failure) => setError(failure instanceof Error ? failure.message : "Unable to reset password."))
      .finally(() => setIsSubmitting(false));
  }

  return (
    <main className="sf-public-auth-shell">
      <form className="sf-public-auth-card" onSubmit={handleSubmit}>
        <h1>Reset Password</h1>
        <p>Choose a new password for your StayFlow account.</p>
        <label>
          New Password
          <input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required autoComplete="new-password" />
        </label>
        <label>
          Confirm Password
          <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} required autoComplete="new-password" />
        </label>
        {error ? <div className="sf-public-auth-error">{error}</div> : null}
        {message ? <div className="sf-public-auth-status">{message}</div> : null}
        <button type="submit" disabled={isSubmitting}>{isSubmitting ? "Updating..." : "Reset password"}</button>
      </form>
    </main>
  );
}