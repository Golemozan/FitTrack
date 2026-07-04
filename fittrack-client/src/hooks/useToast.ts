import { useContext, useMemo } from "react";
import { ToastContext } from "../context/ToastContext";

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within a ToastProvider");
  return useMemo(
    () => ({
      success: (message: string) => ctx.push("success", message),
      error: (message: string) => ctx.push("error", message),
    }),
    [ctx]
  );
}
