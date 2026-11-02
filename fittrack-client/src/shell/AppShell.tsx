import { createContext, useCallback, useContext, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { Dumbbell, House, Sparkles, UserRound, UtensilsCrossed } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import CoachChat from "../components/CoachChat";
import CoachHistory from "../components/CoachHistory";
import ErrorBoundary from "../components/ErrorBoundary";
import Overlay from "../components/Overlay";
import { HabitEditor } from "../components/Habits";
import { AchievementCelebration, AchievementsSheet } from "../components/Achievements";
import { RestTimerProvider } from "../components/RestTimer";
import WeeklySummary from "../components/WeeklySummary";
import AccountSection from "../sections/AccountSection";
import CheckInSection from "../sections/CheckInSection";
import GoalsSection from "../sections/GoalsSection";
import WeightSection from "../sections/WeightSection";
import { useSession } from "../hooks/useSession";

/* ────────────────────────────────────────────────────────────────
   Uygulama kabuğu: 4 gerçek sayfa + her yerden açılabilen sayfalar (sheet).

   Telefonda alt menü yüzen bir haptır; seçili sekme dolu daireye dönüşür, diğerleri sessiz kalır.
   Masaüstünde aynı dört hedef sol rayda durur. Kilo, durum, hedefler, hesap ve koç ayrı sayfa
   değil — bulunduğun yerin üstüne açılır, kapatınca kaldığın yere dönersin.
   ──────────────────────────────────────────────────────────────── */

export type SheetName = "weight" | "checkin" | "goals" | "account" | "coach" | "history" | "habits" | "weekly" | "achievements";

const SheetContext = createContext<{ open: (s: SheetName) => void } | null>(null);

// eslint-disable-next-line react-refresh/only-export-components
export function useSheets() {
  const ctx = useContext(SheetContext);
  if (!ctx) throw new Error("useSheets AppShell içinde kullanılmalı");
  return ctx;
}

const NAV: Array<{ to: string; label: string; icon: LucideIcon; end?: boolean }> = [
  { to: "/", label: "Ana sayfa", icon: House, end: true },
  { to: "/beslenme", label: "Beslenme", icon: UtensilsCrossed },
  { to: "/antrenman", label: "Antrenman", icon: Dumbbell },
  { to: "/profil", label: "Profil", icon: UserRound },
];

export default function AppShell() {
  const [sheet, setSheet] = useState<SheetName | null>(null);
  const open = useCallback((s: SheetName) => setSheet(s), []);
  const close = useCallback(() => setSheet(null), []);
  const session = useSession().data;

  return (
    <SheetContext.Provider value={{ open }}>
      <RestTimerProvider>
      <div className="min-h-dvh lg:grid lg:grid-cols-[var(--rail-width)_minmax(0,1fr)]">
        <aside className="sticky top-0 hidden h-dvh flex-col px-4 py-6 lg:flex">
          <Brand />
          <nav className="mt-10 flex flex-col gap-1.5" aria-label="Ana bölümler">
            {NAV.map(({ to, label, icon: Icon, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  `flex min-h-12 items-center gap-3 rounded-full px-4 text-[15px] font-semibold transition-[background-color,color] duration-150 ${
                    isActive ? "bg-pill text-pillink" : "text-neutral-400 hover:bg-card2 hover:text-neutral-100"
                  }`
                }
              >
                <Icon size={19} strokeWidth={2.1} />
                {label}
              </NavLink>
            ))}
          </nav>
          <button
            onClick={() => open("coach")}
            className="mt-6 flex items-center gap-3 rounded-[var(--radius-card)] bg-navy p-4 text-left text-snow transition-[filter] hover:brightness-125"
          >
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent text-accentink">
              <Sparkles size={18} />
            </span>
            <span className="min-w-0">
              <span className="block text-sm font-semibold">Koçuna sor</span>
              <span className="block truncate text-xs text-snow/60">Verine göre öneri al</span>
            </span>
          </button>
          <p className="mt-auto truncate px-2 text-xs text-neutral-500">{session?.email}</p>
        </aside>

        <main
          className="mx-auto w-full pb-[calc(var(--nav-h)+env(safe-area-inset-bottom)+2rem)] lg:pb-12"
          style={{ maxWidth: "var(--content-max)", paddingInline: "var(--space-page-x)", paddingTop: "var(--space-page-y)" }}
        >
          <ErrorBoundary label="Sayfa">
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>

      <nav
        aria-label="Ana bölümler"
        className="fixed inset-x-0 z-[var(--z-nav)] mx-auto flex w-[min(22rem,calc(100%-2rem))] items-center justify-between rounded-full border border-hair bg-panel p-2 shadow-float lg:hidden"
        style={{ bottom: "calc(env(safe-area-inset-bottom) + 0.75rem)" }}
      >
        {NAV.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            aria-label={label}
            title={label}
            className={({ isActive }) =>
              `flex h-12 min-w-12 items-center justify-center gap-2 rounded-full px-3.5 transition-[background-color,color] duration-200 ${
                isActive ? "bg-pill text-pillink" : "text-neutral-400 active:bg-card2"
              }`
            }
          >
            {({ isActive }) => (
              <>
                <Icon size={21} strokeWidth={2.1} />
                {isActive && <span className="text-[13px] font-semibold">{label}</span>}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <Overlay open={sheet === "weight"} onClose={close}><ErrorBoundary label="Kilo"><WeightSection /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "checkin"} onClose={close}><ErrorBoundary label="Durum"><CheckInSection /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "goals"} onClose={close}><ErrorBoundary label="Hedefler"><GoalsSection /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "account"} onClose={close}><ErrorBoundary label="Hesap"><AccountSection /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "history"} onClose={close}><ErrorBoundary label="Koç geçmişi"><CoachHistory onClose={close} /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "habits"} onClose={close}><ErrorBoundary label="Alışkanlıklar"><HabitEditor /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "weekly"} onClose={close}><ErrorBoundary label="Haftalık özet"><WeeklySummary /></ErrorBoundary></Overlay>
      <Overlay open={sheet === "achievements"} onClose={close}><ErrorBoundary label="Başarımlar"><AchievementsSheet /></ErrorBoundary></Overlay>
      <ErrorBoundary label="Kutlama"><AchievementCelebration /></ErrorBoundary>
      <Overlay open={sheet === "coach"} onClose={close}>
        <ErrorBoundary label="Koç">
          <CoachSheet aiEnabled={session?.aiEnabled ?? false} onAddKey={() => setSheet("account")} onHistory={() => setSheet("history")} />
        </ErrorBoundary>
      </Overlay>
      </RestTimerProvider>
    </SheetContext.Provider>
  );
}

