import { createContext } from "react";
import type { UseHostAuthResult } from "../hooks/useHostAuth";

/**
 * Holds the single shared host-auth runtime instance. There must be exactly one
 * HostAuthProvider per authenticated application root; consumers read from it via
 * useHostAuth() instead of independently instantiating their own auth state.
 */
export const HostAuthContext = createContext<UseHostAuthResult | undefined>(undefined);
