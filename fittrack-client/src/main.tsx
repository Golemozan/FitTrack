import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { keepPreviousData, MutationCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "./context/ThemeContext";
import { ToastProvider } from "./context/ToastContext";
import ToastContainer from "./components/Toast";
import SessionGate from "./components/SessionGate";
import ErrorBoundary from "./components/ErrorBoundary";
import App from "./App";
import "./index.css";

const queryClient = new QueryClient({
  // Herhangi bir kayıt başarıyla değişince başarımlar yeniden hesaplansın — yeni kademe anında kutlanır.
  // Her bileşene tek tek eklemek yerine tek kural; "görüldü" isteği kendini tetiklemesin diye hariç.
  mutationCache: new MutationCache({
    onSuccess: (_data, _vars, _ctx, mutation) => {
      if (mutation.options.mutationKey?.[0] !== "achievements")
        void queryClient.invalidateQueries({ queryKey: ["achievements"] });
    },
  }),
  defaultOptions: {
    queries: {
      retry: 0, // fail fast — no long hang when the API is unreachable
      refetchOnWindowFocus: false,
      // Serve cache instantly on navigation; don't re-fetch/flash skeletons on remount.
      staleTime: 5 * 60_000,
      gcTime: 30 * 60_000,
      refetchOnMount: false,
      // Keep showing previous data while a background refresh runs (no blank flash).
      placeholderData: keepPreviousData,
    },
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <ToastProvider>
          {/* Son çare: kart sınırlarının yakalayamadığı bir hata (provider, SessionGate,
              kabuk) yine de siyah ekran yerine okunabilir bir kart göstersin. */}
          <ErrorBoundary label="FitTrack" className="m-4">
            <SessionGate>
              <App />
            </SessionGate>
          </ErrorBoundary>
          <ToastContainer />
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
);
