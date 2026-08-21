import { useMemo, useState } from "react";
import {
  ChevronRight,
  Clock3,
  Dumbbell,
  HeartPulse,
  Lock,
  Scale,
  Sparkles,
  Target,
  UtensilsCrossed,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { lockApp } from "./api/auth";
import CoachChat from "./components/CoachChat";
import CoachHistory from "./components/CoachHistory";
import ErrorBoundary from "./components/ErrorBoundary";
import Overlay from "./components/Overlay";
import TodayRings from "./components/TodayRings";
import type { Ring } from "./components/TodayRings";
import { ProgressBar } from "./components/ui";
import CheckInSection from "./sections/CheckInSection";
import GoalsSection from "./sections/GoalsSection";
import NutritionSection from "./sections/NutritionSection";
import WeightSection from "./sections/WeightSection";
import WorkoutSection from "./sections/WorkoutSection";
import { useProfile, useTodayCheckins } from "./hooks/useCoach";
import { useGoals } from "./hooks/useGoals";
import { useNutritionStreak, useTodayMeals } from "./hooks/useNutrition";
import { useWeightStats } from "./hooks/useWeight";
import { useTodaySession } from "./hooks/useWorkout";
import type { MealEntry } from "./types";

type View = "nutrition" | "workout" | "weight" | "goals" | "checkin" | "history";

const MOODS = ["😩", "🙁", "😐", "🙂", "😄"];
const tabs: Array<{ label: string; view: Exclude<View, "history">; icon: LucideIcon }> = [
  { label: "Beslenme", view: "nutrition", icon: UtensilsCrossed },
  { label: "Antrenman", view: "workout", icon: Dumbbell },
  { label: "Kilo", view: "weight", icon: Scale },
  { label: "Durum", view: "checkin", icon: HeartPulse },
  { label: "Hedefler", view: "goals", icon: Target },
];

export default function App() {
  const [view, setView] = useState<View | null>(null);
  const today = new Date().toLocaleDateString("tr-TR", {
    weekday: "long",
    day: "numeric",
    month: "long",
  });

  return (
    <div className="flex min-h-dvh bg-ink text-neutral-100">
      <aside className="sticky top-0 hidden h-dvh w-[var(--rail-width)] shrink-0 flex-col border-r border-hair/70 bg-panel/55 px-4 py-5 lg:flex">
        <Brand />

        <nav className="mt-12 flex flex-col gap-1" aria-label="Ana bölümler">
          {tabs.map((item) => (
            <SideTab
              key={item.view}
              {...item}
              active={view === item.view}
              onClick={() => setView(item.view)}
            />
          ))}
        </nav>

        <div className="mt-auto border-t border-hair/70 pt-4">
          <p className="mb-3 px-3 text-xs leading-5 text-neutral-500">
            Günlük verilerin bu cihazdaki kişisel profilinde tutulur.
          </p>
          <button
            onClick={lockApp}
            className="flex min-h-11 w-full items-center gap-3 rounded-xl px-3 text-sm font-medium text-neutral-400 transition-[background-color,color] duration-150 hover:bg-card2 hover:text-neutral-100"
          >
            <Lock size={17} />
            Uygulamayı kilitle
          </button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-hair/70 bg-ink/95 px-4 lg:hidden">
          <Brand compact />
          <button
            onClick={lockApp}
            className="flex h-11 w-11 items-center justify-center rounded-xl text-neutral-400 transition-colors hover:bg-card2 hover:text-neutral-100"
            aria-label="Uygulamayı kilitle"
          >
            <Lock size={18} />
          </button>
        </header>

        <main
          className="mx-auto pb-28 lg:pb-12"
          style={{ maxWidth: "var(--content-max)", paddingInline: "var(--space-page-x)", paddingTop: "var(--space-page-y)" }}
        >
          <header className="mb-7 flex items-end justify-between gap-4 sm:mb-9">
            <div>
              <p className="mb-2 text-sm capitalize text-neutral-500">{today}</p>
              <h1 className="text-[2.5rem] font-bold leading-[0.95] tracking-[-0.045em] text-neutral-100 sm:text-[3.25rem]">
                Bugün
              </h1>
            </div>
            <p className="hidden max-w-xs text-right text-sm leading-6 text-neutral-500 md:block">
              Beslenme, antrenman ve toparlanma verilerinin günlük görünümü.
            </p>
          </header>

          <section className="dashboard-grid" aria-label="Günlük özet">
            <div className="min-w-0 md:col-span-2 xl:col-span-8">
              <ErrorBoundary label="Bugünün halkaları">
                <TodayHero onOpen={() => setView("nutrition")} />
              </ErrorBoundary>
            </div>

            <div className="grid min-w-0 gap-4 md:col-span-2 md:grid-cols-2 xl:col-span-4 xl:grid-cols-1 xl:grid-rows-2 xl:gap-[1.125rem]">
              <ErrorBoundary label="Kilo">
                <WeightCard onOpen={() => setView("weight")} />
              </ErrorBoundary>
              <ErrorBoundary label="Check-in">
                <CheckinCard onOpen={() => setView("checkin")} />
              </ErrorBoundary>
            </div>

            <div className="min-w-0 md:col-span-2 xl:col-span-7">
              <ErrorBoundary label="Antrenman">
                <WorkoutCard onOpen={() => setView("workout")} />
              </ErrorBoundary>
            </div>
            <div className="min-w-0 md:col-span-2 xl:col-span-5">
              <ErrorBoundary label="Öğün günlüğü">
                <MealsCard onOpen={() => setView("nutrition")} />
              </ErrorBoundary>
            </div>
            <div className="min-w-0 md:col-span-2 xl:col-span-12">
              <ErrorBoundary label="Hedefler">
                <GoalsCard onOpen={() => setView("goals")} />
              </ErrorBoundary>
            </div>
          </section>

          <ErrorBoundary label="Koç">
            <CoachCard onHistory={() => setView("history")} />
          </ErrorBoundary>
        </main>
      </div>

      <nav
        className="fixed inset-x-3 bottom-3 z-40 grid grid-cols-5 rounded-[20px] border border-hair bg-panel p-1.5 shadow-[var(--shadow-overlay)] lg:hidden"
        aria-label="Ana bölümler"
      >
        {tabs.map((item) => (
          <MobileTab
            key={item.view}
            {...item}
            active={view === item.view}
            onClick={() => setView(item.view)}
          />
        ))}
      </nav>

      <Overlay open={view === "nutrition"} onClose={() => setView(null)}><ErrorBoundary label="Beslenme"><NutritionSection /></ErrorBoundary></Overlay>
      <Overlay open={view === "workout"} onClose={() => setView(null)}><ErrorBoundary label="Antrenman"><WorkoutSection /></ErrorBoundary></Overlay>
      <Overlay open={view === "weight"} onClose={() => setView(null)}><ErrorBoundary label="Kilo"><WeightSection /></ErrorBoundary></Overlay>
      <Overlay open={view === "checkin"} onClose={() => setView(null)}><ErrorBoundary label="Check-in"><CheckInSection /></ErrorBoundary></Overlay>
      <Overlay open={view === "goals"} onClose={() => setView(null)}><ErrorBoundary label="Hedefler"><GoalsSection /></ErrorBoundary></Overlay>
      <Overlay open={view === "history"} onClose={() => setView(null)}><ErrorBoundary label="Geçmiş"><CoachHistory onClose={() => setView(null)} /></ErrorBoundary></Overlay>
    </div>
  );
}

function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <div className={`flex items-center ${compact ? "gap-2.5" : "gap-3 px-2"}`}>
      <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-accent text-sm font-bold text-accentink">F</div>
      <div>
        <span className="block text-[17px] font-semibold tracking-tight text-neutral-100">FitTrack</span>
        {!compact && <span className="mt-0.5 block text-[11px] text-neutral-500">Günlük performans</span>}
      </div>
    </div>
  );
}

