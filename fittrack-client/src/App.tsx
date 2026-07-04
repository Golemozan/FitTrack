import { useMemo, useState } from "react";
import { Clock, Dumbbell, Flame, HeartPulse, Scale, Sparkles, Target, UtensilsCrossed } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import CoachChat from "./components/CoachChat";
import CoachHistory from "./components/CoachHistory";
import Overlay from "./components/Overlay";
import { Card, ProgressBar } from "./components/ui";
import CheckInSection from "./sections/CheckInSection";
import NutritionSection from "./sections/NutritionSection";
import WorkoutSection from "./sections/WorkoutSection";
import WeightSection from "./sections/WeightSection";
import GoalsSection from "./sections/GoalsSection";
import { useTodayMeals } from "./hooks/useNutrition";
import { useGoals } from "./hooks/useGoals";
import { useTodaySession } from "./hooks/useWorkout";
import { useWeightStats } from "./hooks/useWeight";
import { useProfile, useTodayCheckins } from "./hooks/useCoach";
import type { MealEntry } from "./types";

type View = "nutrition" | "workout" | "weight" | "goals" | "checkin" | "history";

const MOODS = ["😩", "🙁", "😐", "🙂", "😄"];

export default function App() {
  const [view, setView] = useState<View | null>(null);

  const today = new Date().toLocaleDateString("tr-TR", { weekday: "long", day: "numeric", month: "long" });

  return (
    <div className="min-h-dvh">
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
        {/* HEADER */}
        <header className="mb-6 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-accent-grad shadow-glow">
              <span className="font-cond text-lg font-bold text-accentink">FT</span>
            </div>
            <div className="leading-none">
              <div className="font-cond text-2xl font-bold tracking-tight grad-text">FitTrack</div>
              <div className="mt-1 text-xs capitalize text-neutral-500">{today}</div>
            </div>
          </div>
          <div className="hidden items-center gap-2 rounded-full border border-hair bg-white/[0.04] px-3 py-1.5 text-xs text-neutral-400 sm:flex">
            <Sparkles size={13} className="text-accent" /> Koç aktif · Haiku
          </div>
        </header>

        {/* BENTO */}
        <div className="lg:flex lg:gap-4">
          {/* Coach — hero tile */}
          <div className="lg:w-[320px] lg:shrink-0">
            <Card className="flex h-[28rem] flex-col lg:h-[calc(100vh-8rem)]">
              <div className="mb-3 flex items-center justify-between shrink-0">
                <div className="flex items-center gap-2.5">
                  <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-accent-grad text-accentink">
                    <Sparkles size={17} />
                  </span>
                  <div className="leading-tight">
                    <div className="font-cond text-lg font-bold text-white">Koç</div>
                    <div className="text-[11px] text-neutral-500">verilerinle konuşur</div>
                  </div>
                </div>
                <button
                  onClick={() => setView("history")}
                  className="rounded-lg p-1.5 text-neutral-500 transition-colors hover:text-accent"
                  title="Geçmiş konuşmalar"
                >
                  <Clock size={16} />
                </button>
              </div>
              <div className="min-h-0 flex-1">
                <CoachChat />
              </div>
            </Card>
          </div>

          {/* Tiles */}
          <div className="mt-4 grid flex-1 auto-rows-fr grid-cols-2 gap-4 lg:mt-0">
            <CalorieTile onOpen={() => setView("nutrition")} />
            <WeightTile onOpen={() => setView("weight")} />
            <WorkoutTile onOpen={() => setView("workout")} />
            <MacroTile onOpen={() => setView("nutrition")} />
            <CheckinTile onOpen={() => setView("checkin")} />
            <GoalsTile onOpen={() => setView("goals")} />
          </div>
        </div>

        <footer className="mt-8 text-center text-xs text-neutral-600">FitTrack · kişisel sağlık takibi</footer>
      </div>

      {/* DETAIL OVERLAYS */}
      <Overlay open={view === "nutrition"} onClose={() => setView(null)}>
        <NutritionSection />
      </Overlay>
      <Overlay open={view === "workout"} onClose={() => setView(null)}>
        <WorkoutSection />
      </Overlay>
      <Overlay open={view === "weight"} onClose={() => setView(null)}>
        <WeightSection />
      </Overlay>
      <Overlay open={view === "checkin"} onClose={() => setView(null)}>
        <CheckInSection />
      </Overlay>
      <Overlay open={view === "goals"} onClose={() => setView(null)}>
        <GoalsSection />
      </Overlay>
      <Overlay open={view === "history"} onClose={() => setView(null)}>
        <CoachHistory onClose={() => setView(null)} />
      </Overlay>
    </div>
  );
}

