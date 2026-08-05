export function getGenericHttpErrorMessage(status: number): string {
  if (status === 400) {
    return "The request was invalid.";
  }

  if (status === 401) {
    return "Your session has expired.";
  }

  if (status === 403) {
    return "You do not have permission to perform this action.";
  }

  if (status === 404) {
    return "The requested resource could not be found.";
  }

  if (status === 409) {
    return "The request conflicts with the current state.";
  }

  if (status === 422) {
    return "The request could not be processed.";
  }

  if (status === 429) {
    return "Too many requests. Please try again shortly.";
  }

  if (status === 503) {
    return "A required dependency is temporarily unavailable. Please try again shortly.";
  }

  if (status >= 500) {
    return "The server encountered an unexpected error.";
  }

  return "Request failed.";
}

export function isGenericHttpErrorMessage(message: string | null | undefined, status: number): boolean {
  return message === getGenericHttpErrorMessage(status);
}

export function extractSafeBackendErrorMessage(message: string | undefined, errors: string[] | undefined): string | null {
  const candidates = [message, ...(errors ?? [])];

  for (const candidate of candidates) {
    const safeMessage = sanitizeErrorMessage(candidate);
    if (safeMessage) {
      return safeMessage;
    }
  }

  return null;
}

function sanitizeErrorMessage(value: string | undefined): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const template = document.createElement("template");
  template.innerHTML = trimmed;
  template.content.querySelectorAll("script, style, noscript").forEach((element) => element.remove());

  const text = template.content.textContent?.replace(/\s+/g, " ").trim() ?? "";
  return text.length > 0 ? text : null;
}
