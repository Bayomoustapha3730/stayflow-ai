import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { createPropertyKnowledgeApi } from "../api/propertyKnowledgeApi";
import {
  getPropertyKnowledgeDetailErrorMessage,
  getPropertyKnowledgeListErrorMessage
} from "../utils/propertyKnowledgeErrors";
import type {
  CreatePropertyKnowledgeRequest,
  PropertyKnowledgeCategory,
  PropertyKnowledgeDetail,
  PropertyKnowledgeListQuery,
  PropertyKnowledgePagedResult,
  PropertyKnowledgeSummary,
  UpdatePropertyKnowledgeRequest
} from "../models/propertyKnowledge";

type ApprovalFilter = "all" | "approved" | "unapproved";
type ActiveFilter = "all" | "active" | "inactive";

interface UsePropertyKnowledgeOptions {
  propertyId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
}

export interface UsePropertyKnowledgeResult {
  propertyName: string | null;
  response: PropertyKnowledgePagedResult<PropertyKnowledgeSummary> | null;
  selectedKnowledge: PropertyKnowledgeDetail | null;
  selectedKnowledgeId: string | null;
  isLoading: boolean;
  isRefreshing: boolean;
  isLoadingKnowledge: boolean;
  error: string | null;
  selectedKnowledgeError: string | null;
  search: string;
  category?: PropertyKnowledgeCategory;
  approvalFilter: ApprovalFilter;
  activeFilter: ActiveFilter;
  page: number;
  pageSize: number;
  isCreating: boolean;
  isUpdating: boolean;
  isApproving: boolean;
  isActivating: boolean;
  isDeleting: boolean;
  setSearch: (value: string) => void;
  setCategory: (value?: PropertyKnowledgeCategory) => void;
  setApprovalFilter: (value: ApprovalFilter) => void;
  setActiveFilter: (value: ActiveFilter) => void;
  setPage: (value: number) => void;
  setPageSize: (value: number) => void;
  refresh: () => void;
  retry: () => void;
  clearError: () => void;
  clearSelectedKnowledge: () => void;
  selectKnowledge: (knowledgeId: string) => Promise<void>;
  createKnowledge: (request: CreatePropertyKnowledgeRequest) => Promise<PropertyKnowledgeDetail>;
  updateKnowledge: (knowledgeId: string, request: UpdatePropertyKnowledgeRequest) => Promise<PropertyKnowledgeDetail>;
  approveKnowledge: (knowledgeId: string) => Promise<PropertyKnowledgeDetail>;
  unapproveKnowledge: (knowledgeId: string) => Promise<PropertyKnowledgeDetail>;
  activateKnowledge: (knowledgeId: string) => Promise<PropertyKnowledgeDetail>;
  deactivateKnowledge: (knowledgeId: string) => Promise<PropertyKnowledgeDetail>;
  deleteKnowledge: (knowledgeId: string) => Promise<void>;
}

const defaultPageSize = 10;

