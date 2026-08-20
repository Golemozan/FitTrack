import { useMemo, useState } from "react";
import {
  ChevronRight,
  Clock3,
  Dumbbell,
  Flame,
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
import Overlay from "./components/Overlay";
import { ProgressBar } from "./components/ui";
import CheckInSection from "./sections/CheckInSection";
import NutritionSection from "./sections/NutritionSection";
import WorkoutSection from "./sections/WorkoutSection";
import WeightSection from "./sections/WeightSection";
import GoalsSection from "./sections/GoalsSection";
import { useNutritionStreak, useTodayMeals } from "./hooks/useNutrition";
import { useGoals } from "./hooks/useGoals";
import { useTodaySession } from "./hooks/useWorkout";
import { useWeightStats } from "./hooks/useWeight";
import { useProfile, useTodayCheckins } from "./hooks/useCoach";
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
  const today = new Date().toLocaleDateString("tr-TR", { weekday: "long", day: "numeric", month: "long" });

  return (
    <div className="min-h-dvh bg-ink text-neutral-100">
      <header className="sticky top-0 z-30 border-b border-white/[0.06] bg-ink/90 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-[10px] bg-accent text-sm font-bold text-white">F</div>
            <span className="text-[17px] font-semibold tracking-tight">FitTrack</span>
          </div>
          <button onClick={lockApp} className="flex h-10 w-10 items-center justify-center rounded-full text-neutral-400 transition-colors hover:bg-white/[0.07] hover:text-white" aria-label="Uygulamayı kilitle">
            <Lock size={18} />
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 pb-28 pt-7 sm:px-6 sm:pb-12 sm:pt-10">
        <div className="mb-7 flex items-end justify-between gap-4">
          <div>
            <p className="mb-1 text-sm capitalize text-neutral-500">{today}</p>
            <h1 className="text-[36px] font-bold leading-none tracking-tight text-white sm:text-[42px]">Bugün</h1>
          </div>
          <div className="hidden items-center gap-1 rounded-2xl bg-panel p-1 md:flex" aria-label="Ana bölümler">
            {tabs.map((item) => <TabButton key={item.view} {...item} onClick={() => setView(item.view)} />)}
          </div>
        </div>

        <section className="grid gap-4 md:grid-cols-2" aria-label="Günlük özet">
          <CalorieCard onOpen={() => setView("nutrition")} />
          <WeightCard onOpen={() => setView("weight")} />
          <WorkoutCard onOpen={() => setView("workout")} />
          <MacroCard onOpen={() => setView("nutrition")} />
          <CheckinCard onOpen={() => setView("checkin")} />
          <GoalsCard onOpen={() => setView("goals")} />
        </section>

        <CoachCard onHistory={() => setView("history")} />
      </main>

      <nav className="fixed inset-x-3 bottom-3 z-40 grid grid-cols-5 rounded-[22px] border border-white/[0.08] bg-panel/90 p-1.5 shadow-[0_12px_40px_rgba(0,0,0,.45)] backdrop-blur-xl md:hidden" aria-label="Ana bölümler">
        {tabs.map((item) => <MobileTab key={item.view} {...item} onClick={() => setView(item.view)} />)}
      </nav>

      <Overlay open={view === "nutrition"} onClose={() => setView(null)}><NutritionSection /></Overlay>
      <Overlay open={view === "workout"} onClose={() => setView(null)}><WorkoutSection /></Overlay>
      <Overlay open={view === "weight"} onClose={() => setView(null)}><WeightSection /></Overlay>
      <Overlay open={view === "checkin"} onClose={() => setView(null)}><CheckInSection /></Overlay>
      <Overlay open={view === "goals"} onClose={() => setView(null)}><GoalsSection /></Overlay>
      <Overlay open={view === "history"} onClose={() => setView(null)}><CoachHistory onClose={() => setView(null)} /></Overlay>
    </div>
  );
}