function CoachSheet({ aiEnabled, onAddKey, onHistory }: { aiEnabled: boolean; onAddKey: () => void; onHistory: () => void }) {
  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3 pr-12">
        <h2 className="font-display text-2xl font-semibold">Koç</h2>
        {aiEnabled && (
          <button onClick={onHistory} className="min-h-10 rounded-full px-3 text-sm font-semibold text-neutral-400 hover:bg-card2 hover:text-neutral-100">
            Geçmiş
          </button>
        )}
      </div>
      {aiEnabled ? (
        <div className="h-[min(70dvh,34rem)]"><CoachChat onKeyProblem={onAddKey} /></div>
      ) : (
        <div className="rounded-[var(--radius-card)] bg-card2 p-5">
          <p className="font-semibold">Koç kapalı</p>
          <p className="mt-1 text-sm leading-6 text-neutral-400">
            AI koçu kullanmak için kendi Anthropic API anahtarını ekle. Anahtar şifreli saklanır ve geri gösterilmez.
          </p>
          <button onClick={onAddKey} className="mt-4 min-h-11 rounded-full bg-accent px-5 text-sm font-semibold text-accentink">
            Anahtar ekle
          </button>
        </div>
      )}
    </div>
  );
}

function Brand() {
  return (
    <div className="flex items-center gap-3 px-2">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-accent font-display text-base font-bold text-accentink">F</div>
      <span className="font-display text-lg font-semibold">FitTrack</span>
    </div>
  );
}
