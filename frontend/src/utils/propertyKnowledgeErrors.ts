import { ApiError } from "../api/httpClient";
import { getGenericHttpErrorMessage } from "./httpErrorMessages";

const listFallback = "Unable to load property knowledge.";
const detailFallback = "Unable to load the selected knowledge item.";
const saveFallback = "Unable to save the knowledge item.";
const approveFallback = "Unable to approve the knowledge item.";
const activateFallback = "Unable to change the active state.";
const deleteFallback = "Unable to delete the knowledge item.";

export function getPropertyKnowledgeListErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return "Property not found.";
    }

    if (error.message === getGenericHttpErrorMessage(error.status)) {
      return listFallback;
    }
  }

  return listFallback;
}

export function getPropertyKnowledgeDetailErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return "This knowledge item is no longer available.";
    }

    if (error.message === getGenericHttpErrorMessage(error.status)) {
      return detailFallback;
    }
  }

  return detailFallback;
}

export function getPropertyKnowledgeSaveErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return saveFallback;
    }

    if (error.message !== getGenericHttpErrorMessage(error.status)) {
      return error.message;
    }
  }

  return saveFallback;
}

export function getPropertyKnowledgeActionErrorMessage(action: "approve" | "activate" | "delete", error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return "This knowledge item is no longer available.";
    }

    if (error.message !== getGenericHttpErrorMessage(error.status)) {
      return error.message;
    }
  }

  if (action === "approve") {
    return approveFallback;
  }

  if (action === "activate") {
    return activateFallback;
  }

  return deleteFallback;
}