function TabButton({ label, icon: Icon, onClick }: { label: string; icon: LucideIcon; onClick: () => void }) {
  return <button onClick={onClick} className="flex min-h-10 items-center gap-2 rounded-xl px-3 text-sm font-medium text-neutral-400 transition-colors hover:bg-white/[0.06] hover:text-white"><Icon size={16} />{label}</button>;
}

function MobileTab({ label, icon: Icon, onClick }: { label: string; icon: LucideIcon; onClick: () => void }) {
  return <button onClick={onClick} className="flex min-w-0 flex-col items-center justify-center gap-1 rounded-2xl px-1 py-1.5 text-neutral-500 transition-colors active:bg-white/[0.06] active:text-accent"><Icon size={20} /><span className="max-w-full truncate text-[10px] font-medium">{label}</span></button>;
}

function SummaryCard({ title, icon: Icon, onOpen, className = "", children }: { title: string; icon: LucideIcon; onOpen: () => void; className?: string; children: React.ReactNode }) {
  return (
    <button onClick={onOpen} className={`group min-w-0 rounded-[var(--radius-card)] border border-white/[0.06] bg-panel p-5 text-left shadow-[var(--shadow-card)] transition-[background-color,transform] hover:bg-card2 active:scale-[.99] ${className}`}>
      <div className="mb-5 flex items-center justify-between">
        <span className="flex items-center gap-2 text-[15px] font-semibold text-neutral-300"><Icon size={18} className="text-accent" />{title}</span>
        <ChevronRight size={18} className="text-neutral-600 transition-transform group-hover:translate-x-0.5 group-hover:text-neutral-400" />
      </div>
      {children}
    </button>
  );
}

