import axios from "axios";

/** Oturum düştüğünde (401) yayınlanır — SessionGate bunu dinleyip giriş ekranına döner. */
export const UNAUTHORIZED_EVENT = "fittrack:unauthorized";

// Arayüz ve API aynı origin'den çalışır: üretimde tek servis, yerelde Vite proxy'si
// (bkz. vite.config.ts). Böylece oturum çerezi SameSite=Strict kalabilir ve CORS gerekmez.
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "/api",
  headers: {
    "Content-Type": "application/json",
    // CSRF kalkanı: sunucu durum değiştiren her isteğin bu başlığı taşımasını ister.
    "X-Requested-With": "FitTrack",
  },
  withCredentials: true,
  timeout: 8000,
});

// 204 No Content → `null`. ASP.NET Core'da `Ok(null)` gövdesiz 204 döner; axios bunu
// `data: ""` yapar. Boş string null olmadığı için `current?.exercises` gibi optional
// chain'ler tökezlemez, `"".exercises` undefined olur ve bir sonraki erişim patlar.
// Sözleşme `T | null` diyorsa runtime da null vermeli — tek yerden normalize ediyoruz.
api.interceptors.response.use((response) => {
  if (response.status === 204 || response.data === "") response.data = null;
  return response;
});

// Oturum düştüyse (süre doldu, parola başka cihazda değişti, hesap silindi) giriş ekranına dön.
// Giriş/kayıt/me uçlarının 401'i "oturum yok" demek, olay değil — onları kendi çağıranı ele alır.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const url = axios.isAxiosError(error) ? (error.config?.url ?? "") : "";
    if (axios.isAxiosError(error) && error.response?.status === 401 && !url.startsWith("/auth/")) {
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    return Promise.reject(error);
  }
);

export default api;
