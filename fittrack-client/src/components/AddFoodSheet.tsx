import { useEffect, useMemo, useState } from "react";
import { Search, X } from "lucide-react";
import { useLogFood, useUpdateMeal } from "../hooks/useNutrition";
import { FREQUENT_FOODS, searchFoods } from "../data/frequentFoods";
import type { Food } from "../data/frequentFoods";
import { IconButton } from "./ui";
import type { MealEntry, MealType } from "../types";

const MEAL_OPTIONS: { tr: string; type: MealType }[] = [
  { tr: "Kahvaltı", type: "Breakfast" },
  { tr: "Öğle", type: "Lunch" },
  { tr: "Akşam", type: "Dinner" },
  { tr: "Atıştırma", type: "Snack" },
];

const PORTIONS = [50, 100, 150, 200, 250, 300];

/** Selectable tile / pill. Soft when idle, soft-accent when chosen. */
const choiceCls = (active: boolean) =>
  `flex h-11 items-center justify-center rounded-xl text-sm font-semibold transition-colors ${
    active ? "bg-accent/15 text-accent ring-1 ring-inset ring-accent/40" : "bg-white/[0.04] text-neutral-300 hover:bg-white/[0.08]"
  }`;

// Per-gram macro basis so any picked food scales to arbitrary grams.
interface Picked {
  name: string;
  perG: { calories: number; protein: number; carbs: number; fat: number };
}

function fromFood(f: Food): Picked {
  return {
    name: f.name,
    perG: {
      calories: f.per100.calories / 100,
      protein: f.per100.protein / 100,
      carbs: f.per100.carbs / 100,
      fat: f.per100.fat / 100,
    },
  };
}

// Rebuild the per-gram basis from a saved entry so it can be re-edited.
function fromEntry(e: MealEntry): Picked {
  const base = e.grams > 0 ? e.grams : 1;
  return {
    name: e.foodName,
    perG: {
      calories: e.calories / base,
      protein: e.protein / base,
      carbs: e.carbs / base,
      fat: e.fat / base,
    },
  };
}

export default function AddFoodSheet({
  open,
  defaultMeal,
  editEntry,
  logDate,
  onClose,
}: {
  open: boolean;
  defaultMeal?: MealType;
  editEntry?: MealEntry | null;
  logDate?: string | null; // YYYY-MM-DD — geçmiş güne ekleme için
  onClose: () => void;
}) {
  const [shown, setShown] = useState(false);
  const [step, setStep] = useState<1 | 2>(1);
  const [picked, setPicked] = useState<Picked | null>(null);
  const [grams, setGrams] = useState(100);
  const [meal, setMeal] = useState<MealType>(defaultMeal ?? "Breakfast");

  const [rawQuery, setRawQuery] = useState("");
  const results = useMemo(() => searchFoods(rawQuery), [rawQuery]);
  const logFood = useLogFood();
  const updateMeal = useUpdateMeal();

  // Trigger slide-up on mount. In edit mode, skip search and open on the amount step.
  useEffect(() => {
    if (open) {
      setShown(true);
      setRawQuery("");
      if (editEntry) {
        setPicked(fromEntry(editEntry));
        setGrams(editEntry.grams);
        setMeal(editEntry.mealType);
        setStep(2);
      } else {
        setStep(1);
        setPicked(null);
        setGrams(100);
        setMeal(defaultMeal ?? "Breakfast");
      }
    }
  }, [open, defaultMeal, editEntry]);

  const close = () => {
    setShown(false);
    setTimeout(onClose, 300);
  };

  const pick = (p: Picked) => {
    setPicked(p);
    setStep(2);
  };

  const preview = useMemo(() => {
    if (!picked) return { calories: 0, protein: 0, carbs: 0, fat: 0 };
    const r = (v: number) => Math.round(v * grams);
    return {
      calories: r(picked.perG.calories),
      protein: r(picked.perG.protein),
      carbs: r(picked.perG.carbs),
      fat: r(picked.perG.fat),
    };
  }, [picked, grams]);

  const submit = () => {
    if (!picked) return;
    const n = (v: number) => Number((v * grams).toFixed(1));
    const body = {
      foodName: picked.name,
      grams,
      calories: n(picked.perG.calories),
      protein: n(picked.perG.protein),
      carbs: n(picked.perG.carbs),
      fat: n(picked.perG.fat),
      mealType: meal,
      ...(logDate ? { logDate } : {}),
    };
    if (editEntry) updateMeal.mutate({ id: editEntry.id, body }, { onSuccess: close });
    else logFood.mutate(body, { onSuccess: close });
  };

  const saving = logFood.isPending || updateMeal.isPending;

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[60]">
      <div
        onClick={close}
        className={`absolute inset-0 bg-ink/85 transition-opacity duration-300 ${
          shown ? "opacity-100" : "opacity-0"
        }`}
      />
      <div
        className={`absolute inset-x-0 bottom-0 mx-auto max-h-[88vh] max-w-md overflow-y-auto rounded-t-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-[var(--shadow-overlay)] transition-transform duration-300 ${
          shown ? "translate-y-0" : "translate-y-full"
        }`}
      >
        <div className="mb-5 flex items-center justify-between gap-3">
          <h2 className="min-w-0 truncate text-lg font-semibold text-neutral-100">
            {step === 1 ? "Yemek ekle" : editEntry ? `Düzenle · ${picked?.name}` : picked?.name}
          </h2>
          <IconButton icon={X} size={19} onClick={close} aria-label="Kapat" />
        </div>

        {step === 1 ? (
          <Step1 rawQuery={rawQuery} setRawQuery={setRawQuery} results={results} onPick={pick} />
        ) : (
          <Step2
            grams={grams}
            setGrams={setGrams}
            meal={meal}
            setMeal={setMeal}
            preview={preview}
            pending={saving}
            submitLabel={editEntry ? "Kaydet" : "Ekle"}
            onSubmit={submit}
          />
        )}
      </div>
    </div>
  );
}

