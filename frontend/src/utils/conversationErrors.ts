import { ApiError } from "../api/httpClient";
import { getGenericHttpErrorMessage } from "./httpErrorMessages";

const loadFallback = "Unable to load this conversation.";
const actionFallback = "The action could not be completed.";

export function getConversationLoadErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return "This conversation is no longer available.";
    }

    if (error.message === getGenericHttpErrorMessage(error.status)) {
      return loadFallback;
    }
  }

  return loadFallback;
}

export function getConversationActionErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404) {
      return "This conversation is no longer available.";
    }

    if (error.message === getGenericHttpErrorMessage(error.status)) {
      return actionFallback;
    }
  }

  return actionFallback;
}
