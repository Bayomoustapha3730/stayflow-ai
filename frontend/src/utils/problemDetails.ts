export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  correlationId?: string;
  errorCode?: string;
}

export function parseProblemDetails(payload: unknown): ProblemDetails | null {
  if (!payload || typeof payload !== "object") {
    return null;
  }

  const value = payload as Record<string, unknown>;
  const status = typeof value.status === "number" ? value.status : undefined;
  const title = typeof value.title === "string" ? value.title : undefined;
  const detail = typeof value.detail === "string" ? value.detail : undefined;
  const traceId = typeof value.traceId === "string" ? value.traceId : undefined;
  const correlationId = typeof value.correlationId === "string" ? value.correlationId : undefined;
  const type = typeof value.type === "string" ? value.type : undefined;
  const instance = typeof value.instance === "string" ? value.instance : undefined;
  const errorCode = typeof value.errorCode === "string" ? value.errorCode : undefined;

  if (!status && !title && !detail) {
    return null;
  }

  return {
    status,
    title,
    detail,
    traceId,
    correlationId,
    type,
    instance,
    errorCode
  };
}
