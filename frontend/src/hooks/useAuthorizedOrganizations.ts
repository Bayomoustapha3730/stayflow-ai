import { useCallback, useEffect, useMemo, useState } from "react";
import { createAuthApi } from "../api/authApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type { AuthorizedOrganizationSummary } from "../models/organization";
import { getRuntimeApiUrl } from "../runtimeConfig";

interface UseAuthorizedOrganizationsOptions {
  accessToken: string | null;
  onUnauthorized?: () => void;
}

export type AuthorizedOrganizationsLoadStatus = "loading" | "loaded" | "empty" | "error";

export function useAuthorizedOrganizations({ accessToken, onUnauthorized }: UseAuthorizedOrganizationsOptions) {
  const [organizations, setOrganizations] = useState<AuthorizedOrganizationSummary[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadStatus, setLoadStatus] = useState<AuthorizedOrganizationsLoadStatus>("loading");
  const [error, setError] = useState<string | null>(null);

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);
  const api = useMemo(() => createAuthApi(http), [http]);

  const refresh = useCallback(async () => {
    if (!accessToken) {
      setOrganizations([]);
      setError(null);
      setLoadStatus("empty");
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);
    setLoadStatus("loading");

    try {
      const nextOrganizations = await api.listOrganizations();
      setOrganizations(nextOrganizations);
      setLoadStatus(nextOrganizations.length === 0 ? "empty" : "loaded");
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized?.();
        setOrganizations([]);
        setError(null);
        setLoadStatus("error");
        return;
      }

      setOrganizations([]);
      setError(failure instanceof Error ? failure.message : "Unable to load organizations.");
      setLoadStatus("error");
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, api, onUnauthorized]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    organizations,
    isLoading,
    loadStatus,
    error,
    refresh
  };
}