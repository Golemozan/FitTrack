import { useEffect, useRef, type ReactNode } from "react";
import { X } from "lucide-react";

/** Full instrument sheet that hosts a detail section. */
export default function Overlay({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
      if (e.key !== "Tab" || !panelRef.current) return;
      const focusable = [...panelRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href]'
      )];
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable.at(-1)!;
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    requestAnimationFrame(() => panelRef.current?.focus());
    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", onKey);
      previous?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[var(--z-overlay)] flex items-end justify-center overflow-y-auto sm:items-center sm:p-5">
      <div className="fixed inset-0 bg-ink/85" onClick={onClose} aria-hidden="true" />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        tabIndex={-1}
        className="relative z-10 max-h-[94dvh] w-full overflow-y-auto rounded-t-[var(--radius-panel)] border border-hair bg-panel px-4 pb-5 pt-7 shadow-[var(--shadow-overlay)] outline-none sm:max-w-6xl sm:rounded-[var(--radius-panel)] sm:p-7 lg:p-8"
      >
        <div className="absolute left-1/2 top-2 h-1 w-10 -translate-x-1/2 rounded-full bg-hair sm:hidden" aria-hidden="true" />
        <button
          onClick={onClose}
          className="absolute right-3 top-3 z-20 flex h-10 w-10 items-center justify-center rounded-xl border border-hair/70 bg-card2 text-neutral-400 transition-[background-color,color] duration-150 hover:bg-panel hover:text-neutral-100 sm:right-5 sm:top-5"
          aria-label="Kapat"
        >
          <X size={20} />
        </button>
        {children}
      </div>
    </div>
  );
}
