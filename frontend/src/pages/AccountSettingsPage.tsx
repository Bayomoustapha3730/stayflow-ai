import { getRuntimeApiUrl } from "../runtimeConfig";
import { useCallback, useEffect, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";
import { HostConsoleNav, HostLoginPanel } from "../components/host";
import type { AuthSession } from "../models/auth";
import { useHostAuth } from "../hooks/useHostAuth";
import "../styles/host-inbox.css";
import "../styles/account-settings.css";

function formatDateTime(value?: string | null): string {
  if (!value) {
    return "Not available";
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? "Not available" : parsed.toLocaleString();
}

function isSessionNotFoundError(failure: unknown): boolean {
  if (!(failure instanceof Error)) {
    return false;
  }

  return failure.message.toLowerCase().includes("session was not found");
}

export function AccountSettingsPage() {
  const auth = useHostAuth();
  const {
    accessToken,
    currentUser,
    isAuthenticated,
    isSigningIn,
    error: authError,
    login,
    logout,
    clearError,
    refreshCurrentUser,
    setCurrentUserProfile
  } = auth;
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [preferredLanguage, setPreferredLanguage] = useState("en");
  const [timeZone, setTimeZone] = useState("UTC");
  const [emailNotificationsEnabled, setEmailNotificationsEnabled] = useState(true);
  const [securityNotificationsEnabled, setSecurityNotificationsEnabled] = useState(true);
  const [productUpdatesEnabled, setProductUpdatesEnabled] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [sessions, setSessions] = useState<AuthSession[]>([]);
  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [isLoadingSessions, setIsLoadingSessions] = useState(false);
  const [isSendingVerification, setIsSendingVerification] = useState(false);
  const [isRevokingAllSessions, setIsRevokingAllSessions] = useState(false);
  const [revokingSessionIds, setRevokingSessionIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);
  const api = useMemo(() => createAuthApi(http), [http]);

  useEffect(() => {
    if (!currentUser) {
      return;
    }

    setFullName(currentUser.fullName);
    setPhoneNumber(currentUser.phoneNumber);
    setPreferredLanguage(currentUser.preferredLanguage);
    setTimeZone(currentUser.timeZone);
    setEmailNotificationsEnabled(currentUser.emailNotificationsEnabled);
    setSecurityNotificationsEnabled(currentUser.securityNotificationsEnabled);
    setProductUpdatesEnabled(currentUser.productUpdatesEnabled);
  }, [currentUser]);

  const loadSessions = useCallback(async () => {
    if (!isAuthenticated) {
      setSessions([]);
      return;
    }

    setIsLoadingSessions(true);

    try {
      const items = await api.listSessions();
      setSessions(items);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        logout();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to load sessions.");
    } finally {
      setIsLoadingSessions(false);
    }
  }, [api, isAuthenticated, logout]);

  useEffect(() => {
    void loadSessions();
  }, [loadSessions]);

  if (!isAuthenticated) {
    return (
      <div className="sf-host-login-shell">
        <HostLoginPanel
          isSigningIn={isSigningIn}
          error={authError}
          onLogin={login}
          onClearError={clearError}
        />
      </div>
    );
  }

  return (
    <div className="sf-host-page sf-account-page">
      <div className="sf-host-page-top">
        <header className="sf-organization-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Account Settings</h1>
            <p className="sf-host-muted-note">Manage your profile, session security, and notification preferences.</p>
          </div>
          <div className="sf-organization-header-actions">
            <button type="button" onClick={() => void refreshCurrentUser()}>Refresh profile</button>
            <button type="button" onClick={() => logout()}>Sign out</button>
          </div>
        </header>

        <HostConsoleNav
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          accountSettingsHref="/host/settings/account"
          current="account"
        />

        <div className="sf-organization-access-note">
          Signed in as: <strong>{currentUser?.fullName ?? "Host"}</strong> ({currentUser?.organizationRole ?? "Unknown role"})
        </div>

        {error ? <div className="sf-host-inline-error" role="alert"><p>{error}</p></div> : null}
        {message ? <div className="sf-whatsapp-status" role="status">{message}</div> : null}

        <section className="sf-organization-grid" aria-label="Account settings">
          <article className="sf-organization-card">
            <h2>Profile</h2>
            <label>
              Full Name
              <input value={fullName} onChange={(event) => setFullName(event.target.value)} disabled={isSavingProfile} />
            </label>
            <label>
              Email
              <input value={currentUser?.email ?? ""} disabled />
            </label>
            <label>
              Phone Number
              <input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} disabled={isSavingProfile} />
            </label>
            <label>
              Preferred Language
              <input value={preferredLanguage} onChange={(event) => setPreferredLanguage(event.target.value)} disabled={isSavingProfile} />
            </label>
            <label>
              Time Zone
              <input value={timeZone} onChange={(event) => setTimeZone(event.target.value)} disabled={isSavingProfile} />
            </label>
            <label className="sf-account-checkbox">
              <input type="checkbox" checked={emailNotificationsEnabled} onChange={(event) => setEmailNotificationsEnabled(event.target.checked)} disabled={isSavingProfile} />
              Email notifications
            </label>
            <label className="sf-account-checkbox">
              <input type="checkbox" checked={securityNotificationsEnabled} onChange={(event) => setSecurityNotificationsEnabled(event.target.checked)} disabled={isSavingProfile} />
              Security notifications
            </label>
            <label className="sf-account-checkbox">
              <input type="checkbox" checked={productUpdatesEnabled} onChange={(event) => setProductUpdatesEnabled(event.target.checked)} disabled={isSavingProfile} />
              Product updates
            </label>
            <button
              type="button"
              disabled={isSavingProfile}
              onClick={() => {
                setIsSavingProfile(true);
                setError(null);
                setMessage(null);
                void api.updateCurrentUser({
                  fullName,
                  phoneNumber,
                  preferredLanguage,
                  timeZone,
                  emailNotificationsEnabled,
                  securityNotificationsEnabled,
                  productUpdatesEnabled
                })
                  .then((profile) => {
                    setCurrentUserProfile(profile);
                    setMessage("Profile updated.");
                  })
                  .catch((failure) => {
                    if (failure instanceof ApiError && failure.status === 401) {
                      logout();
                      return;
                    }

                    setError(failure instanceof Error ? failure.message : "Unable to update profile.");
                  })
                  .finally(() => setIsSavingProfile(false));
              }}
            >
              {isSavingProfile ? "Saving..." : "Save Profile"}
            </button>
          </article>

          <article className="sf-organization-card">
            <h2>Security</h2>
            <p>Email verification: <strong>{currentUser?.isEmailVerified ? "Verified" : "Pending"}</strong></p>
            {!currentUser?.isEmailVerified ? (
              <button
                type="button"
                disabled={isSendingVerification}
                onClick={() => {
                  setIsSendingVerification(true);
                  setError(null);
                  setMessage(null);
                  void api.resendEmailVerification()
                    .then(() => setMessage("Verification email sent."))
                    .catch((failure) => {
                      if (failure instanceof ApiError && failure.status === 401) {
                        logout();
                        return;
                      }

                      setError(failure instanceof Error ? failure.message : "Unable to send verification email.");
                    })
                    .finally(() => setIsSendingVerification(false));
                }}
              >
                {isSendingVerification ? "Sending..." : "Resend verification email"}
              </button>
            ) : null}

            <label>
              Current Password
              <input type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} autoComplete="current-password" disabled={isChangingPassword} />
            </label>
            <label>
              New Password
              <input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} autoComplete="new-password" disabled={isChangingPassword} />
            </label>
            <label>
              Confirm New Password
              <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" disabled={isChangingPassword} />
            </label>
            <button
              type="button"
              disabled={isChangingPassword}
              onClick={() => {
                if (newPassword !== confirmPassword) {
                  setError("Password confirmation does not match.");
                  return;
                }

                setIsChangingPassword(true);
                setError(null);
                setMessage(null);
                void api.changePassword({ currentPassword, newPassword })
                  .then(() => {
                    setCurrentPassword("");
                    setNewPassword("");
                    setConfirmPassword("");
                    setMessage("Password changed. Other sessions were revoked.");
                    void loadSessions();
                  })
                  .catch((failure) => {
                    if (failure instanceof ApiError && failure.status === 401) {
                      logout();
                      return;
                    }

                    setError(failure instanceof Error ? failure.message : "Unable to change password.");
                  })
                  .finally(() => setIsChangingPassword(false));
              }}
            >
              {isChangingPassword ? "Updating..." : "Change Password"}
            </button>
          </article>

          <article className="sf-organization-card">
            <h2>Sessions</h2>
            {isLoadingSessions ? <p>Loading sessions...</p> : null}
            {!isLoadingSessions && sessions.length === 0 ? <p>No active refresh sessions found.</p> : null}
            <div className="sf-account-session-list" role="list">
              {sessions.map((session) => (
                <div key={session.sessionId} className="sf-account-session-row" role="listitem">
                  <div>
                    <p className="sf-organization-member-name">{session.isCurrent ? "Current session" : "Active session"}</p>
                    <p className="sf-organization-member-meta">Started {formatDateTime(session.createdAtUtc)}</p>
                    <p className="sf-organization-member-meta">Last used {formatDateTime(session.lastUsedAtUtc ?? session.createdAtUtc)}</p>
                    <p className="sf-organization-member-meta">Expires {formatDateTime(session.expiresAtUtc)}</p>
                  </div>
                  <button
                    type="button"
                    disabled={isRevokingAllSessions || revokingSessionIds.includes(session.sessionId)}
                    onClick={() => {
                      if (isRevokingAllSessions || revokingSessionIds.includes(session.sessionId)) {
                        return;
                      }

                      setError(null);
                      setMessage(null);
                      setRevokingSessionIds((current) => current.concat(session.sessionId));
                      void api.revokeSession(session.sessionId)
                        .then(() => {
                          setSessions((current) => current.filter((item) => item.sessionId !== session.sessionId));
                          setMessage(session.isCurrent ? "Current refresh session revoked. Sign in again when needed." : "Session revoked.");
                        })
                        .catch((failure) => {
                          if (failure instanceof ApiError && failure.status === 401) {
                            logout();
                            return;
                          }

                          if (isSessionNotFoundError(failure)) {
                            setSessions((current) => current.filter((item) => item.sessionId !== session.sessionId));
                            setMessage("Session already revoked.");
                            return;
                          }

                          setError(failure instanceof Error ? failure.message : "Unable to revoke session.");
                        })
                        .finally(() => {
                          setRevokingSessionIds((current) => current.filter((id) => id !== session.sessionId));
                        });
                    }}
                  >
                    {revokingSessionIds.includes(session.sessionId) ? "Revoking..." : "Revoke"}
                  </button>
                </div>
              ))}
            </div>
            <button
              type="button"
              disabled={isRevokingAllSessions || sessions.length === 0}
              onClick={() => {
                if (isRevokingAllSessions) {
                  return;
                }

                setError(null);
                setMessage(null);
                setIsRevokingAllSessions(true);
                void api.revokeAllSessions()
                  .then(() => {
                    setSessions([]);
                    setMessage("All refresh sessions revoked.");
                  })
                  .catch((failure) => {
                    if (failure instanceof ApiError && failure.status === 401) {
                      logout();
                      return;
                    }

                    setError(failure instanceof Error ? failure.message : "Unable to revoke all sessions.");
                  })
                  .finally(() => {
                    setIsRevokingAllSessions(false);
                    setRevokingSessionIds([]);
                  });
              }}
            >
              {isRevokingAllSessions ? "Revoking all sessions..." : "Revoke All Sessions"}
            </button>
          </article>
        </section>
      </div>
    </div>
  );
}