import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { keepPreviousData, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "./context/ThemeContext";
import { ToastProvider } from "./context/ToastContext";
import ToastContainer from "./components/Toast";
import App from "./App";
import "./index.css";

const queryClient = new QueryClient({
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
          <App />
          <ToastContainer />
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>
);
