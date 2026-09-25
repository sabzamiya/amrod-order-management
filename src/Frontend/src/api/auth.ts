import { apiRequest, setAccessToken } from "./client";

interface TokenResponse {
  accessToken: string;
  expiresAt: string;
}

export async function authenticateForDevelopment() {
  const result = await apiRequest<TokenResponse>("/auth/token", {
    method: "POST",
    body: JSON.stringify({
      permission: "Orders.Admin",
    }),
  });

  setAccessToken(result.accessToken);

  return result;
}