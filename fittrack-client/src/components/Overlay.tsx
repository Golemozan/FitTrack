import { useEffect, type ReactNode } from "react";
import { X } from "lucide-react";

/** Center modal that hosts a full detail section, opened from a bento tile. */
export default function Overlay({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto p-3 sm:p-6">
      <div className="fixed inset-0 bg-ink/80 backdrop-blur-sm" onClick={onClose} />
      <div className="relative z-10 my-auto w-full max-w-4xl rounded-3xl border border-hair bg-panel/95 p-5 shadow-2xl backdrop-blur-xl sm:p-7">
        <button
          onClick={onClose}
          className="absolute right-4 top-4 z-20 flex h-9 w-9 items-center justify-center rounded-xl bg-white/[0.06] text-neutral-300 transition-colors hover:bg-white/10 hover:text-white"
          aria-label="Kapat"
        >
          <X size={20} />
        </button>
        {children}
      </div>
    </div>
  );
}
