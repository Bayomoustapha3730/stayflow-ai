import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { createWhatsAppSettingsApi } from "../api/whatsAppSettingsApi";
import type {
  WhatsAppIntegrationHealth,
  WhatsAppIntegrationSummary,
  WhatsAppTemplateDetail,
  WhatsAppTemplateListResponse,
  WhatsAppTemplateSyncResult
} from "../models/whatsAppSettings";

interface UseWhatsAppSettingsOptions {
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UseWhatsAppSettingsResult {
  integrations: WhatsAppIntegrationSummary[];
  selectedIntegrationId: string | null;
  selectedIntegration: WhatsAppIntegrationSummary | null;
  health: WhatsAppIntegrationHealth | null;
  templatesResponse: WhatsAppTemplateListResponse | null;
  selectedTemplate: WhatsAppTemplateDetail | null;
  isLoadingIntegrations: boolean;
  isLoadingTemplates: boolean;
  isLoadingTemplateDetail: boolean;
  isCheckingHealth: boolean;
  isSyncingTemplates: boolean;
  error: string | null;
  templatesError: string | null;
  actionMessage: string | null;
  syncResult: WhatsAppTemplateSyncResult | null;
  search: string;
  statusFilter: string;
  languageFilter: string;
  categoryFilter: string;
  approvedOnly: boolean;
  page: number;
  pageSize: number;
  setSelectedIntegrationId: (integrationId: string) => void;
  setSearch: (value: string) => void;
  setStatusFilter: (value: string) => void;
  setLanguageFilter: (value: string) => void;
  setCategoryFilter: (value: string) => void;
  setApprovedOnly: (value: boolean) => void;
  setPage: (value: number) => void;
  setPageSize: (value: number) => void;
  selectTemplate: (templateId: string) => Promise<void>;
  checkHealth: () => Promise<void>;
  syncTemplates: () => Promise<void>;
  refresh: () => Promise<void>;
}

const defaultPageSize = 20;

export function useWhatsAppSettings({ accessToken, onUnauthorized }: UseWhatsAppSettingsOptions): UseWhatsAppSettingsResult {
  const [integrations, setIntegrations] = useState<WhatsAppIntegrationSummary[]>([]);
  const [selectedIntegrationId, setSelectedIntegrationId] = useState<string | null>(null);
  const [health, setHealth] = useState<WhatsAppIntegrationHealth | null>(null);
  const [templatesResponse, setTemplatesResponse] = useState<WhatsAppTemplateListResponse | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<WhatsAppTemplateDetail | null>(null);
  const [isLoadingIntegrations, setIsLoadingIntegrations] = useState(false);
  const [isLoadingTemplates, setIsLoadingTemplates] = useState(false);
  const [isLoadingTemplateDetail, setIsLoadingTemplateDetail] = useState(false);
  const [isCheckingHealth, setIsCheckingHealth] = useState(false);
  const [isSyncingTemplates, setIsSyncingTemplates] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [templatesError, setTemplatesError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [syncResult, setSyncResult] = useState<WhatsAppTemplateSyncResult | null>(null);

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [languageFilter, setLanguageFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [approvedOnly, setApprovedOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);

  const integrationRequestVersionRef = useRef(0);
  const templatesRequestVersionRef = useRef(0);
  const detailRequestVersionRef = useRef(0);

  const http = useMemo(
    () => new HttpClient({
      baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
      getAccessToken: () => accessToken
    }),
    [accessToken]
  );

  const api = useMemo(() => createWhatsAppSettingsApi(http), [http]);

  const selectedIntegration = useMemo(
    () => integrations.find((item) => item.id === selectedIntegrationId) ?? null,
    [integrations, selectedIntegrationId]
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, statusFilter, languageFilter, categoryFilter, approvedOnly, pageSize]);

  const handleFailure = useCallback((failure: unknown, fallbackMessage: string) => {
    if (failure instanceof ApiError && failure.status === 401) {
      onUnauthorized();
      return "Your host session expired. Please sign in again.";
    }

    if (failure instanceof Error && failure.message.trim()) {
      return failure.message;
    }

    return fallbackMessage;
  }, [onUnauthorized]);

  const loadIntegrations = useCallback(async () => {
    if (!accessToken) {
      setIntegrations([]);
      setSelectedIntegrationId(null);
      setHealth(null);
      setTemplatesResponse(null);
      setSelectedTemplate(null);
      setError(null);
      setTemplatesError(null);
      return;
    }

    const version = ++integrationRequestVersionRef.current;
    setIsLoadingIntegrations(true);
    setError(null);

    try {
      const items = await api.listIntegrations();
      if (version !== integrationRequestVersionRef.current) {
        return;
      }

      setIntegrations(items);
      setSelectedIntegrationId((current) => {
        if (current && items.some((item) => item.id === current)) {
          return current;
        }

        return items[0]?.id ?? null;
      });
    } catch (failure) {
      if (version !== integrationRequestVersionRef.current) {
        return;
      }

      setIntegrations([]);
      setSelectedIntegrationId(null);
      setError(handleFailure(failure, "Unable to load WhatsApp integrations."));
    } finally {
      if (version === integrationRequestVersionRef.current) {
        setIsLoadingIntegrations(false);
      }
    }
  }, [accessToken, api, handleFailure]);

  const loadTemplates = useCallback(async () => {
    if (!accessToken || !selectedIntegrationId) {
      setTemplatesResponse(null);
      setSelectedTemplate(null);
      setTemplatesError(null);
      return;
    }

    const version = ++templatesRequestVersionRef.current;
    setIsLoadingTemplates(true);
    setTemplatesError(null);

    try {
      const response = await api.listTemplates(selectedIntegrationId, {
        search: debouncedSearch || undefined,
        status: statusFilter || undefined,
        language: languageFilter || undefined,
        category: categoryFilter || undefined,
        approvedOnly,
        page,
        pageSize
      });

      if (version !== templatesRequestVersionRef.current) {
        return;
      }

      setTemplatesResponse(response);
      setSelectedTemplate((current) => {
        if (!current) {
          return current;
        }

        return response.items.some((item) => item.id === current.id) ? current : null;
      });
    } catch (failure) {
      if (version !== templatesRequestVersionRef.current) {
        return;
      }

      setTemplatesResponse(null);
      setSelectedTemplate(null);
      setTemplatesError(handleFailure(failure, "Unable to load WhatsApp templates."));
    } finally {
      if (version === templatesRequestVersionRef.current) {
        setIsLoadingTemplates(false);
      }
    }
  }, [accessToken, api, approvedOnly, categoryFilter, debouncedSearch, handleFailure, languageFilter, page, pageSize, selectedIntegrationId, statusFilter]);

  useEffect(() => {
    void loadIntegrations();
  }, [loadIntegrations]);

  useEffect(() => {
    void loadTemplates();
  }, [loadTemplates]);

  const selectTemplate = useCallback(async (templateId: string) => {
    if (!selectedIntegrationId || !accessToken) {
      setSelectedTemplate(null);
      return;
    }

    const version = ++detailRequestVersionRef.current;
    setIsLoadingTemplateDetail(true);
    setActionMessage(null);

    try {
      const detail = await api.getTemplate(selectedIntegrationId, templateId);
      if (version !== detailRequestVersionRef.current) {
        return;
      }

      setSelectedTemplate(detail);
    } catch (failure) {
      if (version !== detailRequestVersionRef.current) {
        return;
      }

      setActionMessage(handleFailure(failure, "Unable to load template preview."));
    } finally {
      if (version === detailRequestVersionRef.current) {
        setIsLoadingTemplateDetail(false);
      }
    }
  }, [accessToken, api, handleFailure, selectedIntegrationId]);

  const checkHealth = useCallback(async () => {
    if (!selectedIntegrationId || !accessToken) {
      return;
    }

    setIsCheckingHealth(true);
    setActionMessage(null);

    try {
      const result = await api.checkIntegrationHealth(selectedIntegrationId);
      setHealth(result);
      setActionMessage("Health check completed.");
      await loadIntegrations();
    } catch (failure) {
      setActionMessage(handleFailure(failure, "Unable to check integration health."));
    } finally {
      setIsCheckingHealth(false);
    }
  }, [accessToken, api, handleFailure, loadIntegrations, selectedIntegrationId]);

  const syncTemplates = useCallback(async () => {
    if (!selectedIntegrationId || !accessToken) {
      return;
    }

    setIsSyncingTemplates(true);
    setActionMessage(null);

    try {
      const result = await api.syncTemplates(selectedIntegrationId);
      setSyncResult(result);
      setActionMessage("Template synchronization completed.");
      await Promise.all([loadIntegrations(), loadTemplates()]);
    } catch (failure) {
      setActionMessage(handleFailure(failure, "Unable to synchronize templates."));
    } finally {
      setIsSyncingTemplates(false);
    }
  }, [accessToken, api, handleFailure, loadIntegrations, loadTemplates, selectedIntegrationId]);

  const refresh = useCallback(async () => {
    await Promise.all([loadIntegrations(), loadTemplates()]);
  }, [loadIntegrations, loadTemplates]);

  return {
    integrations,
    selectedIntegrationId,
    selectedIntegration,
    health,
    templatesResponse,
    selectedTemplate,
    isLoadingIntegrations,
    isLoadingTemplates,
    isLoadingTemplateDetail,
    isCheckingHealth,
    isSyncingTemplates,
    error,
    templatesError,
    actionMessage,
    syncResult,
    search,
    statusFilter,
    languageFilter,
    categoryFilter,
    approvedOnly,
    page,
    pageSize,
    setSelectedIntegrationId,
    setSearch,
    setStatusFilter,
    setLanguageFilter,
    setCategoryFilter,
    setApprovedOnly,
    setPage,
    setPageSize,
    selectTemplate,
    checkHealth,
    syncTemplates,
    refresh
  };
}