function SideTab({ label, icon: Icon, active, onClick }: { label: string; icon: LucideIcon; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      aria-current={active ? "page" : undefined}
      className={`flex min-h-11 w-full items-center gap-3 rounded-xl px-3 text-sm font-medium transition-[background-color,color] duration-150 ${active ? "bg-card2 text-neutral-100" : "text-neutral-500 hover:bg-card2/70 hover:text-neutral-200"}`}
    >
      <Icon size={18} className={active ? "text-accent" : undefined} />
      {label}
    </button>
  );
}

function MobileTab({ label, icon: Icon, active, onClick }: { label: string; icon: LucideIcon; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      aria-current={active ? "page" : undefined}
      className={`flex min-w-0 flex-col items-center justify-center gap-1 rounded-2xl px-1 py-1.5 transition-colors ${active ? "bg-card2 text-accent" : "text-neutral-500 active:bg-card2 active:text-accent"}`}
    >
      <Icon size={20} />
      <span className="max-w-full truncate text-[10px] font-medium">{label}</span>
    </button>
  );
}

type Tone = "move" | "lift" | "flow" | "lav" | "carb";
const TONE_ICON: Record<Tone, string> = {
  move: "text-move",
  lift: "text-lift",
  flow: "text-flow",
  lav: "text-lav",
  carb: "text-carb",
};

