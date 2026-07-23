import { useMemo, useState } from "react";
import { ChevronDown, History, Pencil, Plus, Trash2, UtensilsCrossed, X } from "lucide-react";
import { useDayMeals, useDeleteMeal, useTodayMeals } from "../hooks/useNutrition";
import { useGoals } from "../hooks/useGoals";
import MacroRing from "../components/MacroRing";
import AddFoodSheet from "../components/AddFoodSheet";
import CalorieHistory from "../components/CalorieHistory";
import { Card, EmptyState, ProgressBar, Skeleton } from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";
import type { MealEntry, MealType } from "../types";

const MEALS: { tr: string; type: MealType }[] = [
  { tr: "Kahvaltı", type: "Breakfast" },
  { tr: "Öğle", type: "Lunch" },
  { tr: "Akşam", type: "Dinner" },
  { tr: "Atıştırma", type: "Snack" },
];

const zero = { calories: 0, protein: 0, carbs: 0, fat: 0 };

function sum(entries: MealEntry[]) {
  return entries.reduce(
    (a, m) => ({
      calories: a.calories + m.calories,
      protein: a.protein + m.protein,
      carbs: a.carbs + m.carbs,
      fat: a.fat + m.fat,
    }),
    { ...zero }
  );
}

export default function NutritionSection() {
  const today = useTodayMeals();
  const goals = useGoals();
  const deleteMeal = useDeleteMeal();

  const [tab, setTab] = useState<"today" | "history">("today");
  const [sheetOpen, setSheetOpen] = useState(false);
  const [sheetMeal, setSheetMeal] = useState<MealType>("Breakfast");
  const [editEntry, setEditEntry] = useState<MealEntry | null>(null);
  const [collapsed, setCollapsed] = useState<Set<MealType>>(new Set());
  const [histDays, setHistDays] = useState(30);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);

  const dayMeals = useDayMeals(selectedDate);

  const totals = useMemo(() => {
    const all = today.data ? Object.values(today.data).flat() : [];
    return sum(all as MealEntry[]);
  }, [today.data]);

  const g = goals.data;

  const macroRows = [
    { label: "Protein", value: totals.protein, goal: g?.proteinGoal ?? 0, color: "bg-pro" },
    { label: "Karb", value: totals.carbs, goal: g?.carbGoal ?? 0, color: "bg-carb" },
    { label: "Yağ", value: totals.fat, goal: g?.fatGoal ?? 0, color: "bg-fat" },
  ];

  const toggle = (t: MealType) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      next.has(t) ? next.delete(t) : next.add(t);
      return next;
    });

  const openSheet = (meal: MealType) => {
    setEditEntry(null);
    setSheetMeal(meal);
    setSheetOpen(true);
  };

  const openEdit = (entry: MealEntry) => {
    setEditEntry(entry);
    setSheetOpen(true);
  };

  const closeSheet = () => {
    setSheetOpen(false);
    setEditEntry(null);
  };

  const loading = today.isLoading || goals.isLoading;

  return (
    <section id="beslenme" className="scroll-mt-24">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <SectionHeader
          icon={UtensilsCrossed}
          title="Beslenme"
          action={tab === "today" ? { label: "Yemek Ekle", onClick: () => openSheet("Breakfast") } : undefined}
        />
        <div className="flex gap-1 rounded-xl border border-hair bg-white/[0.04] p-1">
          <button
            onClick={() => setTab("today")}
            className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
              tab === "today" ? "bg-accent text-accentink" : "text-neutral-400 hover:text-white"
            }`}
          >
            Bugün
          </button>
          <button
            onClick={() => setTab("history")}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
              tab === "history" ? "bg-accent text-accentink" : "text-neutral-400 hover:text-white"
            }`}
          >
            <History size={14} />
            Geçmiş
          </button>
        </div>
      </div>

      {tab === "today" ? (
        <>
        {/* ---- TODAY VIEW ---- */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* MACRO SUMMARY */}
        <Card className="self-start lg:col-span-1">
          {loading ? (
            <div className="flex flex-col items-center gap-4">
              <Skeleton className="h-48 w-48 rounded-full" />
              <div className="w-full space-y-2">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-full" />
              </div>
            </div>
          ) : (
            <>
              <MacroRing consumed={totals.calories} goal={g?.calorieGoal ?? 0} />
              <div className="mt-3 flex items-baseline justify-center gap-1.5 text-sm text-neutral-400">
                <span className="num text-lg text-neutral-100">{Math.round(totals.calories)}</span>
                <span>/ {g?.calorieGoal ?? 0} kcal</span>
              </div>
              <div className="mt-4 space-y-3">
                {macroRows.map((m) => {
                  const remaining = Math.max(0, Math.round(m.goal - m.value));
                  const done = m.goal > 0 && m.value >= m.goal;
                  return (
                    <div key={m.label} className="flex items-center gap-3">
                      <span className="flex w-16 items-center gap-1.5 text-sm text-neutral-300">
                        <span className={`h-2 w-2 shrink-0 rounded-full ${m.color}`} />
                        {m.label}
                      </span>
                      <div className="flex-1">
                        <ProgressBar value={m.value} max={m.goal} colorClass={m.color} />
                      </div>
                      <span className="w-24 text-right text-xs tabular-nums">
                        <span className="block font-semibold text-neutral-200">
                          {Math.round(m.value)} / {Math.round(m.goal)}g
                        </span>
                        <span className="block text-[10px] text-neutral-400">
                          {done ? "tamam ✓" : `${remaining}g kaldı`}
                        </span>
                      </span>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </Card>

        <div className="space-y-4 lg:col-span-2">
          {!loading && totals.calories === 0 && (
            <Card>
              <EmptyState icon={UtensilsCrossed} title="Bugün henüz bir şey eklemedin">
                Öğünlerden veya "Yemek Ekle" ile besin ekle.
              </EmptyState>
            </Card>
          )}

          {/* MEAL SECTIONS */}
          <div className="grid gap-3 sm:grid-cols-2">
            {MEALS.map(({ tr, type }) => {
              const entries = (today.data?.[type] ?? []) as MealEntry[];
              const t = sum(entries);
              const isOpen = !collapsed.has(type);
              return (
                <Card key={type} className="flex flex-col p-0">
                  <button
                    onClick={() => toggle(type)}
                    className="flex w-full items-center justify-between gap-2 px-4 py-3"
                  >
                    <div className="flex items-center gap-2">
                      <ChevronDown
                        size={18}
                        className={`text-neutral-400 transition-transform ${isOpen ? "" : "-rotate-90"}`}
                      />
                      <span className="font-semibold">{tr}</span>
                      {entries.length > 0 && (
                        <span className="rounded-full bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold tabular-nums text-neutral-300">
                          {entries.length}
                        </span>
                      )}
                    </div>
                    <div className="text-right">
                      <div className="num text-base text-neutral-200">
                        {Math.round(t.calories)}
                        <span className="ml-0.5 text-[10px] font-normal text-neutral-400">kcal</span>
                      </div>
                      {entries.length > 0 && (
                        <div className="text-[10px] tabular-nums">
                          <span className="text-pro">P{Math.round(t.protein)}</span>{" "}
                          <span className="text-carb">K{Math.round(t.carbs)}</span>{" "}
                          <span className="text-fat">Y{Math.round(t.fat)}</span>
                        </div>
                      )}
                    </div>
                  </button>

                  {isOpen && (
                    <div className="px-4 pb-3">
                      {entries.length === 0 ? (
                        <p className="py-2 text-sm text-neutral-500">Boş</p>
                      ) : (
                        <ul className="space-y-1">
                          {entries.map((m) => (
                            <li
                              key={m.id}
                              className="group flex items-center gap-1 rounded-lg px-2 py-1.5 hover:bg-white/5"
                            >
                              <button
                                onClick={() => openEdit(m)}
                                className="flex min-w-0 flex-1 items-center gap-1.5 text-left"
                                aria-label={`${m.foodName} düzenle`}
                              >
                                <div className="min-w-0 flex-1">
                                  <span className="block truncate text-sm font-medium capitalize">{m.foodName}</span>
                                  <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0 text-[11px] tabular-nums text-neutral-400">
                                    <span>{Math.round(m.grams)}g</span>
                                    <span className="text-pro">P{Math.round(m.protein)}</span>
                                    <span className="text-carb">K{Math.round(m.carbs)}</span>
                                    <span className="text-fat">Y{Math.round(m.fat)}</span>
                                  </div>
                                </div>
                              </button>
                              <Pencil
                                size={12}
                                className="hidden shrink-0 text-neutral-400 group-hover:block"
                              />
                              <span className="num shrink-0 text-sm text-neutral-200 tabular-nums">
                                {Math.round(m.calories)}
                              </span>
                              <button
                                onClick={() => deleteMeal.mutate(m.id)}
                                className="shrink-0 text-neutral-400 hover:text-gain"
                                aria-label="Sil"
                              >
                                <Trash2 size={14} />
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                      <button
                        onClick={() => openSheet(type)}
                        className="mt-1 flex items-center gap-1 px-2 text-sm font-medium text-accent"
                      >
                        <Plus size={14} /> Ekle
                      </button>
                    </div>
                  )}
                </Card>
              );
            })}
          </div>
        </div>
      </div>

      {/* ---- /TODAY ---- */}
      </>
      ) : (
        /* ---- HISTORY VIEW ---- */
        <div className="space-y-4">
          <Card>
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-neutral-200">Kalori Geçmişi</h3>
              <div className="flex gap-1">
                {[7, 30, 60].map((r) => (
                  <button
                    key={r}
                    onClick={() => setHistDays(r)}
                    className={`rounded-lg px-2 py-1 text-xs font-medium ${
                      histDays === r ? "bg-accent text-accentink" : "bg-card2 text-neutral-400"
                    }`}
                  >
                    {r} gün
                  </button>
                ))}
              </div>
            </div>
            <CalorieHistory days={histDays} onSelectDay={setSelectedDate} selectedDate={selectedDate} />
          </Card>

          {selectedDate && (
            <Card>
              <div className="mb-3 flex items-center justify-between">
                <h3 className="text-sm font-semibold text-neutral-200">
                  {new Date(selectedDate).toLocaleDateString("tr-TR", { day: "numeric", month: "long", weekday: "long" })}
                </h3>
                <button
                  onClick={() => setSelectedDate(null)}
                  className="text-neutral-400 hover:text-white"
                  aria-label="Kapat"
                >
                  <X size={18} />
                </button>
              </div>
              {dayMeals.isLoading ? (
                <Skeleton className="h-24 w-full" />
              ) : (
                <div className="grid gap-3 sm:grid-cols-2">
                  {MEALS.map(({ tr, type }) => {
                    const entries = (dayMeals.data?.[type] ?? []) as MealEntry[];
                    const t = sum(entries);
                    return (
                      <div key={type} className="rounded-xl border border-hair bg-white/[0.03] p-3">
                        <div className="mb-2 flex items-center justify-between">
                          <span className="text-sm font-semibold">{tr}</span>
                          {entries.length > 0 && (
                            <span className="num text-sm text-neutral-200">{Math.round(t.calories)} kcal</span>
                          )}
                        </div>
                        {entries.length > 0 ? (
                          <ul className="space-y-1">
                            {entries.map((m) => (
                              <li
                                key={m.id}
                                className="group flex items-center gap-1 rounded-lg px-2 py-1.5 hover:bg-white/5"
                              >
                                <button
                                  onClick={() => openEdit(m)}
                                  className="flex min-w-0 flex-1 items-center gap-1.5 text-left"
                                >
                                  <span className="truncate text-sm font-medium">{m.foodName}</span>
                                  <span className="text-xs text-neutral-400">{Math.round(m.grams)}g</span>
                                </button>
                                <span className="num shrink-0 text-sm text-neutral-200 tabular-nums">
                                  {Math.round(m.calories)}
                                </span>
                                <button
                                  onClick={() => deleteMeal.mutate(m.id)}
                                  className="shrink-0 text-neutral-400 hover:text-gain"
                                >
                                  <Trash2 size={14} />
                                </button>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="py-2 text-sm text-neutral-500">Boş</p>
                        )}
                        <button
                          onClick={() => { setSheetMeal(type); setSheetOpen(true); }}
                          className="mt-1 flex items-center gap-1 px-1 text-sm font-medium text-accent hover:underline"
                        >
                          <Plus size={14} /> Ekle
                        </button>
                      </div>
                    );
                  })}
                </div>
              )}
            </Card>
          )}
        </div>
      )}
      <AddFoodSheet open={sheetOpen} defaultMeal={sheetMeal} editEntry={editEntry} logDate={selectedDate} onClose={closeSheet} />
    </section>
    );
}
