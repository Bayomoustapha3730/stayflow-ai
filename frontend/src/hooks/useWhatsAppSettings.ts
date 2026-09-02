import { getRuntimeApiUrl } from "../runtimeConfig";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { createWhatsAppSettingsApi } from "../api/whatsAppSettingsApi";
import type {
  WhatsAppIntegrationConfiguration,
  WhatsAppIntegrationDetail,
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
  isSavingConfiguration: boolean;
  isChangingProduction: boolean;
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
  getIntegrationConfiguration: (integrationId: string) => Promise<WhatsAppIntegrationDetail | null>;
  saveIntegrationConfiguration: (request: WhatsAppIntegrationConfiguration, integrationId?: string) => Promise<WhatsAppIntegrationDetail | null>;
  setProductionEnabled: (enabled: boolean) => Promise<void>;
  refresh: () => Promise<void>;
}

const defaultPageSize = 20;

export function useWhatsAppSettings({ accessToken, onUnauthorized }: UseWhatsAppSettingsOptions): UseWhatsAppSettingsResult {
  const isMountedRef = useRef(true);

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
  const [isSavingConfiguration, setIsSavingConfiguration] = useState(false);
  const [isChangingProduction, setIsChangingProduction] = useState(false);
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
  const integrationsAbortRef = useRef<AbortController | null>(null);
  const templatesAbortRef = useRef<AbortController | null>(null);
  const detailAbortRef = useRef<AbortController | null>(null);
  const healthAbortRef = useRef<AbortController | null>(null);
  const syncAbortRef = useRef<AbortController | null>(null);

  const http = useMemo(
    () => new HttpClient({
      baseUrl: getRuntimeApiUrl(),
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
    isMountedRef.current = true;

    return () => {
      isMountedRef.current = false;
      integrationRequestVersionRef.current += 1;
      templatesRequestVersionRef.current += 1;
      detailRequestVersionRef.current += 1;

      integrationsAbortRef.current?.abort();
      templatesAbortRef.current?.abort();
      detailAbortRef.current?.abort();
      healthAbortRef.current?.abort();
      syncAbortRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    const timer = globalThis.setTimeout(() => {
      if (!isMountedRef.current) {
        return;
      }

      setDebouncedSearch(search.trim());
    }, 300);

    return () => globalThis.clearTimeout(timer);
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
    integrationsAbortRef.current?.abort();
    const controller = new AbortController();
    integrationsAbortRef.current = controller;

    setIsLoadingIntegrations(true);
    setError(null);

    try {
      const items = await api.listIntegrations({ signal: controller.signal });
      if (!isMountedRef.current || version !== integrationRequestVersionRef.current) {
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
      if (!isMountedRef.current || version !== integrationRequestVersionRef.current || controller.signal.aborted) {
        return;
      }

      setIntegrations([]);
      setSelectedIntegrationId(null);
      setError(handleFailure(failure, "Unable to load WhatsApp integrations."));
    } finally {
      if (isMountedRef.current && version === integrationRequestVersionRef.current) {
        setIsLoadingIntegrations(false);
      }

      if (integrationsAbortRef.current === controller) {
        integrationsAbortRef.current = null;
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
    templatesAbortRef.current?.abort();
    const controller = new AbortController();
    templatesAbortRef.current = controller;

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
      }, { signal: controller.signal });

      if (!isMountedRef.current || version !== templatesRequestVersionRef.current) {
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
      if (!isMountedRef.current || version !== templatesRequestVersionRef.current || controller.signal.aborted) {
        return;
      }

      setTemplatesResponse(null);
      setSelectedTemplate(null);
      setTemplatesError(handleFailure(failure, "Unable to load WhatsApp templates."));
    } finally {
      if (isMountedRef.current && version === templatesRequestVersionRef.current) {
        setIsLoadingTemplates(false);
      }

      if (templatesAbortRef.current === controller) {
        templatesAbortRef.current = null;
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
    detailAbortRef.current?.abort();
    const controller = new AbortController();
    detailAbortRef.current = controller;

    setIsLoadingTemplateDetail(true);
    setActionMessage(null);

    try {
      const detail = await api.getTemplate(selectedIntegrationId, templateId, { signal: controller.signal });
      if (!isMountedRef.current || version !== detailRequestVersionRef.current) {
        return;
      }

      setSelectedTemplate(detail);
    } catch (failure) {
      if (!isMountedRef.current || version !== detailRequestVersionRef.current || controller.signal.aborted) {
        return;
      }

      setActionMessage(handleFailure(failure, "Unable to load template preview."));
    } finally {
      if (isMountedRef.current && version === detailRequestVersionRef.current) {
        setIsLoadingTemplateDetail(false);
      }

      if (detailAbortRef.current === controller) {
        detailAbortRef.current = null;
      }
    }
  }, [accessToken, api, handleFailure, selectedIntegrationId]);

  const checkHealth = useCallback(async () => {
    if (!selectedIntegrationId || !accessToken) {
      return;
    }

    setIsCheckingHealth(true);
    setActionMessage(null);
    healthAbortRef.current?.abort();
    const controller = new AbortController();
    healthAbortRef.current = controller;

    try {
      const result = await api.checkIntegrationHealth(selectedIntegrationId, { signal: controller.signal });
      if (!isMountedRef.current || controller.signal.aborted) {
        return;
      }

      setHealth(result);
      setActionMessage("Health check completed.");
      await loadIntegrations();
    } catch (failure) {
      if (!isMountedRef.current || controller.signal.aborted) {
        return;
      }

      setActionMessage(handleFailure(failure, "Unable to check integration health."));
    } finally {
      if (isMountedRef.current) {
        setIsCheckingHealth(false);
      }

      if (healthAbortRef.current === controller) {
        healthAbortRef.current = null;
      }
    }
  }, [accessToken, api, handleFailure, loadIntegrations, selectedIntegrationId]);

  const syncTemplates = useCallback(async () => {
    if (!selectedIntegrationId || !accessToken) {
      return;
    }

    setIsSyncingTemplates(true);
    setActionMessage(null);
    syncAbortRef.current?.abort();
    const controller = new AbortController();
    syncAbortRef.current = controller;

    try {
      const result = await api.syncTemplates(selectedIntegrationId, { signal: controller.signal });
      if (!isMountedRef.current || controller.signal.aborted) {
        return;
      }

      setSyncResult(result);
      setActionMessage("Template synchronization completed.");
      await Promise.all([loadIntegrations(), loadTemplates()]);
    } catch (failure) {
      if (!isMountedRef.current || controller.signal.aborted) {
        return;
      }

      setActionMessage(handleFailure(failure, "Unable to synchronize templates."));
    } finally {
      if (isMountedRef.current) {
        setIsSyncingTemplates(false);
      }

      if (syncAbortRef.current === controller) {
        syncAbortRef.current = null;
      }
    }
  }, [accessToken, api, handleFailure, loadIntegrations, loadTemplates, selectedIntegrationId]);

  const getIntegrationConfiguration = useCallback(async (integrationId: string) => {
    if (!accessToken) {
      return null;
    }

    setActionMessage(null);
    try {
      return await api.getIntegration(integrationId);
    } catch (failure) {
      setActionMessage(handleFailure(failure, "Unable to load integration configuration."));
      return null;
    }
  }, [accessToken, api, handleFailure]);

  const saveIntegrationConfiguration = useCallback(async (request: WhatsAppIntegrationConfiguration, integrationId?: string) => {
    if (!accessToken) {
      return null;
    }

    setIsSavingConfiguration(true);
    setActionMessage(null);
    try {
      const integration = integrationId
        ? await api.updateIntegration(integrationId, request)
        : await api.createIntegration(request);
      setSelectedIntegrationId(integration.id);
      setActionMessage(integrationId ? "Integration configuration updated." : "Integration configuration created.");
      await loadIntegrations();
      return integration;
    } catch (failure) {
      setActionMessage(handleFailure(failure, "Unable to save integration configuration."));
      return null;
    } finally {
      if (isMountedRef.current) {
        setIsSavingConfiguration(false);
      }
    }
  }, [accessToken, api, handleFailure, loadIntegrations]);

  const setProductionEnabled = useCallback(async (enabled: boolean) => {
    if (!selectedIntegrationId || !accessToken) {
      return;
    }

    setIsChangingProduction(true);
    setActionMessage(null);
    try {
      const result = enabled
        ? await api.enableProduction(selectedIntegrationId)
        : await api.disableProduction(selectedIntegrationId);
      setActionMessage(result.message || (enabled ? "Production enabled." : "Production disabled."));
      await loadIntegrations();
    } catch (failure) {
      setActionMessage(handleFailure(failure, enabled ? "Unable to enable production." : "Unable to disable production."));
    } finally {
      if (isMountedRef.current) {
        setIsChangingProduction(false);
      }
    }
  }, [accessToken, api, handleFailure, loadIntegrations, selectedIntegrationId]);

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
    isSavingConfiguration,
    isChangingProduction,
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
    getIntegrationConfiguration,
    saveIntegrationConfiguration,
    setProductionEnabled,
    refresh
  };
}