function SummaryCard({ title, icon: Icon, tone, onOpen, density = "standard", className = "", children }: {
  title: string;
  icon: LucideIcon;
  tone: Tone;
  onOpen: () => void;
  density?: "compact" | "standard" | "strip";
  className?: string;
  children: React.ReactNode;
}) {
  const padding = density === "compact" ? "p-4 sm:p-5" : density === "strip" ? "p-4 sm:px-5" : "p-5 sm:p-6";
  return (
    <button
      onClick={onOpen}
      className={`press group flex h-full min-w-0 w-full flex-col rounded-[var(--radius-card)] border border-hair/70 bg-panel text-left hover:border-hair hover:bg-card2 ${padding} ${className}`}
    >
      <div className={`${density === "strip" ? "mb-3" : "mb-5"} flex w-full items-center justify-between gap-3`}>
        <span className="flex min-w-0 items-center gap-2 text-[15px] font-semibold text-neutral-300">
          <Icon size={18} className={TONE_ICON[tone]} />
          <span className="truncate">{title}</span>
        </span>
        <ChevronRight size={18} className="shrink-0 text-neutral-600 transition-[transform,color] duration-150 group-hover:translate-x-0.5 group-hover:text-neutral-400" />
      </div>
      <div className="w-full min-w-0 flex-1">{children}</div>
    </button>
  );
}

