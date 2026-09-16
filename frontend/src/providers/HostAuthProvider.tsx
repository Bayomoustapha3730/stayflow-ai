import type { ReactNode } from "react";
import { HostAuthContext } from "../context/HostAuthContext";
import { useHostAuthState } from "../hooks/useHostAuth";

export interface HostAuthProviderProps {
  children: ReactNode;
}

/**
 * The single owner of host authentication/organization state. Mount once at the
 * authenticated application root so every consumer (pages, OnboardingGate, nav/selector
 * components) shares one session-initialization/token-refresh/organization-switch lifecycle
 * instead of each independently racing the same side effects.
 */
export function HostAuthProvider({ children }: HostAuthProviderProps) {
  const auth = useHostAuthState();
  return <HostAuthContext.Provider value={auth}>{children}</HostAuthContext.Provider>;
}
