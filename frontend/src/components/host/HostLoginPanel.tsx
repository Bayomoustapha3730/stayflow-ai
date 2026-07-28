import { FormEvent, useState } from "react";

interface HostLoginPanelProps {
  defaultEmail?: string;
  isSigningIn: boolean;
  error: string | null;
  onLogin: (email: string, password: string) => Promise<void>;
  onClearError: () => void;
}

export function HostLoginPanel({
  defaultEmail = import.meta.env.VITE_DEMO_EMAIL ?? "",
  isSigningIn,
  error,
  onLogin,
  onClearError
}: HostLoginPanelProps) {
  const [email, setEmail] = useState(defaultEmail);
  const [password, setPassword] = useState("");

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onClearError();
    try {
      await onLogin(email, password);
    } catch {
      // Error state is surfaced by the auth hook.
    }
  }

  return (
    <form className="sf-host-login" onSubmit={handleSubmit}>
      <h1>Host Sign In</h1>
      <p>Use a seeded development account to access the host inbox.</p>

      <label>
        Email
        <input
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          autoComplete="username"
          required
        />
      </label>

      <label>
        Password
        <input
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          autoComplete="current-password"
          required
        />
      </label>

      {error ? <div className="sf-host-login-error">{error}</div> : null}

      <button type="submit" disabled={isSigningIn}>
        {isSigningIn ? "Signing in..." : "Sign in"}
      </button>
    </form>
  );
}