function TodayHero({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals();
  const goals = useGoals();
  const streak = useNutritionStreak();

  const totals = useMemo(() => {
    const entries = (meals.data ? Object.values(meals.data).flat() : []) as MealEntry[];
    return entries.reduce(
      (sum, meal) => ({
        calories: sum.calories + meal.calories,
        protein: sum.protein + meal.protein,
        carbs: sum.carbs + meal.carbs,
        fat: sum.fat + meal.fat,
      }),
      { calories: 0, protein: 0, carbs: 0, fat: 0 },
    );
  }, [meals.data]);

  const rings: Ring[] = [
    { label: "Carb", value: totals.carbs, goal: goals.data?.carbGoal ?? 0, unit: "g", tone: "carb" },
    { label: "Protein", value: totals.protein, goal: goals.data?.proteinGoal ?? 0, unit: "g", tone: "protein" },
    { label: "Fat", value: totals.fat, goal: goals.data?.fatGoal ?? 0, unit: "g", tone: "fat" },
  ];

  return (
    <section className="h-full rounded-[var(--radius-panel)] border border-hair/70 bg-panel p-[var(--space-panel)]" aria-label="Bugünün beslenme dengesi">
      <header className="mb-7 flex items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold text-neutral-100 sm:text-2xl">Günün dengesi</h2>
          <p className="mt-1.5 text-sm leading-6 text-neutral-500">Makro hedeflerin ve toplam enerji alımın.</p>
        </div>
        <button
          onClick={onOpen}
          className="shrink-0 rounded-xl bg-accent px-3.5 py-2.5 text-sm font-semibold text-accentink transition-[filter,transform] duration-150 hover:brightness-110 active:translate-y-px"
        >
          Öğün ekle
        </button>
      </header>
      <TodayRings
        rings={rings}
        calories={totals.calories}
        calorieGoal={goals.data?.calorieGoal ?? 0}
        streakDays={streak.data?.days ?? 0}
      />
    </section>
  );
}

function WeightCard({ onOpen }: { onOpen: () => void }) {
  const stats = useWeightStats();
  const weight = stats.data?.currentWeight;
  const weekly = stats.data?.weeklyChange;
  return (
    <SummaryCard title="Kilo" icon={Scale} tone="flow" onOpen={onOpen} density="compact">
      <div className="flex items-baseline gap-2">
        <span className="num text-4xl text-neutral-100">{weight != null ? weight.toFixed(1) : "—"}</span>
        <span className="text-sm text-neutral-500">kg</span>
      </div>
      <p className={`mt-3 text-sm ${weekly == null || weekly === 0 ? "text-neutral-500" : weekly < 0 ? "text-loss" : "text-gain"}`}>
        {weekly == null || weekly === 0 ? "Bu hafta değişim yok" : `${weekly > 0 ? "+" : ""}${weekly.toFixed(1)} kg · son 7 gün`}
      </p>
    </SummaryCard>
  );
}

function CheckinCard({ onOpen }: { onOpen: () => void }) {
  const checkins = useTodayCheckins();
  const list = checkins.data ?? [];
  const last = list[0];
  return (
    <SummaryCard title="Günün durumu" icon={HeartPulse} tone="lav" onOpen={onOpen} density="compact">
      <div className="flex items-end justify-between gap-3">
        <span className="text-4xl" aria-label={last ? `Ruh hali ${last.mood}/5` : "Durum kaydı yok"}>{last ? MOODS[last.mood - 1] : "—"}</span>
        <span className="tnum text-sm text-neutral-500">{last ? `${last.mood}/5` : "Kayıt yok"}</span>
      </div>
      <p className="mt-3 text-sm text-neutral-500">{last ? `Bugün ${list.length} check-in` : "Kısa bir check-in yap"}</p>
    </SummaryCard>
  );
}

function WorkoutCard({ onOpen }: { onOpen: () => void }) {
  const session = useTodaySession();
  const current = session.data;
  const stats = useMemo(() => {
    const sets = current?.exercises?.flatMap((exercise) => exercise.sets ?? []) ?? [];
    return {
      done: sets.filter((set) => set.isCompleted).length,
      total: sets.length,
      volume: sets.reduce((sum, set) => sum + (set.isCompleted ? set.weightKg * set.reps : 0), 0),
    };
  }, [current]);

  return (
    <SummaryCard title="Antrenman" icon={Dumbbell} tone="lift" onOpen={onOpen}>
      <div className="grid gap-5 sm:grid-cols-[minmax(0,1fr)_minmax(12rem,.8fr)] sm:items-end">
        <div className="min-w-0">
          <div className="flex items-baseline gap-2">
            <span className="num text-4xl text-neutral-100">{Math.round(stats.volume)}</span>
            <span className="text-sm text-neutral-500">kg hacim</span>
          </div>
          <p className="mt-3 truncate text-sm text-neutral-500">
            {current && current.exercises.length > 0
              ? current.exercises.map((exercise) => exercise.name).join(" · ")
              : "Bugünün antrenmanı henüz başlamadı"}
          </p>
        </div>
        <div>
          <div className="mb-2.5 flex items-center justify-between gap-3 text-sm">
            <span className="text-neutral-500">Tamamlanan set</span>
            <span className="tnum font-medium text-lift">{stats.done} / {stats.total}</span>
          </div>
          <ProgressBar value={stats.done} max={stats.total} colorClass="bg-lift" />
        </div>
      </div>
    </SummaryCard>
  );
}

function MealsCard({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals();
  const entries = useMemo(
    () => ((meals.data ? Object.values(meals.data).flat() : []) as MealEntry[])
      .slice()
      .sort((a, b) => new Date(b.loggedAt).getTime() - new Date(a.loggedAt).getTime()),
    [meals.data],
  );
  const activeMeals = new Set(entries.map((entry) => entry.mealType)).size;
  const last = entries[0];

  return (
    <SummaryCard title="Öğün günlüğü" icon={UtensilsCrossed} tone="move" onOpen={onOpen}>
      <div className="flex items-end justify-between gap-4">
        <div>
          <span className="num text-4xl text-neutral-100">{entries.length}</span>
          <span className="ml-2 text-sm text-neutral-500">kayıt</span>
        </div>
        <span className="tnum text-sm text-neutral-500">{activeMeals} öğün</span>
      </div>
      <div className="mt-5 border-t border-hair/70 pt-4">
        <p className="text-xs text-neutral-600">Son kayıt</p>
        <p className="mt-1 truncate text-sm text-neutral-300">{last?.foodName ?? "Henüz öğün eklenmedi"}</p>
      </div>
    </SummaryCard>
  );
}

function GoalsCard({ onOpen }: { onOpen: () => void }) {
  const profile = useProfile();
  const stats = useWeightStats();
  const height = profile.data?.heightCm ?? null;
  const weight = stats.data?.currentWeight ?? null;
  const bmi = height && weight ? weight / (height / 100) ** 2 : null;
  return (
    <SummaryCard title="Hedefler" icon={Target} tone="carb" onOpen={onOpen} density="strip">
      <div className="flex flex-wrap items-end justify-between gap-x-8 gap-y-2">
        <div className="flex items-baseline gap-2">
          <span className="num text-3xl text-neutral-100">{bmi != null ? bmi.toFixed(1) : "—"}</span>
          <span className="text-sm text-neutral-500">BMI</span>
        </div>
        <p className="text-sm text-neutral-500">{bmi != null ? "Profil ve hedeflerini görüntüle" : "Profil bilgilerini tamamla"}</p>
      </div>
    </SummaryCard>
  );
}

function CoachCard({ onHistory }: { onHistory: () => void }) {
  return (
    <section className="mt-5 overflow-hidden rounded-[var(--radius-panel)] border border-hair/70 bg-panel" aria-label="Koç">
      <div className="flex items-center justify-between gap-4 border-b border-hair/70 px-5 py-4 sm:px-6">
        <div className="flex min-w-0 items-center gap-3">
          <Sparkles size={19} className="shrink-0 text-accent" />
          <div className="min-w-0">
            <h2 className="text-[17px] font-semibold text-neutral-100">Koç</h2>
            <p className="truncate text-xs text-neutral-500">Kendi verilerine göre günlük değerlendirme</p>
          </div>
        </div>
        <button onClick={onHistory} className="flex min-h-10 shrink-0 items-center gap-1.5 rounded-xl px-2 text-sm text-accent transition-colors hover:bg-accent/10">
          <Clock3 size={16} />
          Geçmiş
        </button>
      </div>
      <div className="h-[24rem] p-4 sm:h-[22rem] sm:p-5"><CoachChat /></div>
    </section>
  );
}
