import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ChevronRight, Coffee, Cookie, Moon, Plus, Sun } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import AddFoodSheet from "./AddFoodSheet";
import { Skeleton } from "./ui";
import { useGoals } from "../hooks/useGoals";
import { useTodayMeals } from "../hooks/useNutrition";
import type { MealEntry, MealType } from "../types";

/* Bugün ne yedin: alınan kalori + makrolar ve öğün öğün ne kadar ne yenildiği.
   Kalori tek halka (mercan = beslenme), makrolar kendi renklerinde ince çubuk. Boş öğün gizlenmez —
   "eklenmedi" satırı tek dokunuşla o öğünü açar; günün iskeleti her zaman aynı yerde durur. */

const MEALS: Array<{ type: MealType; label: string; icon: LucideIcon }> = [
  { type: "Breakfast", label: "Kahvaltı", icon: Coffee },
  { type: "Lunch", label: "Öğle", icon: Sun },
  { type: "Dinner", label: "Akşam", icon: Moon },
  { type: "Snack", label: "Ara öğün", icon: Cookie },
];

const VISIBLE_ITEMS = 3;
const n0 = (v: number) => Math.round(v).toLocaleString("tr-TR");

function guessMeal(): MealType {
  const h = new Date().getHours();
  if (h >= 5 && h < 11) return "Breakfast";
  if (h >= 11 && h < 15) return "Lunch";
  if (h >= 18 && h < 22) return "Dinner";
  return "Snack";
}

export default function NutritionToday() {
  const meals = useTodayMeals();
  const goals = useGoals();
  const navigate = useNavigate();
  const [sheet, setSheet] = useState<{ open: boolean; meal: MealType }>({ open: false, meal: "Breakfast" });

  const all = useMemo(() => (meals.data ? (Object.values(meals.data).flat() as MealEntry[]) : []), [meals.data]);
  const totals = useMemo(
    () => all.reduce((s, m) => ({ cal: s.cal + m.calories, p: s.p + m.protein, c: s.c + m.carbs, f: s.f + m.fat }), { cal: 0, p: 0, c: 0, f: 0 }),
    [all],
  );
  const g = goals.data;
  const openAdd = (meal: MealType) => setSheet({ open: true, meal });

  return (
    <section className="rounded-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-card sm:p-6" aria-label="Bugünkü beslenme">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-[17px] font-bold">Bugün ne yedin?</h2>
        <button
          type="button"
          onClick={() => openAdd(guessMeal())}
          className="flex min-h-10 items-center gap-1.5 rounded-full bg-accent px-4 text-sm font-semibold text-accentink transition-[filter] hover:brightness-[1.04]"
        >
          <Plus size={16} strokeWidth={2.5} /> Ekle
        </button>
      </div>

      {meals.isPending || goals.isPending ? (
        <div className="mt-5 grid grid-cols-[8.5rem_minmax(0,1fr)] items-center gap-5">
          <Skeleton className="h-[8.5rem] w-[8.5rem] rounded-full" />
          <div className="space-y-4"><Skeleton className="h-9" /><Skeleton className="h-9" /><Skeleton className="h-9" /></div>
        </div>
      ) : (
        <div className="mt-5 grid grid-cols-[8.5rem_minmax(0,1fr)] items-center gap-5 sm:grid-cols-[10rem_minmax(0,1fr)] sm:gap-7">
          <CalorieRing value={totals.cal} goal={g?.calorieGoal ?? 0} />
          <div className="space-y-3.5">
            <Macro label="Protein" value={totals.p} goal={g?.proteinGoal ?? 0} color="var(--color-protein)" />
            <Macro label="Karb" fullLabel="Karbonhidrat" value={totals.c} goal={g?.carbGoal ?? 0} color="var(--color-carb)" />
            <Macro label="Yağ" value={totals.f} goal={g?.fatGoal ?? 0} color="var(--color-fat)" />
          </div>
        </div>
      )}

      <ul className="mt-6 divide-y divide-hair border-t border-hair">
        {MEALS.map((m) => (
          <MealRow key={m.type} meta={m} entries={meals.data?.[m.type] ?? []} onAdd={() => openAdd(m.type)} loading={meals.isPending} />
        ))}
      </ul>

      <button
        type="button"
        onClick={() => navigate("/beslenme")}
        className="mt-2 flex min-h-11 w-full items-center justify-center gap-1 rounded-full text-sm font-semibold text-neutral-400 transition-colors hover:bg-card2 hover:text-neutral-100"
      >
        Beslenme geçmişi ve detaylar <ChevronRight size={16} />
      </button>

      <AddFoodSheet open={sheet.open} defaultMeal={sheet.meal} onClose={() => setSheet((s) => ({ ...s, open: false }))} />
    </section>
  );
}

