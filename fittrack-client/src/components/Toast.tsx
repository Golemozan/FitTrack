import { useContext } from "react";
import { CheckCircle2, XCircle } from "lucide-react";
import { ToastContext } from "../context/ToastContext";

export default function ToastContainer() {
  const ctx = useContext(ToastContext);
  if (!ctx) return null;

  return (
    <div className="pointer-events-none fixed inset-x-0 top-4 z-50 flex flex-col items-center gap-2 px-4">
      {ctx.toasts.map((t) => (
        <div
          key={t.id}
          onClick={() => ctx.dismiss(t.id)}
          className={`pointer-events-auto flex w-full max-w-sm items-center gap-2 rounded-xl px-4 py-3 text-sm font-medium text-white shadow-lg animate-[toast-in_0.2s_ease-out] ${
            t.type === "success" ? "bg-loss" : "bg-gain"
          }`}
        >
          {t.type === "success" ? <CheckCircle2 size={18} /> : <XCircle size={18} />}
          <span className="flex-1">{t.message}</span>
        </div>
      ))}
    </div>
  );
}