/* ---------- Tile shell ---------- */

function Tile({
  icon: Icon,
  label,
  onOpen,
  className = "",
  children,
}: {
  icon: LucideIcon;
  label: string;
  onOpen: () => void;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <button
      onClick={onOpen}
      className={`group flex flex-col rounded-2xl border border-white/[0.08] bg-white/[0.04] p-4 text-left shadow-lg shadow-black/20 backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:border-accent/40 ${className}`}
    >
      <div className="mb-2 flex items-center gap-1.5 eyebrow text-[11px] text-neutral-400">
        <Icon size={13} className="text-accent" /> {label}
      </div>
      {children}
    </button>
  );
}

/* ---------- Tiles ---------- */

function CalorieTile({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals();
  const goals = useGoals();
  const cal = useMemo(() => {
    const all = (meals.data ? Object.values(meals.data).flat() : []) as MealEntry[];
    return all.reduce((a, m) => a + m.calories, 0);
  }, [meals.data]);
  const goal = goals.data?.calorieGoal ?? 0;

  return (
    <Tile icon={Flame} label="Kalori" onOpen={onOpen}>
      <div className="flex items-baseline gap-1.5">
        <span className="num text-4xl text-white">{Math.round(cal)}</span>
        <span className="text-sm text-neutral-500">/ {goal || "—"}</span>
      </div>
      <div className="mt-3">
        <ProgressBar value={cal} max={goal} colorClass="bg-accent-grad" />
      </div>
    </Tile>
  );
}

function WeightTile({ onOpen }: { onOpen: () => void }) {
  const stats = useWeightStats();
  const w = stats.data?.currentWeight;
  const weekly = stats.data?.weeklyChange;
  const down = (weekly ?? 0) < 0;

  return (
    <Tile icon={Scale} label="Kilo" onOpen={onOpen}>
      <div className="flex items-baseline gap-1">
        <span className="num text-4xl text-white">{w != null ? w.toFixed(1) : "—"}</span>
        <span className="text-sm text-neutral-500">kg</span>
      </div>
      {weekly != null && weekly !== 0 && (
        <div className={`mt-2 text-xs font-medium ${down ? "text-loss" : "text-gain"}`}>
          {weekly > 0 ? "+" : ""}
          {weekly.toFixed(1)} kg · bu hafta
        </div>
      )}
    </Tile>
  );
}

function WorkoutTile({ onOpen }: { onOpen: () => void }) {
  const session = useTodaySession();
  const s = session.data;
  const volume = useMemo(() => {
    if (!s) return 0;
    return s.exercises.reduce(
      (t, e) => t + e.sets.reduce((a, x) => a + (x.isCompleted ? x.weightKg * x.reps : 0), 0),
      0
    );
  }, [s]);

  return (
    <Tile icon={Dumbbell} label="Antrenman" onOpen={onOpen} className="col-span-2">
      {s && (s.exercises.length > 0 || volume > 0) ? (
        <div className="flex items-end justify-between">
          <div>
            <div className="flex items-baseline gap-1.5">
              <span className="num text-4xl text-white">{Math.round(volume)}</span>
              <span className="text-sm text-neutral-500">kg hacim</span>
            </div>
            <div className="mt-1.5 truncate text-xs text-neutral-400">
              {s.exercises.length} hareket · {s.exercises.map((e) => e.name).join(", ") || "egzersiz ekle"}
            </div>
          </div>
          <Dumbbell className="text-accent/60" size={28} />
        </div>
      ) : (
        <div className="flex items-center justify-between">
          <span className="text-sm text-neutral-400">İdman yapmaya başla</span>
          <Dumbbell className="text-accent/40" size={24} />
        </div>
      )}
    </Tile>
  );
}

function MacroTile({ onOpen }: { onOpen: () => void }) {
  const meals = useTodayMeals();
  const goals = useGoals();
  const t = useMemo(() => {
    const all = (meals.data ? Object.values(meals.data).flat() : []) as MealEntry[];
    return all.reduce(
      (a, m) => ({ pro: a.pro + m.protein, carb: a.carb + m.carbs, fat: a.fat + m.fat }),
      { pro: 0, carb: 0, fat: 0 }
    );
  }, [meals.data]);
  const g = goals.data;

  const rows = [
    { label: "Protein", value: t.pro, goal: g?.proteinGoal ?? 0, color: "bg-pro" },
    { label: "Karb", value: t.carb, goal: g?.carbGoal ?? 0, color: "bg-carb" },
    { label: "Yağ", value: t.fat, goal: g?.fatGoal ?? 0, color: "bg-fat" },
  ];

  return (
    <Tile icon={UtensilsCrossed} label="Makrolar" onOpen={onOpen} className="col-span-2">
      <div className="space-y-2.5 pt-1">
        {rows.map((r) => (
          <div key={r.label} className="flex items-center gap-3">
            <span className="w-14 text-xs text-neutral-300">{r.label}</span>
            <div className="flex-1">
              <ProgressBar value={r.value} max={r.goal} colorClass={r.color} />
            </div>
            <span className="w-20 text-right text-xs tabular-nums text-neutral-400">
              {Math.round(r.value)}/{Math.round(r.goal)}g
            </span>
          </div>
        ))}
      </div>
    </Tile>
  );
}

function CheckinTile({ onOpen }: { onOpen: () => void }) {
  const today = useTodayCheckins();
  const list = today.data ?? [];
  const last = list[0];

  return (
    <Tile icon={HeartPulse} label="Nasılsın?" onOpen={onOpen}>
      {last ? (
        <div>
          <div className="flex gap-1.5 text-2xl">
            <span>{MOODS[last.mood - 1]}</span>
          </div>
          <div className="mt-2 text-xs text-neutral-400">
            {list.length} check-in · bugün
          </div>
        </div>
      ) : (
        <div>
          <div className="text-2xl">🫧</div>
          <div className="mt-2 text-xs text-neutral-400">Check-in gir</div>
        </div>
      )}
    </Tile>
  );
}

function GoalsTile({ onOpen }: { onOpen: () => void }) {
  const profile = useProfile();
  const stats = useWeightStats();
  const h = profile.data?.heightCm ?? null;
  const w = stats.data?.currentWeight ?? null;
  const bmi = h && w ? w / (h / 100) ** 2 : null;

  return (
    <Tile icon={Target} label="Hedefler" onOpen={onOpen}>
      {bmi != null ? (
        <div>
          <div className="flex items-baseline gap-1.5">
            <span className="num text-4xl text-white">{bmi.toFixed(1)}</span>
            <span className="text-sm text-neutral-500">BMI</span>
          </div>
          <div className="mt-2 text-xs text-neutral-400">makro & profil</div>
        </div>
      ) : (
        <div>
          <div className="num text-2xl text-white">Ayarla</div>
          <div className="mt-2 text-xs text-neutral-400">boy + hedef kilo</div>
        </div>
      )}
    </Tile>
  );
}
