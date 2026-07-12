import { FormEvent, useState } from "react";

interface DevLoginPanelProps {
  defaultEmail?: string;
  isBusy: boolean;
  onLogin: (email: string, password: string) => Promise<void>;
}

export function DevLoginPanel({ defaultEmail = "", isBusy, onLogin }: DevLoginPanelProps) {
  const [email, setEmail] = useState(defaultEmail);
  const [password, setPassword] = useState("");

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onLogin(email, password);
  }

  return (
    <form className="sf-chat-login" onSubmit={handleSubmit}>
      <h3>Guest chat sign in</h3>
      <p>Use a seeded StayFlow account to test the authenticated widget locally.</p>
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
      <button type="submit" disabled={isBusy}>
        {isBusy ? "Signing in" : "Sign in"}
      </button>
    </form>
  );
}
