import axios from "axios";

// Use 127.0.0.1 (not "localhost"): Kestrel binds IPv4 (0.0.0.0), while "localhost"
// resolves to IPv6 ::1 first on Windows → each request pays an IPv6 connect timeout
// before falling back to IPv4. 127.0.0.1 avoids that per-request delay.
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://127.0.0.1:5000/api",
  headers: { "Content-Type": "application/json" },
  timeout: 8000,
});

export default api;