export function usePropertyKnowledge({ propertyId, accessToken, onUnauthorized }: UsePropertyKnowledgeOptions): UsePropertyKnowledgeResult {
  const [response, setResponse] = useState<PropertyKnowledgePagedResult<PropertyKnowledgeSummary> | null>(null);
  const [propertyName, setPropertyName] = useState<string | null>(null);
  const [selectedKnowledge, setSelectedKnowledge] = useState<PropertyKnowledgeDetail | null>(null);
  const [selectedKnowledgeId, setSelectedKnowledgeId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isLoadingKnowledge, setIsLoadingKnowledge] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedKnowledgeError, setSelectedKnowledgeError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [category, setCategory] = useState<PropertyKnowledgeCategory | undefined>(undefined);
  const [approvalFilter, setApprovalFilter] = useState<ApprovalFilter>("all");
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>("all");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [refreshTick, setRefreshTick] = useState(0);
  const [isCreating, setIsCreating] = useState(false);
  const [isUpdating, setIsUpdating] = useState(false);
  const [isApproving, setIsApproving] = useState(false);
  const [isActivating, setIsActivating] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const listRequestVersionRef = useRef(0);
  const detailRequestVersionRef = useRef(0);
  const listAbortRef = useRef<AbortController | null>(null);
  const detailAbortRef = useRef<AbortController | null>(null);

  const http = useMemo(
    () => new HttpClient({ baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243", getAccessToken: () => accessToken }),
    [accessToken]
  );

  const api = useMemo(() => createPropertyKnowledgeApi(http), [http]);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, category, approvalFilter, activeFilter, pageSize]);

  const loadKnowledgeList = useCallback(async () => {
    if (!propertyId || !accessToken) {
      listRequestVersionRef.current += 1;
      listAbortRef.current?.abort();
      setResponse(null);
      setPropertyName(null);
      setSelectedKnowledge(null);
      setSelectedKnowledgeId(null);
      setSelectedKnowledgeError(null);
      setError(null);
      setIsLoading(false);
      setIsRefreshing(false);
      return;
    }

    const version = ++listRequestVersionRef.current;
    listAbortRef.current?.abort();
    const controller = new AbortController();
    listAbortRef.current = controller;

    if (response === null) {
      setIsLoading(true);
    } else {
      setIsRefreshing(true);
    }
    setError(null);

    try {
      const nextResponse = await api.listKnowledge(propertyId, {
        search: debouncedSearch || undefined,
        category,
        isApproved: approvalFilter === "all" ? undefined : approvalFilter === "approved",
        isActive: activeFilter === "all" ? undefined : activeFilter === "active",
        pageNumber: page,
        pageSize
      }, { signal: controller.signal });

      if (version !== listRequestVersionRef.current) {
        return;
      }

      setResponse(nextResponse);
      setPropertyName(nextResponse.items[0]?.propertyName ?? null);

      setSelectedKnowledgeId((current) => {
        if (!current) {
          return current;
        }

        return nextResponse.items.some((item) => item.id === current) ? current : null;
      });

      setSelectedKnowledge((current) => {
        if (!current) {
          return current;
        }

        return nextResponse.items.some((item) => item.id === current.id) ? current : null;
      });
    } catch (failure) {
      if (version !== listRequestVersionRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setResponse(null);
      setSelectedKnowledge(null);
      setSelectedKnowledgeId(null);
      setError(getPropertyKnowledgeListErrorMessage(failure));
    } finally {
      if (version === listRequestVersionRef.current) {
        setIsLoading(false);
        setIsRefreshing(false);
      }
    }
  }, [accessToken, activeFilter, approvalFilter, api, category, debouncedSearch, onUnauthorized, page, pageSize, propertyId, response]);

  useEffect(() => {
    void loadKnowledgeList();

    return () => {
      listAbortRef.current?.abort();
    };
  }, [loadKnowledgeList, refreshTick]);

  const loadKnowledgeDetail = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      detailRequestVersionRef.current += 1;
      detailAbortRef.current?.abort();
      setSelectedKnowledge(null);
      setSelectedKnowledgeId(null);
      setSelectedKnowledgeError(null);
      setIsLoadingKnowledge(false);
      return;
    }

    const version = ++detailRequestVersionRef.current;
    detailAbortRef.current?.abort();
    const controller = new AbortController();
    detailAbortRef.current = controller;

    setIsLoadingKnowledge(true);
    setSelectedKnowledgeError(null);

    try {
      const next = await api.getKnowledgeItem(propertyId, knowledgeId, { signal: controller.signal });
      if (version !== detailRequestVersionRef.current) {
        return;
      }

      setSelectedKnowledgeId(knowledgeId);
      setSelectedKnowledge(next);
    } catch (failure) {
      if (version !== detailRequestVersionRef.current) {
        return;
      }

      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      setSelectedKnowledge(null);
      setSelectedKnowledgeId(null);
      setSelectedKnowledgeError(getPropertyKnowledgeDetailErrorMessage(failure));
    } finally {
      if (version === detailRequestVersionRef.current) {
        setIsLoadingKnowledge(false);
      }
    }
  }, [accessToken, api, onUnauthorized, propertyId]);

  const runMutation = useCallback(async <T,>(setBusy: (value: boolean) => void, operation: () => Promise<T>): Promise<T> => {
    setBusy(true);
    try {
      return await operation();
    } finally {
      setBusy(false);
    }
  }, []);

  const createKnowledge = useCallback(async (request: CreatePropertyKnowledgeRequest) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const created = await runMutation(setIsCreating, () => api.createKnowledge(propertyId, request));
      setSelectedKnowledgeId(created.id);
      setSelectedKnowledge(created);
      setPropertyName(created.propertyName || propertyName);
      setRefreshTick((current) => current + 1);
      return created;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, propertyName, runMutation]);

  const updateKnowledge = useCallback(async (knowledgeId: string, request: UpdatePropertyKnowledgeRequest) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const updated = await runMutation(setIsUpdating, () => api.updateKnowledge(propertyId, knowledgeId, request));
      setSelectedKnowledgeId(updated.id);
      setSelectedKnowledge(updated);
      setPropertyName(updated.propertyName || propertyName);
      setRefreshTick((current) => current + 1);
      return updated;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, propertyName, runMutation]);

  const approveKnowledge = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const updated = await runMutation(setIsApproving, () => api.approveKnowledge(propertyId, knowledgeId));
      setSelectedKnowledge((current) => current?.id === knowledgeId ? updated : current);
      setRefreshTick((current) => current + 1);
      return updated;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, runMutation]);

  const unapproveKnowledge = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const updated = await runMutation(setIsApproving, () => api.unapproveKnowledge(propertyId, knowledgeId));
      setSelectedKnowledge((current) => current?.id === knowledgeId ? updated : current);
      setRefreshTick((current) => current + 1);
      return updated;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, runMutation]);

  const activateKnowledge = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const updated = await runMutation(setIsActivating, () => api.activateKnowledge(propertyId, knowledgeId));
      setSelectedKnowledge((current) => current?.id === knowledgeId ? updated : current);
      setRefreshTick((current) => current + 1);
      return updated;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, runMutation]);

  const deactivateKnowledge = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      const updated = await runMutation(setIsActivating, () => api.deactivateKnowledge(propertyId, knowledgeId));
      setSelectedKnowledge((current) => current?.id === knowledgeId ? updated : current);
      setRefreshTick((current) => current + 1);
      return updated;
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, runMutation]);

  const deleteKnowledge = useCallback(async (knowledgeId: string) => {
    if (!propertyId || !accessToken) {
      throw new Error("Authenticated tenant context is required.");
    }

    try {
      await runMutation(setIsDeleting, () => api.deleteKnowledge(propertyId, knowledgeId));
      if (selectedKnowledgeId === knowledgeId) {
        setSelectedKnowledge(null);
        setSelectedKnowledgeId(null);
      }
      setRefreshTick((current) => current + 1);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
      }

      throw failure;
    }
  }, [accessToken, api, onUnauthorized, propertyId, runMutation, selectedKnowledgeId]);

  const retry = useCallback(() => {
    setRefreshTick((current) => current + 1);
  }, []);

  const refresh = useCallback(() => {
    setRefreshTick((current) => current + 1);
  }, []);

  const clearError = useCallback(() => setError(null), []);
  const clearSelectedKnowledge = useCallback(() => {
    setSelectedKnowledgeId(null);
    setSelectedKnowledge(null);
    setSelectedKnowledgeError(null);
  }, []);

  return {
    propertyName,
    response,
    selectedKnowledge,
    selectedKnowledgeId,
    isLoading,
    isRefreshing,
    isLoadingKnowledge,
    error,
    selectedKnowledgeError,
    search,
    category,
    approvalFilter,
    activeFilter,
    page,
    pageSize,
    isCreating,
    isUpdating,
    isApproving,
    isActivating,
    isDeleting,
    setSearch,
    setCategory,
    setApprovalFilter,
    setActiveFilter,
    setPage,
    setPageSize,
    refresh,
    retry,
    clearError,
    clearSelectedKnowledge,
    selectKnowledge: loadKnowledgeDetail,
    createKnowledge,
    updateKnowledge,
    approveKnowledge,
    unapproveKnowledge,
    activateKnowledge,
    deactivateKnowledge,
    deleteKnowledge
  };
}
