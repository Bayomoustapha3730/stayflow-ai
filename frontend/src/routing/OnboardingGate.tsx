import { useEffect } from "react";
import { useHostAuth } from "../hooks/useHostAuth";
import { useOnboardingStatus } from "../hooks/useOnboardingStatus";
import type { OnboardingStatus } from "../models/onboarding";

const ONBOARDING_PATH_PATTERN = /^\/(onboarding(\/.*)?|get-started)\/?$/;

// Host routes that represent normal post-onboarding application usage. Organization switching
// (/host/organizations) and personal account settings (/host/settings/account) are intentionally
// excluded so a user is never trapped by an incomplete organization's onboarding state.
const GATED_HOST_PATH_PATTERNS: RegExp[] = [
  /^\/host\/?$/,
  /^\/host\/conversations(\/.*)?\/?$/,
  /^\/host\/copilot\/?$/,
  /^\/host\/properties(\/.*)?\/?$/,
  /^\/host\/settings\/billing(\/.*)?\/?$/,
  /^\/host\/settings\/whatsapp\/?$/,
  /^\/host\/settings\/organization\/?$/
];

export function isOnboardingRoute(path: string): boolean {
  return ONBOARDING_PATH_PATTERN.test(path);
}

export function isGatedHostRoute(path: string): boolean {
  return GATED_HOST_PATH_PATTERNS.some((pattern) => pattern.test(path));
}

function findSafeLinkHref(status: OnboardingStatus | null, rel: string): string | null {
  const href = status?.safeLinks?.find((item) => item.rel === rel)?.href;
  return href && href.startsWith("/") ? href : null;
}

export function resolveOnboardingTarget(status: OnboardingStatus | null): string {
  return findSafeLinkHref(status, "current_step") ?? "/onboarding";
}

export function resolvePostOnboardingTarget(status: OnboardingStatus | null): string {
  return findSafeLinkHref(status, "host_inbox") ?? "/host/conversations";
}

export type OnboardingGateDecision =
  | { kind: "render" }
  | { kind: "loading" }
  | { kind: "redirect"; to: string }
  | { kind: "error" };

export interface UseOnboardingGateResult {
  decision: OnboardingGateDecision;
  retry: () => void;
}

/**
 * Server-authoritative onboarding route decision for the given path. Consumes the canonical
 * onboarding-status hook only; it never recalculates progress/completion itself.
 */
export function useOnboardingGate(path: string): UseOnboardingGateResult {
  const auth = useHostAuth();
  const activeCompanyId = auth.currentUser?.companyId ?? null;
  const needsGate = isOnboardingRoute(path) || isGatedHostRoute(path);

  // Only fetch onboarding status when this route actually requires a gating decision, and only
  // once the active organization is known (accessToken alone is not enough to attribute status).
  const onboarding = useOnboardingStatus({
    accessToken: needsGate ? auth.accessToken : null,
    activeCompanyId
  });

  const retry = () => {
    void onboarding.refresh();
  };

  if (!auth.isAuthenticated || !needsGate) {
    return { decision: { kind: "render" }, retry };
  }

  if (!activeCompanyId || !onboarding.isResolved) {
    return { decision: { kind: "loading" }, retry };
  }

  if (onboarding.error || !onboarding.status) {
    // Never treat a failed/unknown lookup as "completed". Onboarding routes stay reachable
    // (the wizard has its own error/retry UI); gated host routes are blocked until resolved.
    return {
      decision: isOnboardingRoute(path) ? { kind: "render" } : { kind: "error" },
      retry
    };
  }

  const status = onboarding.status;

  if (isGatedHostRoute(path) && !status.isCompleted) {
    return { decision: { kind: "redirect", to: resolveOnboardingTarget(status) }, retry };
  }

  if (isOnboardingRoute(path) && status.isCompleted) {
    return { decision: { kind: "redirect", to: resolvePostOnboardingTarget(status) }, retry };
  }

  return { decision: { kind: "render" }, retry };
}

export interface OnboardingGateProps {
  path: string;
  children: React.ReactNode;
}

/**
 * Wraps a route's rendered output with server-authoritative onboarding enforcement. The wrapped
 * children are only ever mounted when the decision is "render", so a protected/onboarding page's
 * own component body never executes while status is loading, redirecting, or unresolved.
 */
export function OnboardingGate({ path, children }: OnboardingGateProps) {
  const { decision, retry } = useOnboardingGate(path);

  useEffect(() => {
    if (decision.kind === "redirect" && decision.to !== window.location.pathname) {
      window.history.replaceState({}, "", decision.to);
      window.dispatchEvent(new PopStateEvent("popstate"));
    }
  }, [decision]);

  if (decision.kind === "loading" || decision.kind === "redirect") {
    return (
      <div className="sf-host-page-top">
        <p>Loading...</p>
      </div>
    );
  }

  if (decision.kind === "error") {
    return (
      <div className="sf-host-page-top">
        <div className="sf-host-inline-error" role="alert">
          <p>Unable to verify onboarding status. Please try again.</p>
        </div>
        <button type="button" onClick={retry}>Retry</button>
      </div>
    );
  }

  return <>{children}</>;
}
