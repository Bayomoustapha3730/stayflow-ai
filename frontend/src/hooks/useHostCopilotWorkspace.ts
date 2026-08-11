import { getRuntimeApiUrl } from "../runtimeConfig";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { createHostCopilotWorkspaceApi } from "../api/hostCopilotWorkspaceApi";
import type {
  HostCopilotWorkspaceResponse,
  HostCopilotDraftResponse,
  HostCopilotDraftValidationResponse
} from "../models/hostCopilotWorkspace";
import { useConversationRealtime } from "./useConversationRealtime";

interface UseHostCopilotWorkspaceOptions {
  accessToken: string | null;
  onUnauthorized: () => void;
  propertyId?: string | null;
}

export interface UseHostCopilotWorkspaceResult {
  workspace: HostCopilotWorkspaceResponse | null;
  selectedWorkItemId: string | null;
  isLoading: boolean;
  isSendingDraft: boolean;
  error: string | null;
  draftResult: HostCopilotDraftResponse | null;
  validationResult: HostCopilotDraftValidationResponse | null;
  realtimeState: "offline" | "connecting" | "online" | "reconnecting";
  selectWorkItem: (workItemId: string) => void;
  refresh: () => Promise<void>;
  generateDraft: (conversationId: string, tone?: string, hostInstruction?: string) => Promise<void>;
  validateDraft: (conversationId: string, draft: string) => Promise<void>;
  sendDraft: (conversationId: string, draft: string) => Promise<void>;
  approveAction: (actionId: string, decisionNote?: string) => Promise<void>;
  declineAction: (actionId: string, decisionNote?: string) => Promise<void>;
}

export function useHostCopilotWorkspace({ accessToken, onUnauthorized, propertyId }: UseHostCopilotWorkspaceOptions): UseHostCopilotWorkspaceResult {
  const [workspace, setWorkspace] = useState<HostCopilotWorkspaceResponse | null>(null);
  const [selectedWorkItemId, setSelectedWorkItemId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSendingDraft, setIsSendingDraft] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draftResult, setDraftResult] = useState<HostCopilotDraftResponse | null>(null);
  const [validationResult, setValidationResult] = useState<HostCopilotDraftValidationResponse | null>(null);
  const requestVersionRef = useRef(0);

  const http = useMemo(() => new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  }), [accessToken]);

  const api = useMemo(() => createHostCopilotWorkspaceApi(http), [http]);

  const loadWorkspace = useCallback(async () => {
    if (!accessToken) {
      setWorkspace(null);
      setError(null);
      return;
    }

    const requestVersion = ++requestVersionRef.current;
    setIsLoading(true);
    setError(null);

    try {
      const response = await api.getWorkspace(propertyId);
      if (requestVersion !== requestVersionRef.current) {
        return;
      }

      setWorkspace(response);
      setSelectedWorkItemId((current) => {
        const hasCurrent = response.items.some((item) => item.workItemId === current);
        return hasCurrent ? current : (response.items[0]?.workItemId ?? null);
      });
    } catch (failure) {
      if (requestVersion !== requestVersionRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to load host copilot workspace.");
    } finally {
      if (requestVersion === requestVersionRef.current) {
        setIsLoading(false);
      }
    }
  }, [accessToken, api, onUnauthorized, propertyId]);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const refresh = useCallback(async () => {
    await loadWorkspace();
  }, [loadWorkspace]);

  const generateDraft = useCallback(async (conversationId: string, tone?: string, hostInstruction?: string) => {
    try {
      const response = await api.generateDraft(conversationId, { tone, hostInstruction });
      setDraftResult(response);
      setValidationResult(response.validation);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to generate draft.");
    }
  }, [api, onUnauthorized]);

  const validateDraft = useCallback(async (conversationId: string, draft: string) => {
    try {
      const response = await api.validateDraft(conversationId, { draft });
      setValidationResult(response);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to validate draft.");
    }
  }, [api, onUnauthorized]);

  const sendDraft = useCallback(async (conversationId: string, draft: string) => {
    setIsSendingDraft(true);
    try {
      await api.sendDraft(conversationId, { draft });
      await loadWorkspace();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to send draft.");
    } finally {
      setIsSendingDraft(false);
    }
  }, [api, loadWorkspace, onUnauthorized]);

  const approveAction = useCallback(async (actionId: string, decisionNote?: string) => {
    try {
      await api.approveAction(actionId, { decisionNote });
      await loadWorkspace();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to approve action.");
    }
  }, [api, loadWorkspace, onUnauthorized]);

  const declineAction = useCallback(async (actionId: string, decisionNote?: string) => {
    try {
      await api.declineAction(actionId, { decisionNote });
      await loadWorkspace();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setError(failure instanceof Error ? failure.message : "Unable to decline action.");
    }
  }, [api, loadWorkspace, onUnauthorized]);

  const realtime = useConversationRealtime({
    accessToken,
    conversationId: null,
    enabled: Boolean(accessToken),
    onMessageCreated: () => {
      void loadWorkspace();
    },
    onMessageUpdated: () => {
      void loadWorkspace();
    },
    onAssigned: () => {
      void loadWorkspace();
    },
    onStateChanged: () => {
      void loadWorkspace();
    },
    onHostCopilotWorkspaceUpdated: () => {
      void loadWorkspace();
    }
  });

  return {
    workspace,
    selectedWorkItemId,
    isLoading,
    isSendingDraft,
    error,
    draftResult,
    validationResult,
    realtimeState: realtime.connectionState,
    selectWorkItem: setSelectedWorkItemId,
    refresh,
    generateDraft,
    validateDraft,
    sendDraft,
    approveAction,
    declineAction
  };
}