function CalorieRing({ value, goal }: { value: number; goal: number }) {
  const R = 52;
  const C = 2 * Math.PI * R;
  const pct = goal > 0 ? value / goal : 0;
  const over = goal > 0 && value > goal * 1.05;
  const remaining = goal - value;
  return (
    <div className="relative aspect-square w-full" role="img" aria-label={`${n0(value)} / ${n0(goal)} kalori`}>
      <svg viewBox="0 0 120 120" className="h-full w-full -rotate-90">
        <circle cx="60" cy="60" r={R} fill="none" stroke="var(--color-track)" strokeWidth="11" />
        <circle
          cx="60" cy="60" r={R} fill="none" stroke={over ? "var(--color-danger)" : "var(--color-move)"} strokeWidth="11" strokeLinecap="round"
          strokeDasharray={C} strokeDashoffset={C * (1 - Math.min(1, pct))}
          style={{ transition: "stroke-dashoffset 700ms var(--ease-out)" }}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center text-center">
        <span className="num text-[26px] sm:text-[30px]">{n0(value)}</span>
        <span className="mt-0.5 text-[11px] font-semibold text-neutral-500">/ {n0(goal)} kcal</span>
        <span className={`mt-1.5 rounded-full px-2 py-0.5 text-[10px] font-bold ${over ? "bg-danger/15 text-danger" : "bg-card2 text-neutral-300"}`}>
          {goal <= 0 ? "Hedef yok" : remaining >= 0 ? `${n0(remaining)} kaldı` : `${n0(-remaining)} fazla`}
        </span>
      </div>
    </div>
  );
}

function Macro({ label, fullLabel, value, goal, color }: { label: string; fullLabel?: string; value: number; goal: number; color: string }) {
  const pct = goal > 0 ? Math.min(1, value / goal) : 0;
  return (
    <div>
      <div className="flex items-baseline justify-between gap-2">
        <span className="flex min-w-0 items-center gap-1.5 text-sm font-semibold" title={fullLabel ?? label}>
          <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ background: color }} aria-hidden="true" />
          <span className="truncate" aria-label={fullLabel ?? label}>{label}</span>
        </span>
        <span className="shrink-0 whitespace-nowrap text-xs text-neutral-500">
          <span className="num text-sm text-neutral-100">{n0(value)}</span> / {n0(goal)} g
        </span>
      </div>
      <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-track" role="progressbar" aria-label={fullLabel ?? label} aria-valuemin={0} aria-valuemax={goal} aria-valuenow={Math.round(value)}>
        <div className="h-full rounded-full" style={{ width: `${pct * 100}%`, background: color, transition: "width 600ms var(--ease-out)" }} />
      </div>
    </div>
  );
}

function MealRow({ meta, entries, onAdd, loading }: { meta: (typeof MEALS)[number]; entries: MealEntry[]; onAdd: () => void; loading: boolean }) {
  const [expanded, setExpanded] = useState(false);
  const sorted = [...entries].sort((a, b) => a.loggedAt.localeCompare(b.loggedAt));
  const kcal = sorted.reduce((s, e) => s + e.calories, 0);
  const shown = expanded ? sorted : sorted.slice(0, VISIBLE_ITEMS);
  const time = sorted[0] ? new Date(sorted[0].loggedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" }) : null;

  if (loading) return <li className="py-3"><Skeleton className="h-10 w-full" /></li>;

  if (sorted.length === 0)
    return (
      <li className="flex min-h-14 items-center gap-3 py-2">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-card2 text-neutral-500"><meta.icon size={17} /></span>
        <span className="flex-1 text-sm text-neutral-500"><span className="font-semibold text-neutral-300">{meta.label}</span> · eklenmedi</span>
        <button type="button" onClick={onAdd} aria-label={`${meta.label} ekle`}
          className="flex min-h-9 items-center gap-1 rounded-full border border-hair px-3 text-xs font-semibold text-neutral-300 transition-colors hover:bg-card2">
          <Plus size={14} /> Ekle
        </button>
      </li>
    );

  return (
    <li className="py-3">
      <div className="flex items-center gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-move text-navy"><meta.icon size={17} /></span>
        <span className="min-w-0 flex-1">
          <span className="block text-[15px] font-bold">{meta.label}</span>
          <span className="block text-xs text-neutral-500">{time} · {sorted.length} besin</span>
        </span>
        <span className="text-right">
          <span className="num text-[17px]">{n0(kcal)}</span>
          <span className="ml-1 text-xs font-semibold text-neutral-500">kcal</span>
        </span>
        <button type="button" onClick={onAdd} aria-label={`${meta.label} öğününe ekle`}
          className="flex h-9 w-9 items-center justify-center rounded-full text-neutral-400 transition-colors hover:bg-card2 hover:text-neutral-100">
          <Plus size={17} />
        </button>
      </div>
      <ul className="ml-12 mt-2 space-y-1.5">
        {shown.map((e) => (
          <li key={e.id} className="flex items-baseline justify-between gap-3 text-sm">
            <span className="min-w-0 truncate text-neutral-200">
              {e.foodName}
              <span className="ml-1.5 text-xs text-neutral-500">{n0(e.grams)} g · P {n0(e.protein)} g</span>
            </span>
            <span className="tnum shrink-0 text-xs font-semibold text-neutral-400">{n0(e.calories)} kcal</span>
          </li>
        ))}
      </ul>
      {sorted.length > VISIBLE_ITEMS && (
        <button type="button" onClick={() => setExpanded((v) => !v)} className="ml-12 mt-1.5 min-h-8 text-xs font-semibold text-neutral-400 hover:text-neutral-100">
          {expanded ? "Daha az göster" : `+${sorted.length - VISIBLE_ITEMS} besin daha`}
        </button>
      )}
    </li>
  );
}