function Step1({
  rawQuery,
  setRawQuery,
  results,
  onPick,
}: {
  rawQuery: string;
  setRawQuery: (v: string) => void;
  results: Food[];
  onPick: (p: Picked) => void;
}) {
  return (
    <div className="space-y-5">
      <div className="relative">
        <Search size={17} className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-500" />
        <input
          autoFocus
          value={rawQuery}
          onChange={(e) => setRawQuery(e.target.value)}
          placeholder="200g tavuk göğsü"
          className="h-11 w-full rounded-xl border border-hair/70 bg-card2 pl-10 pr-3 text-sm text-neutral-100 outline-none transition-colors placeholder:text-neutral-600 focus:border-accent/70 focus:bg-panel"
        />
      </div>

      <div>
        <div className="eyebrow mb-2.5 text-[11px] text-neutral-500">Sık kullanılan</div>
        <div className="grid grid-cols-4 gap-2">
          {FREQUENT_FOODS.map((f) => (
            <button
              key={f.name}
              onClick={() => onPick(fromFood(f))}
              className="flex flex-col items-center gap-1 rounded-xl bg-white/[0.04] p-2.5 text-center transition-colors hover:bg-white/[0.08]"
            >
              <span className="text-2xl">{f.emoji}</span>
              <span className="text-[11px] font-medium leading-tight text-neutral-200">{f.name}</span>
              <span className="text-[11px] tabular-nums text-neutral-500">{f.per100.calories} kcal</span>
            </button>
          ))}
        </div>
      </div>

      {rawQuery.trim().length > 0 && (
        <div>
          <div className="eyebrow mb-2.5 text-[11px] text-neutral-500">Arama sonuçları</div>
          {results.length === 0 ? (
            <p className="py-3 text-center text-sm text-neutral-600">Sonuç yok.</p>
          ) : (
            <ul className="space-y-1.5">
              {results.map((f) => (
                <li key={f.name}>
                  <button
                    onClick={() => onPick(fromFood(f))}
                    className="flex w-full items-center justify-between gap-3 rounded-xl bg-white/[0.04] px-3 py-2.5 text-left transition-colors hover:bg-white/[0.08]"
                  >
                    <span className="flex min-w-0 items-center gap-2 text-sm font-medium text-neutral-100">
                      <span className="text-lg">{f.emoji}</span>
                      <span className="truncate">{f.name}</span>
                    </span>
                    <span className="shrink-0 text-[11px] tabular-nums text-neutral-500">
                      {f.per100.calories} kcal · 100g
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

function Step2({
  grams,
  setGrams,
  meal,
  setMeal,
  preview,
  pending,
  submitLabel,
  onSubmit,
}: {
  grams: number;
  setGrams: (g: number) => void;
  meal: MealType;
  setMeal: (m: MealType) => void;
  preview: { calories: number; protein: number; carbs: number; fat: number };
  pending: boolean;
  submitLabel: string;
  onSubmit: () => void;
}) {
  return (
    <div className="space-y-5">
      <div className="text-center">
        <span className="num text-5xl text-neutral-100">{grams}</span>
        <span className="num ml-1 text-2xl font-normal text-neutral-500">g</span>
      </div>

      <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
        {PORTIONS.map((p) => (
          <button key={p} onClick={() => setGrams(p)} className={choiceCls(grams === p)}>
            {p}
          </button>
        ))}
      </div>

      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-neutral-400">Özel miktar (g)</span>
        <input
          type="number"
          inputMode="numeric"
          value={grams}
          onChange={(e) => setGrams(Math.max(0, Number(e.target.value) || 0))}
          className="h-11 w-full rounded-xl border border-hair/70 bg-card2 px-3 text-sm tabular-nums text-neutral-100 outline-none transition-colors focus:border-accent/70 focus:bg-panel"
        />
      </label>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {MEAL_OPTIONS.map((o) => (
          <button key={o.type} onClick={() => setMeal(o.type)} className={choiceCls(meal === o.type)}>
            {o.tr}
          </button>
        ))}
      </div>

      <div className="grid grid-cols-4 gap-2 rounded-2xl border border-hair/60 bg-card2 p-3.5 text-center">
        <Preview label="kcal" value={preview.calories} className="text-neutral-100" />
        <Preview label="Protein" value={`${preview.protein}g`} className="text-pro/90" />
        <Preview label="Karb" value={`${preview.carbs}g`} className="text-carb/90" />
        <Preview label="Yağ" value={`${preview.fat}g`} className="text-fat/90" />
      </div>

      <button
        onClick={onSubmit}
        disabled={pending || grams <= 0}
        className="h-12 w-full rounded-xl bg-accent text-base font-semibold text-white press hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
      >
        {submitLabel}
      </button>
    </div>
  );
}

function Preview({ label, value, className }: { label: string; value: React.ReactNode; className?: string }) {
  return (
    <div>
      <div className={`num text-lg ${className}`}>{value}</div>
      <div className="eyebrow mt-1 text-[10px] text-neutral-600">{label}</div>
    </div>
  );
}
