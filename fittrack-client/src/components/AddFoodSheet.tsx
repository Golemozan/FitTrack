import { useEffect, useMemo, useState } from "react";
import { Search, X } from "lucide-react";
import { useLogFood, useUpdateMeal } from "../hooks/useNutrition";
import { FREQUENT_FOODS, searchFoods } from "../data/frequentFoods";
import type { Food } from "../data/frequentFoods";
import type { MealEntry, MealType } from "../types";

const MEAL_OPTIONS: { tr: string; type: MealType }[] = [
  { tr: "Kahvaltı", type: "Breakfast" },
  { tr: "Öğle", type: "Lunch" },
  { tr: "Akşam", type: "Dinner" },
  { tr: "Atıştırma", type: "Snack" },
];

const PORTIONS = [50, 100, 150, 200, 250, 300];

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
  onClose,
}: {
  open: boolean;
  defaultMeal?: MealType;
  editEntry?: MealEntry | null;
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
    };
    if (editEntry) updateMeal.mutate({ id: editEntry.id, body }, { onSuccess: close });
    else logFood.mutate(body, { onSuccess: close });
  };

  const saving = logFood.isPending || updateMeal.isPending;

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[60]">
      {/* backdrop */}
      <div
        onClick={close}
        className={`absolute inset-0 bg-black/50 transition-opacity duration-300 ${
          shown ? "opacity-100" : "opacity-0"
        }`}
      />
      {/* sheet */}
      <div
        className={`absolute inset-x-0 bottom-0 mx-auto max-h-[88vh] max-w-md overflow-y-auto rounded-t-3xl bg-white p-5 shadow-2xl transition-transform duration-300 dark:bg-card ${
          shown ? "translate-y-0" : "translate-y-full"
        }`}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">
            {step === 1 ? "Yemek ekle" : editEntry ? `Düzenle · ${picked?.name}` : picked?.name}
          </h2>
          <button onClick={close} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            <X size={22} />
          </button>
        </div>

        {step === 1 ? (
          <Step1
            rawQuery={rawQuery}
            setRawQuery={setRawQuery}
            results={results}
            onPick={pick}
          />
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
    <div className="space-y-4">
      <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 dark:border-white/10 dark:bg-card2">
        <Search size={18} className="text-slate-400" />
        <input
          autoFocus
          value={rawQuery}
          onChange={(e) => setRawQuery(e.target.value)}
          placeholder="200g tavuk göğsü"
          className="w-full bg-transparent text-sm outline-none"
        />
      </div>

      {/* frequent foods grid */}
      <div>
        <div className="mb-2 text-xs font-semibold uppercase text-slate-400">Sık kullanılan</div>
        <div className="grid grid-cols-4 gap-2">
          {FREQUENT_FOODS.map((f) => (
            <button
              key={f.name}
              onClick={() =>
                onPick({
                  name: f.name,
                  perG: {
                    calories: f.per100.calories / 100,
                    protein: f.per100.protein / 100,
                    carbs: f.per100.carbs / 100,
                    fat: f.per100.fat / 100,
                  },
                })
              }
              className="flex flex-col items-center gap-1 rounded-xl bg-slate-50 p-2 text-center transition-colors hover:bg-slate-100 dark:bg-card2 dark:hover:bg-white/10"
            >
              <span className="text-2xl">{f.emoji}</span>
              <span className="text-[11px] font-medium leading-tight">{f.name}</span>
              <span className="text-[10px] text-slate-400">{f.per100.calories} kcal</span>
            </button>
          ))}
        </div>
      </div>

      {/* search results (local, instant) */}
      {rawQuery.trim().length > 0 && (
        <div>
          <div className="mb-2 text-xs font-semibold uppercase text-slate-400">Arama sonuçları</div>
          {results.length === 0 ? (
            <p className="py-3 text-center text-sm text-slate-400">Sonuç yok.</p>
          ) : (
            <ul className="space-y-1">
              {results.map((f) => (
                <li key={f.name}>
                  <button
                    onClick={() => onPick(fromFood(f))}
                    className="flex w-full items-center justify-between rounded-xl bg-slate-50 px-3 py-2 text-left hover:bg-slate-100 dark:bg-card2 dark:hover:bg-white/10"
                  >
                    <span className="flex items-center gap-2 text-sm font-medium">
                      <span className="text-lg">{f.emoji}</span>
                      {f.name}
                    </span>
                    <span className="text-xs text-slate-400">
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
        <div className="num text-5xl text-accent">{grams} <span className="text-2xl text-neutral-400">g</span></div>
      </div>

      <div className="grid grid-cols-6 gap-2">
        {PORTIONS.map((p) => (
          <button
            key={p}
            onClick={() => setGrams(p)}
            className={`rounded-xl py-2 text-sm font-semibold transition-colors ${
              grams === p
                ? "bg-accent text-accentink"
                : "bg-black/[0.05] text-neutral-600 dark:bg-card2 dark:text-neutral-300"
            }`}
          >
            {p}
          </button>
        ))}
      </div>

      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-500 dark:text-slate-400">Özel miktar (g)</span>
        <input
          type="number"
          value={grams}
          onChange={(e) => setGrams(Math.max(0, Number(e.target.value) || 0))}
          className="rounded-xl border border-black/10 bg-white px-3 py-2 outline-none focus:border-accent dark:border-hair dark:bg-card2"
        />
      </label>

      {/* meal type pill toggle */}
      <div className="grid grid-cols-4 gap-2">
        {MEAL_OPTIONS.map((o) => (
          <button
            key={o.type}
            onClick={() => setMeal(o.type)}
            className={`rounded-full py-2 text-xs font-semibold transition-colors ${
              meal === o.type
                ? "bg-accent text-accentink"
                : "bg-black/[0.05] text-neutral-600 dark:bg-card2 dark:text-neutral-300"
            }`}
          >
            {o.tr}
          </button>
        ))}
      </div>

      {/* live macro preview */}
      <div className="grid grid-cols-4 gap-2 rounded-2xl bg-black/[0.04] p-3 text-center dark:bg-card2">
        <Preview label="kcal" value={preview.calories} className="text-neutral-900 dark:text-neutral-100" />
        <Preview label="Protein" value={`${preview.protein}g`} className="text-pro" />
        <Preview label="Karb" value={`${preview.carbs}g`} className="text-carb" />
        <Preview label="Yağ" value={`${preview.fat}g`} className="text-fat" />
      </div>

      <button
        onClick={onSubmit}
        disabled={pending || grams <= 0}
        className="w-full rounded-2xl bg-accent py-3 text-base font-bold text-accentink shadow-glow transition-all hover:brightness-110 disabled:opacity-40 disabled:shadow-none"
      >
        {submitLabel}
      </button>
    </div>
  );
}

function Preview({ label, value, className }: { label: string; value: React.ReactNode; className?: string }) {
  return (
    <div>
      <div className={`text-lg font-bold ${className}`}>{value}</div>
      <div className="text-[10px] uppercase text-slate-400">{label}</div>
    </div>
  );
}
