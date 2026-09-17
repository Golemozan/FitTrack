import type { ReactNode } from "react";

/** Giriş öncesi ekranların ortak çerçevesi. */
export function AuthShell({ children, subtitle = "Kişisel sağlık günlüğün" }: { children: ReactNode; subtitle?: string }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-ink p-4 sm:p-5">
      <div className="w-full max-w-sm rounded-[var(--radius-panel)] border border-hair bg-panel p-6 shadow-[var(--shadow-overlay)] sm:p-8">
        <div className="mb-7 flex flex-col items-center text-center">
          <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-[var(--radius-card)] bg-accent text-2xl font-bold text-accentink">F</div>
          <h1 className="text-2xl font-bold text-neutral-100">FitTrack</h1>
          <p className="mt-1 text-sm text-neutral-500">{subtitle}</p>
        </div>
        {children}
      </div>
    </div>
  );
}
