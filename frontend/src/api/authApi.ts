import type { LoginResponse } from "../models/chat";
import type { HttpClient } from "./httpClient";

export function createAuthApi(http: HttpClient) {
  return {
    loginForDevelopment(email: string, password: string) {
      return http.post<LoginResponse>("/auth/login", { email, password });
    }
  };
}
