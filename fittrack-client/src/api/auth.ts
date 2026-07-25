// Parola tarayıcıda saklanır ve her isteğe X-Api-Key başlığı olarak eklenir.
// Sunucu tarafında karşılığı: FITTRACK_API_KEY ortam değişkeni.

const STORAGE_KEY = "fittrack.apiKey";

/** Anahtar geçersizleştiğinde yayınlanır — kilit ekranı bunu dinler. */
export const UNAUTHORIZED_EVENT = "fittrack:unauthorized";

export function getApiKey(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? "";
  } catch {
    return ""; // private mode / storage kapalı
  }
}

export function setApiKey(key: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, key);
  } catch {
    /* yoksay — oturum bellekte devam eder */
  }
}

export function clearApiKey(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* yoksay */
  }
}

/** Uygulamayı elle kilitle (paylaşılan cihazdan çıkarken). */
export function lockApp(): void {
  clearApiKey();
  window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
}