function CalorieCard({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals(); const goals = useGoals(); const streak = useNutritionStreak();
  const calories = useMemo(() => ((meals.data ? Object.values(meals.data).flat() : []) as MealEntry[]).reduce((sum, meal) => sum + meal.calories, 0), [meals.data]);
  const goal = goals.data?.calorieGoal ?? 0;
  return <SummaryCard title="Kalori" icon={Flame} onOpen={onOpen}>
    <div className="flex items-baseline gap-2"><span className="num text-5xl text-white">{Math.round(calories)}</span><span className="text-sm text-neutral-500">/ {goal || "—"} kcal</span></div>
    <div className="mt-5"><ProgressBar value={calories} max={goal} /></div>
    <p className="mt-3 text-sm text-neutral-500">{(streak.data?.days ?? 0) > 0 ? `${streak.data!.days} günlük seri` : "Bugünkü beslenme özeti"}</p>
  </SummaryCard>;
}

function WeightCard({ onOpen }: { onOpen: () => void }) {
  const stats = useWeightStats(); const weight = stats.data?.currentWeight; const weekly = stats.data?.weeklyChange;
  return <SummaryCard title="Kilo" icon={Scale} onOpen={onOpen}>
    <div className="flex items-baseline gap-2"><span className="num text-5xl text-white">{weight != null ? weight.toFixed(1) : "—"}</span><span className="text-sm text-neutral-500">kg</span></div>
    <p className={`mt-4 text-sm ${weekly == null || weekly === 0 ? "text-neutral-500" : weekly < 0 ? "text-loss" : "text-gain"}`}>{weekly == null || weekly === 0 ? "Bu hafta değişim yok" : `${weekly > 0 ? "+" : ""}${weekly.toFixed(1)} kg · son 7 gün`}</p>
  </SummaryCard>;
}

function WorkoutCard({ onOpen }: { onOpen: () => void }) {
  const session = useTodaySession(); const current = session.data;
  const volume = useMemo(() => current?.exercises.reduce((total, exercise) => total + exercise.sets.reduce((sum, set) => sum + (set.isCompleted ? set.weightKg * set.reps : 0), 0), 0) ?? 0, [current]);
  return <SummaryCard title="Antrenman" icon={Dumbbell} onOpen={onOpen} className="md:col-span-2">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div><div className="flex items-baseline gap-2"><span className="num text-4xl text-white">{Math.round(volume)}</span><span className="text-sm text-neutral-500">kg hacim</span></div><p className="mt-3 text-sm text-neutral-500">{current && current.exercises.length > 0 ? `${current.exercises.length} hareket tamamlandı` : "Bugünün antrenmanı henüz başlamadı"}</p></div>
      {current && current.exercises.length > 0 && <p className="max-w-sm truncate text-sm text-neutral-400">{current.exercises.map((exercise) => exercise.name).join(" · ")}</p>}
    </div>
  </SummaryCard>;
}

function MacroCard({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals(); const goals = useGoals();
  const totals = useMemo(() => ((meals.data ? Object.values(meals.data).flat() : []) as MealEntry[]).reduce((sum, meal) => ({ protein: sum.protein + meal.protein, carbs: sum.carbs + meal.carbs, fat: sum.fat + meal.fat }), { protein: 0, carbs: 0, fat: 0 }), [meals.data]);
  const rows = [{ label: "Protein", value: totals.protein, goal: goals.data?.proteinGoal ?? 0, color: "bg-pro" }, { label: "Karbonhidrat", value: totals.carbs, goal: goals.data?.carbGoal ?? 0, color: "bg-carb" }, { label: "Yağ", value: totals.fat, goal: goals.data?.fatGoal ?? 0, color: "bg-fat" }];
  return <SummaryCard title="Makrolar" icon={UtensilsCrossed} onOpen={onOpen} className="md:col-span-2"><div className="grid gap-5 sm:grid-cols-3">{rows.map((row) => <div key={row.label}><div className="mb-2.5 flex justify-between text-sm"><span className="text-neutral-400">{row.label}</span><span className="tnum text-neutral-300">{Math.round(row.value)} / {Math.round(row.goal)} g</span></div><ProgressBar value={row.value} max={row.goal} colorClass={row.color} /></div>)}</div></SummaryCard>;
}

function CheckinCard({ onOpen }: { onOpen: () => void }) {
  const checkins = useTodayCheckins(); const list = checkins.data ?? []; const last = list[0];
  return <SummaryCard title="Nasıl hissediyorsun?" icon={HeartPulse} onOpen={onOpen}><div className="text-4xl" aria-label={last ? `Ruh hali ${last.mood}/5` : "Durum kaydı yok"}>{last ? MOODS[last.mood - 1] : "—"}</div><p className="mt-4 text-sm text-neutral-500">{last ? `Bugün ${list.length} kayıt` : "Kısa bir check-in yap"}</p></SummaryCard>;
}

function GoalsCard({ onOpen }: { onOpen: () => void }) {
  const profile = useProfile(); const stats = useWeightStats(); const height = profile.data?.heightCm ?? null; const weight = stats.data?.currentWeight ?? null; const bmi = height && weight ? weight / (height / 100) ** 2 : null;
  return <SummaryCard title="Hedefler" icon={Target} onOpen={onOpen}><div className="flex items-baseline gap-2"><span className="num text-4xl text-white">{bmi != null ? bmi.toFixed(1) : "—"}</span><span className="text-sm text-neutral-500">BMI</span></div><p className="mt-4 text-sm text-neutral-500">{bmi != null ? "Profil ve hedeflerini görüntüle" : "Profil bilgilerini tamamla"}</p></SummaryCard>;
}

function CoachCard({ onHistory }: { onHistory: () => void }) {
  return <section className="mt-4 overflow-hidden rounded-[var(--radius-card)] border border-white/[0.06] bg-panel shadow-[var(--shadow-card)]" aria-label="Koç">
    <div className="flex items-center justify-between border-b border-white/[0.06] px-5 py-4"><div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-full bg-accent/15 text-accent"><Sparkles size={18} /></div><div><h2 className="text-[17px] font-semibold text-white">Koç</h2><p className="text-xs text-neutral-500">Verilerine göre sorularını yanıtlar</p></div></div><button onClick={onHistory} className="flex h-10 items-center gap-1.5 rounded-xl px-2 text-sm text-accent hover:bg-accent/10"><Clock3 size={16} />Geçmiş</button></div>
    <div className="h-[28rem] p-4 sm:p-5"><CoachChat /></div>
  </section>;
}
