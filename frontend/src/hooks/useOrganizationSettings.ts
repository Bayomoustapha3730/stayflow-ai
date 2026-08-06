import { useCallback, useEffect, useMemo, useState } from "react";
import { createOrganizationApi } from "../api/organizationApi";
import { ApiError, HttpClient } from "../api/httpClient";
import type {
  OrganizationMember,
  OrganizationSummary,
  UpdateOrganizationRequest
} from "../models/organization";

export interface UseOrganizationSettingsOptions {
  accessToken: string | null;
  onUnauthorized?: () => void;
}

export function useOrganizationSettings({ accessToken, onUnauthorized }: UseOrganizationSettingsOptions) {
  const [organization, setOrganization] = useState<OrganizationSummary | null>(null);
  const [members, setMembers] = useState<OrganizationMember[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const http = useMemo(
    () =>
      new HttpClient({
        baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
        getAccessToken: () => accessToken
      }),
    [accessToken]
  );

  const api = useMemo(() => createOrganizationApi(http), [http]);

  const refresh = useCallback(async () => {
    if (!accessToken) {
      setOrganization(null);
      setMembers([]);
      return;
    }

    setIsLoading(true);
    setError(null);
    try {
      const [org, team] = await Promise.all([api.getCurrent(), api.listMembers()]);
      setOrganization(org);
      setMembers(team);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized?.();
      }
      setError(failure instanceof Error ? failure.message : "Unable to load organization settings.");
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, api, onUnauthorized]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const updateOrganization = useCallback(async (request: UpdateOrganizationRequest) => {
    if (!accessToken) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      const updated = await api.updateCurrent(request);
      setOrganization(updated);
      setMessage("Organization updated.");
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized?.();
      }
      setError(failure instanceof Error ? failure.message : "Unable to update organization.");
    } finally {
      setIsSaving(false);
    }
  }, [accessToken, api, onUnauthorized]);

  const updateMemberRole = useCallback(async (memberUserId: string, role: string) => {
    if (!accessToken) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      const updatedMember = await api.updateMemberRole(memberUserId, role);
      setMembers((current) => current.map((member) => member.userId === memberUserId ? updatedMember : member));
      setMessage("Member role updated.");
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized?.();
      }
      setError(failure instanceof Error ? failure.message : "Unable to update member role.");
    } finally {
      setIsSaving(false);
    }
  }, [accessToken, api, onUnauthorized]);

  const removeMember = useCallback(async (memberUserId: string) => {
    if (!accessToken) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await api.removeMember(memberUserId);
      setMembers((current) => current.filter((member) => member.userId !== memberUserId));
      setMessage("Member removed.");
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized?.();
      }
      setError(failure instanceof Error ? failure.message : "Unable to remove member.");
    } finally {
      setIsSaving(false);
    }
  }, [accessToken, api, onUnauthorized]);

  return {
    organization,
    members,
    isLoading,
    isSaving,
    error,
    message,
    refresh,
    updateOrganization,
    updateMemberRole,
    removeMember
  };
}