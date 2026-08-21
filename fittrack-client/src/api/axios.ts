import axios from "axios";
import { clearApiKey, getApiKey, UNAUTHORIZED_EVENT } from "./auth";

// Use 127.0.0.1 (not "localhost"): Kestrel binds IPv4 (0.0.0.0), while "localhost"
// resolves to IPv6 ::1 first on Windows → each request pays an IPv6 connect timeout
// before falling back to IPv4. 127.0.0.1 avoids that per-request delay.
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://127.0.0.1:5000/api",
  headers: { "Content-Type": "application/json" },
  timeout: 8000,
});

// Her isteğe parolayı ekle.
api.interceptors.request.use((config) => {
  const key = getApiKey();
  if (key) config.headers.set("X-Api-Key", key);
  return config;
});

// 204 No Content → `null`. ASP.NET Core'da `Ok(null)` gövdesiz 204 döner; axios bunu
// `data: ""` yapar. Boş string null olmadığı için `current?.exercises` gibi optional
// chain'ler tökezlemez, `"".exercises` undefined olur ve bir sonraki erişim patlar.
// Sözleşme `T | null` diyorsa runtime da null vermeli — tek yerden normalize ediyoruz.
api.interceptors.response.use((response) => {
  if (response.status === 204 || response.data === "") response.data = null;
  return response;
});

// Parola reddedildiyse sakladığımızı at ve kilit ekranını çağır.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      clearApiKey();
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    return Promise.reject(error);
  }
);

export default api;
