import axios from "axios";
import api, { UNAUTHORIZED_EVENT } from "./axios";

// Oturum HttpOnly çerezde durur — JavaScript onu hiç görmez, localStorage'da parola/anahtar tutulmaz.
// Sunucu tarafı: AuthController + AccountController.

export { UNAUTHORIZED_EVENT };

export interface Session {
  id: string;
  email: string;
  displayName: string;
  aiEnabled: boolean;
  aiKeyStorageAvailable: boolean;
}

export interface ApiKeyStatus {
  hasKey: boolean;
  hint: string | null;
  createdAt: string | null;
  lastUsedAt: string | null;
  storageAvailable: boolean;
}

export interface TelegramStatus {
  botEnabled: boolean;
  linked: boolean;
}

export const authApi = {
  /** Oturum yoksa null döner, hata fırlatmaz. */
  me: async (): Promise<Session | null> => {
    try {
      return (await api.get<Session>("/auth/me")).data;
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 401) return null;
      throw err;
    }
  },
  login: (email: string, password: string) =>
    api.post<Session>("/auth/login", { email, password }).then((r) => r.data),
  register: (email: string, password: string, displayName: string) =>
    api.post<Session>("/auth/register", { email, password, displayName }).then((r) => r.data),
  logout: () => api.post("/auth/logout").then(() => undefined),
};

export const accountApi = {
  aiKey: () => api.get<ApiKeyStatus>("/account/ai-key").then((r) => r.data),
  // Anahtar doğrulaması Anthropic'e gider; 8 sn'lik varsayılan zaman aşımı dar.
  saveAiKey: (key: string) =>
    api.put<ApiKeyStatus>("/account/ai-key", { key }, { timeout: 25000 }).then((r) => r.data),
  deleteAiKey: () => api.delete("/account/ai-key").then(() => undefined),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post("/account/password", { currentPassword, newPassword }).then(() => undefined),
  logoutAll: () => api.post("/account/logout-all").then(() => undefined),
  deleteAccount: (password: string) => api.post("/account/delete", { password }).then(() => undefined),
  telegram: () => api.get<TelegramStatus>("/account/telegram").then((r) => r.data),
  telegramCode: () =>
    api.post<{ code: string; expiresAt: string }>("/account/telegram/link-code").then((r) => r.data),
  unlinkTelegram: () => api.delete("/account/telegram").then(() => undefined),
  legacy: () => api.get<{ available: boolean }>("/account/legacy").then((r) => r.data),
  claimLegacy: (legacyPassword: string) =>
    api.post("/account/legacy/claim", { legacyPassword }).then(() => undefined),
};

/** Sunucunun `{ error }` gövdesini okunur mesaja çevirir. */
export function apiError(err: unknown, fallback = "Bir şeyler ters gitti."): string {
  if (axios.isAxiosError(err)) {
    const msg = (err.response?.data as { error?: unknown } | undefined)?.error;
    if (typeof msg === "string" && msg) return msg;
    if (!err.response) return "Sunucuya ulaşılamadı.";
  }
  return fallback;
}

export function apiErrorCode(err: unknown): string | null {
  if (!axios.isAxiosError(err)) return null;
  const code = (err.response?.data as { code?: unknown } | undefined)?.code;
  return typeof code === "string" ? code : null;
}
