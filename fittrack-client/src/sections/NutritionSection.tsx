import { useMemo, useState } from "react";
import { ChevronDown, History, Plus, Trash2, UtensilsCrossed } from "lucide-react";
import { useDayMeals, useDeleteMeal, useTodayMeals } from "../hooks/useNutrition";
import { useGoals } from "../hooks/useGoals";
import MacroRing from "../components/MacroRing";
import AddFoodSheet from "../components/AddFoodSheet";
import CalorieHistory from "../components/CalorieHistory";
import {
  Card,
  Chip,
  EmptyLine,
  EmptyState,
  IconButton,
  ProgressBar,
  Segmented,
  Skeleton,
} from "../components/ui";
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
      if (next.has(t)) next.delete(t);
      else next.add(t);
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
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <SectionHeader
          icon={UtensilsCrossed}
          title="Beslenme"
          className=""
          action={tab === "today" ? { label: "Yemek Ekle", onClick: () => openSheet("Breakfast") } : undefined}
        />
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: "today", label: "Bugün" },
            { value: "history", label: "Geçmiş", icon: History },
          ]}
        />
      </div>

      {tab === "today" ? (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          {/* ---- Macro summary ---- */}
          <Card className="self-start lg:col-span-1">
            {loading ? (
              <div className="flex flex-col items-center gap-5">
                <Skeleton className="h-48 w-48 rounded-full" />
                <div className="w-full space-y-3">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                </div>
              </div>
            ) : (
              <>
                <MacroRing consumed={totals.calories} goal={g?.calorieGoal ?? 0} />
                <div className="mt-4 flex items-baseline justify-center gap-1.5">
                  <span className="num text-lg text-neutral-100">{Math.round(totals.calories)}</span>
                  <span className="num text-sm font-normal text-neutral-500">
                    / {g?.calorieGoal ?? 0} kcal
                  </span>
                </div>

                <div className="mt-5 space-y-3.5">
                  {macroRows.map((m) => {
                    const remaining = Math.max(0, Math.round(m.goal - m.value));
                    const done = m.goal > 0 && m.value >= m.goal;
                    return (
                      // Fixed label / flexible bar / fixed number columns keep all
                      // three rows aligned no matter how long the label is.
                      <div key={m.label} className="grid grid-cols-[4.5rem_1fr_5rem] items-center gap-3">
                        <span className="flex items-center gap-2 whitespace-nowrap text-[13px] text-neutral-300">
                          <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${m.color}`} />
                          {m.label}
                        </span>
                        <ProgressBar value={m.value} max={m.goal} colorClass={m.color} />
                        <span className="text-right tabular-nums">
                          <span className="block text-xs font-medium text-neutral-300">
                            {Math.round(m.value)} / {Math.round(m.goal)}g
                          </span>
                          <span className="block text-[11px] text-neutral-600">
                            {done ? "tamam" : `${remaining}g kaldı`}
                          </span>
                        </span>
                      </div>
                    );
                  })}
                </div>
              </>
            )}
          </Card>

          {/* ---- Meals ---- */}
          <div className="space-y-4 lg:col-span-2">
            {!loading && totals.calories === 0 && (
              <Card>
                <EmptyState icon={UtensilsCrossed} title="Bugün henüz bir şey eklemedin">
                  Bir öğünün altındaki Ekle'ye bas ya da yukarıdan Yemek Ekle'yi kullan.
                </EmptyState>
              </Card>
            )}

            <div className="grid gap-3 sm:grid-cols-2">
              {MEALS.map(({ tr, type }) => {
                const entries = (today.data?.[type] ?? []) as MealEntry[];
                const t = sum(entries);
                const isOpen = !collapsed.has(type);
                return (
                  <Card key={type} flat padded={false} className="flex flex-col">
                    <button
                      onClick={() => toggle(type)}
                      className="flex w-full items-center justify-between gap-2 rounded-2xl px-4 py-3.5 text-left transition-colors hover:bg-white/[0.02]"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <ChevronDown
                          size={17}
                          className={`shrink-0 text-neutral-500 transition-transform ${isOpen ? "" : "-rotate-90"}`}
                        />
                        <span className="truncate text-[15px] font-semibold text-neutral-100">{tr}</span>
                        {entries.length > 0 && <Chip>{entries.length}</Chip>}
                      </span>
                      <span className="shrink-0 text-right">
                        <span className="num block text-base text-neutral-200">
                          {Math.round(t.calories)}
                          <span className="ml-1 text-[11px] font-normal text-neutral-500">kcal</span>
                        </span>
                        {entries.length > 0 && (
                          <span className="mt-0.5 block space-x-1.5 text-[11px] tabular-nums">
                            <span className="text-pro/80">P{Math.round(t.protein)}</span>
                            <span className="text-carb/80">K{Math.round(t.carbs)}</span>
                            <span className="text-fat/80">Y{Math.round(t.fat)}</span>
                          </span>
                        )}
                      </span>
                    </button>

                    {isOpen && (
                      <div className="px-3 pb-3">
                        {entries.length === 0 ? (
                          <EmptyLine>Henüz bir şey yok</EmptyLine>
                        ) : (
                          <ul className="space-y-0.5">
                            {entries.map((m) => (
                              <MealRow
                                key={m.id}
                                entry={m}
                                onEdit={() => openEdit(m)}
                                onDelete={() => deleteMeal.mutate(m.id)}
                              />
                            ))}
                          </ul>
                        )}
                        <AddRowButton onClick={() => openSheet(type)} />
                      </div>
                    )}
                  </Card>
                );
              })}
            </div>
          </div>
        </div>
      ) : (
        /* ---- History ---- */
        <div className="space-y-4">
          <Card>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <h3 className="text-sm font-semibold text-neutral-200">Kalori geçmişi</h3>
              <Segmented
                size="sm"
                value={histDays}
                onChange={setHistDays}
                options={[7, 30, 60].map((r) => ({ value: r, label: `${r} gün` }))}
              />
            </div>
            <CalorieHistory days={histDays} onSelectDay={setSelectedDate} selectedDate={selectedDate} />
          </Card>

          {selectedDate && (
            <Card>
              <div className="mb-4 flex items-center justify-between gap-3">
                <h3 className="min-w-0 truncate text-sm font-semibold text-neutral-200">
                  {new Date(selectedDate).toLocaleDateString("tr-TR", {
                    day: "numeric",
                    month: "long",
                    weekday: "long",
                  })}
                </h3>
                <button
                  onClick={() => setSelectedDate(null)}
                  className="shrink-0 rounded-lg px-2.5 py-1.5 text-xs font-medium text-neutral-400 transition-colors hover:bg-white/[0.06] hover:text-neutral-100"
                >
                  Kapat
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
                      <Card key={type} flat padded={false} className="flex flex-col">
                        <div className="flex items-center justify-between gap-2 px-4 py-3.5">
                          <span className="truncate text-[15px] font-semibold text-neutral-100">{tr}</span>
                          {entries.length > 0 && (
                            <span className="num shrink-0 text-base text-neutral-200">
                              {Math.round(t.calories)}
                              <span className="ml-1 text-[11px] font-normal text-neutral-500">kcal</span>
                            </span>
                          )}
                        </div>
                        <div className="px-3 pb-3">
                          {entries.length === 0 ? (
                            <EmptyLine>Henüz bir şey yok</EmptyLine>
                          ) : (
                            <ul className="space-y-0.5">
                              {entries.map((m) => (
                                <MealRow
                                  key={m.id}
                                  entry={m}
                                  onEdit={() => openEdit(m)}
                                  onDelete={() => deleteMeal.mutate(m.id)}
                                />
                              ))}
                            </ul>
                          )}
                          <AddRowButton
                            onClick={() => {
                              setEditEntry(null);
                              setSheetMeal(type);
                              setSheetOpen(true);
                            }}
                          />
                        </div>
                      </Card>
                    );
                  })}
                </div>
              )}
            </Card>
          )}
        </div>
      )}

      <AddFoodSheet
        open={sheetOpen}
        defaultMeal={sheetMeal}
        editEntry={editEntry}
        logDate={selectedDate}
        onClose={closeSheet}
      />
    </section>
  );
}

/* ---------- Shared row ---------- */

/**
 * One row treatment for both the today and history views — they render the same
 * `MealEntry`, so they had no business looking different.
 */
function MealRow({
  entry,
  onEdit,
  onDelete,
}: {
  entry: MealEntry;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <li className="flex items-center gap-2 rounded-xl px-2 transition-colors hover:bg-white/[0.04]">
      <button
        onClick={onEdit}
        className="min-w-0 flex-1 py-2 text-left"
        aria-label={`${entry.foodName} düzenle`}
      >
        <span className="block truncate text-sm font-medium capitalize text-neutral-100">
          {entry.foodName}
        </span>
        <span className="mt-0.5 flex flex-wrap items-center gap-x-2 text-[11px] tabular-nums text-neutral-500">
          <span>{Math.round(entry.grams)}g</span>
          <span className="text-pro/70">P{Math.round(entry.protein)}</span>
          <span className="text-carb/70">K{Math.round(entry.carbs)}</span>
          <span className="text-fat/70">Y{Math.round(entry.fat)}</span>
        </span>
      </button>
      <span className="num shrink-0 text-sm tabular-nums text-neutral-200">
        {Math.round(entry.calories)}
      </span>
      <IconButton icon={Trash2} tone="danger" size={15} onClick={onDelete} aria-label="Sil" />
    </li>
  );
}

function AddRowButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className="mt-1 flex w-full items-center justify-center gap-1.5 rounded-xl py-2.5 text-[13px] font-medium text-accent transition-colors hover:bg-accent/10"
    >
      <Plus size={15} /> Ekle
    </button>
  );
}
