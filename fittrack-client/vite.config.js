import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
// /api isteklerini backend'e aynı origin'den geçir: oturum çerezi SameSite=Strict ve
// CORS kapalı olduğu için arayüz ile API tarayıcı gözünde aynı adreste olmalı.
// 127.0.0.1 (localhost değil): Windows'ta localhost önce IPv6'ya gider, Kestrel IPv4 dinler.
export default defineConfig({
    plugins: [react()],
    server: {
        proxy: {
            "/api": { target: "http://127.0.0.1:5000", changeOrigin: false },
        },
    },
});
