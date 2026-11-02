import { useEffect, useRef, useState } from "react";
import { Apple, BedDouble, BookOpen, Check, CheckCheck, Footprints, GlassWater, Minus, Pencil, PersonStanding, Pill, Plus, Trash2 } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useArchiveHabit, useCreateHabit, useHabits, useLogHabit } from "../hooks/useHabits";
import { useToast } from "../hooks/useToast";
import type { Habit, HabitIcon } from "../types";
import { EmptyState, Skeleton } from "./ui";

/* Alışkanlıklar: to-do listesinin fitness hali. Her gün sıfırdan başlar.
   Hedef 1 → tek dokunuşla işaretlenen daire. Hedef > 1 (su) → dokunulabilir hücreler + −/+ . */

// eslint-disable-next-line react-refresh/only-export-components
export const HABIT_ICONS: Record<HabitIcon, { icon: LucideIcon; label: string; tone: string }> = {
  water: { icon: GlassWater, label: "Su", tone: "bg-flow text-navy" },
  pill: { icon: Pill, label: "Takviye", tone: "bg-lav text-navy" },
  steps: { icon: Footprints, label: "Adım", tone: "bg-lift text-snow" },
  stretch: { icon: PersonStanding, label: "Esneme", tone: "bg-sun text-navy" },
  sleep: { icon: BedDouble, label: "Uyku", tone: "bg-fat text-snow" },
  book: { icon: BookOpen, label: "Okuma", tone: "bg-sun text-navy" },
  apple: { icon: Apple, label: "Beslenme", tone: "bg-move text-navy" },
  check: { icon: CheckCheck, label: "Genel", tone: "bg-accent text-accentink" },
};

const MAX_CELLS = 10;

export function HabitsCard({ onEdit }: { onEdit: () => void }) {
  const habits = useHabits();
  const list = habits.data ?? [];
  const done = list.filter((h) => h.count >= h.target).length;

  return (
    <section className="rounded-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-card sm:p-6" aria-label="Alışkanlıklar">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-[17px] font-bold">Alışkanlıklar</h2>
        <div className="flex items-center gap-1.5">
          {list.length > 0 && (
            <span className="rounded-full bg-card2 px-3 py-1.5 text-xs font-semibold">
              <span className="num">{done}</span> / {list.length}
            </span>
          )}
          <button
            type="button"
            onClick={onEdit}
            aria-label="Alışkanlıkları düzenle"
            className="flex h-10 w-10 items-center justify-center rounded-full text-neutral-400 transition-colors hover:bg-card2 hover:text-neutral-100"
          >
            <Pencil size={17} />
          </button>
        </div>
      </div>

      {habits.isPending ? (
        <div className="mt-4 space-y-2.5"><Skeleton className="h-16 w-full" /><Skeleton className="h-16 w-full" /></div>
      ) : habits.isError ? (
        <EmptyState tone="error" title="Alışkanlıklar yüklenemedi">Bağlantını kontrol edip sayfayı yenile.</EmptyState>
      ) : list.length === 0 ? (
        <div className="mt-4 rounded-[var(--radius-card)] border border-dashed border-hair p-5 text-center">
          <p className="text-sm font-semibold">Takip ettiğin alışkanlık yok</p>
          <p className="mt-1 text-sm text-neutral-400">Su, kreatin, 10 bin adım… Her gün sıfırdan başlar.</p>
          <button type="button" onClick={onEdit} className="mt-3 min-h-11 rounded-full bg-accent px-5 text-sm font-semibold text-accentink">
            Alışkanlık ekle
          </button>
        </div>
      ) : (
        <ul className="mt-4 space-y-2.5">
          {list.map((h) => <HabitRow key={h.id} habit={h} />)}
        </ul>
      )}
    </section>
  );
}

function HabitRow({ habit }: { habit: Habit }) {
  const log = useLogHabit();
  const toast = useToast();
  const meta = HABIT_ICONS[habit.icon] ?? HABIT_ICONS.check;
  const done = habit.count >= habit.target;
  const set = (count: number) =>
    log.mutate({ id: habit.id, count: Math.max(0, count) }, { onError: () => toast.error("Kaydedilemedi") });

  if (habit.target === 1) {
    return (
      <li>
        <button
          type="button"
          onClick={() => set(done ? 0 : 1)}
          aria-pressed={done}
          className={`flex w-full items-center gap-3.5 rounded-[var(--radius-card)] border border-hair p-3.5 text-left transition-colors hover:bg-card2 ${done ? "bg-card2/60" : ""}`}
        >
          <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${meta.tone}`}><meta.icon size={20} /></span>
          <span className="min-w-0 flex-1">
            <span className={`block text-[15px] font-semibold ${done ? "text-neutral-400 line-through decoration-2" : ""}`}>{habit.name}</span>
            <span className="block text-xs text-neutral-500">{done ? "Bugün tamam" : "Bugün yapılmadı"}</span>
          </span>
          <span
            className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full border-2 transition-colors ${done ? "border-transparent bg-accent text-accentink" : "text-transparent"}`}
            style={done ? undefined : { borderColor: "var(--color-rule-2)" }}
            aria-hidden="true"
          >
            <Check size={17} strokeWidth={3} />
          </span>
        </button>
      </li>
    );
  }

  const cells = Math.min(habit.target, MAX_CELLS);
  const perCell = habit.target / cells; // hedef 10'dan büyükse her hücre birden fazla birim
  const rising = useRising(habit.count);
  const color = done ? "var(--color-accent)" : HABIT_FILL[habit.icon] ?? "var(--color-flow)";
  return (
    <li className={`rounded-[var(--radius-card)] border border-hair p-3.5 ${done ? "bg-card2/60" : ""}`}>
      <div className="flex items-center gap-3.5">
        <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${meta.tone}`}><meta.icon size={20} /></span>
        <span className="min-w-0 flex-1">
          <span className="block text-[15px] font-semibold">{habit.name}</span>
          <span className="block text-xs text-neutral-500">
            <span className="num text-neutral-300">{habit.count}</span> / {habit.target} {habit.unit ?? ""}
            {done && " · tamam"}
          </span>
        </span>
        <button type="button" onClick={() => set(habit.count - 1)} disabled={habit.count === 0} aria-label={`${habit.name} azalt`}
          className="flex h-10 w-10 items-center justify-center rounded-full bg-card2 text-neutral-300 transition-colors hover:bg-hair disabled:opacity-35">
          <Minus size={17} />
        </button>
        <button type="button" onClick={() => set(habit.count + 1)} aria-label={`${habit.name} artır`}
          className="flex h-10 w-10 items-center justify-center rounded-full bg-navy text-snow transition-[filter] hover:brightness-125">
          <Plus size={17} />
        </button>
      </div>
      <div className="mt-3 flex gap-1.5" role="group" aria-label={`${habit.name}: ${habit.count} / ${habit.target}`}>
        {Array.from({ length: cells }).map((_, i) => {
          const upTo = Math.round((i + 1) * perCell);
          const filled = habit.count >= upTo;
          const isRising = rising != null && filled && upTo > rising.from && upTo <= rising.to;
          // Birden fazla hücre aynı anda dolarsa (hücreye dokunarak atlama) soldan sağa sırayla yükselir.
          const delay = isRising ? (upTo - rising.from - 1) * 90 : 0;
          return (
            <button
              key={i}
              type="button"
              onClick={() => set(filled && habit.count === upTo ? Math.round(i * perCell) : upTo)}
              aria-label={`${upTo} ${habit.unit ?? ""}`}
              className="relative h-7 flex-1 overflow-hidden rounded-lg bg-track transition-colors hover:bg-hair"
            >
              <span
                aria-hidden="true"
                className="water-fill absolute inset-x-0 bottom-0"
                style={{ height: filled ? "100%" : "0%", backgroundColor: color, color, transitionDelay: `${delay}ms` }}
              >
                {isRising && (
                  <svg className="water-wave" style={{ animationDelay: `${delay}ms` }} viewBox="0 0 80 8" preserveAspectRatio="none">
                    <path d="M0 4 Q10 0 20 4 T40 4 T60 4 T80 4 V8 H0 Z" fill="currentColor" />
                  </svg>
                )}
                {isRising && <span className="water-shine" style={{ animationDelay: `${delay}ms` }} />}
              </span>
            </button>
          );
        })}
      </div>
    </li>
  );
}

const HABIT_FILL: Partial<Record<HabitIcon, string>> = {
  water: "var(--color-flow)", pill: "var(--color-lav)", steps: "var(--color-lift)", stretch: "var(--color-sun)",
  sleep: "var(--color-fat)", book: "var(--color-sun)", apple: "var(--color-move)", check: "var(--color-accent)",
};

/**
 * Sayı arttığında hangi aralığın yeni dolduğunu bir süre tutar — dalga yalnız o hücrelerde oynar.
 * İlk yüklemede (sayfa açılışı) animasyon yok: yalnız kullanıcının kendi dokunuşu suyu yükseltir.
 */
function useRising(count: number) {
  const prev = useRef(count);
  const [rising, setRising] = useState<{ from: number; to: number } | null>(null);
  useEffect(() => {
    if (count > prev.current) {
      setRising({ from: prev.current, to: count });
      const id = window.setTimeout(() => setRising(null), 1400);
      prev.current = count;
      return () => window.clearTimeout(id);
    }
    prev.current = count;
    setRising(null);
  }, [count]);
  return rising;
}

/* ---------- Düzenleme: ekle / arşivle ---------- */

export function HabitEditor() {
  const habits = useHabits();
  const create = useCreateHabit();
  const archive = useArchiveHabit();
  const toast = useToast();
  const [name, setName] = useState("");
  const [icon, setIcon] = useState<HabitIcon>("pill");
  const [target, setTarget] = useState(1);
  const [unit, setUnit] = useState("");

  const add = async () => {
    try {
      await create.mutateAsync({ name: name.trim(), icon, target, unit: target > 1 && unit.trim() ? unit.trim() : null });
      toast.success(`${name.trim()} eklendi`);
      setName("");
      setTarget(1);
      setUnit("");
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } }).response?.data?.error;
      toast.error(msg ?? "Eklenemedi");
    }
  };

  const suggestions = ([
    { name: "Kreatin", icon: "pill", target: 1 },
    { name: "Vitamin", icon: "pill", target: 1 },
    { name: "10 bin adım", icon: "steps", target: 1 },
    { name: "Esneme", icon: "stretch", target: 1 },
    { name: "8 saat uyku", icon: "sleep", target: 1 },
    { name: "Su", icon: "water", target: 8, unit: "bardak" },
  ] as Array<{ name: string; icon: HabitIcon; target: number; unit?: string }>).filter((s) => !(habits.data ?? []).some((h) => h.name.toLocaleLowerCase("tr") === s.name.toLocaleLowerCase("tr")));

  return (
    <div className="mx-auto max-w-xl">
      <h2 className="pr-12 font-display text-2xl font-semibold">Alışkanlıklar</h2>
      <p className="mt-1 text-sm text-neutral-400">Her gün sıfırdan başlar. Kaldırdığın alışkanlığın geçmişi haftalık özette kalır.</p>

      <ul className="mt-5 divide-y divide-hair overflow-hidden rounded-[var(--radius-card)] border border-hair">
        {(habits.data ?? []).map((h) => {
          const meta = HABIT_ICONS[h.icon] ?? HABIT_ICONS.check;
          return (
            <li key={h.id} className="flex items-center gap-3 px-3.5 py-3">
              <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl ${meta.tone}`}><meta.icon size={18} /></span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[15px] font-semibold">{h.name}</span>
                <span className="block text-xs text-neutral-500">{h.target === 1 ? "Günde bir kez" : `Günde ${h.target} ${h.unit ?? ""}`}</span>
              </span>
              <button type="button" onClick={() => archive.mutate(h.id)} aria-label={`${h.name} kaldır`}
                className="flex h-10 w-10 items-center justify-center rounded-full text-neutral-500 transition-colors hover:bg-danger/10 hover:text-danger">
                <Trash2 size={17} />
              </button>
            </li>
          );
        })}
        {(habits.data ?? []).length === 0 && <li className="px-4 py-5 text-sm text-neutral-500">Henüz alışkanlık yok. Aşağıdan ekle.</li>}
      </ul>

      {suggestions.length > 0 && (
        <div className="mt-5">
          <p className="text-xs font-semibold text-neutral-400">Hızlı ekle</p>
          <div className="mt-2 flex flex-wrap gap-2">
            {suggestions.map((s) => (
              <button key={s.name} type="button"
                onClick={() => create.mutate({ name: s.name, icon: s.icon, target: s.target, unit: s.unit ?? null })}
                className="flex min-h-10 items-center gap-1.5 rounded-full border border-hair px-3.5 text-sm font-medium transition-colors hover:bg-card2">
                <Plus size={14} /> {s.name}
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="mt-6 rounded-[var(--radius-card)] bg-card2 p-4">
        <p className="text-sm font-bold">Yeni alışkanlık</p>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={40}
          placeholder="Ör. Omega-3"
          aria-label="Alışkanlık adı"
          className="mt-3 min-h-11 w-full rounded-[var(--radius-input)] border border-hair bg-panel px-3.5 text-[15px] outline-none focus:border-accentfg"
        />
        <div className="mt-3 grid grid-cols-8 gap-1.5" role="radiogroup" aria-label="İkon">
          {(Object.keys(HABIT_ICONS) as HabitIcon[]).map((k) => {
            const m = HABIT_ICONS[k];
            return (
              <button key={k} type="button" role="radio" aria-checked={icon === k} aria-label={m.label} title={m.label}
                onClick={() => setIcon(k)}
                className={`flex aspect-square items-center justify-center rounded-2xl transition-[box-shadow,transform] ${m.tone} ${icon === k ? "ring-2 ring-neutral-100 ring-offset-2 ring-offset-card2" : "opacity-60 hover:opacity-100"}`}>
                <m.icon size={17} />
              </button>
            );
          })}
        </div>
        <div className="mt-3 flex items-center gap-3">
          <span className="text-sm font-semibold">Günlük hedef</span>
          <div className="ml-auto flex items-center gap-2">
            <button type="button" onClick={() => setTarget((t) => Math.max(1, t - 1))} aria-label="Hedefi azalt" className="flex h-10 w-10 items-center justify-center rounded-full bg-panel"><Minus size={16} /></button>
            <span className="num w-8 text-center text-lg">{target}</span>
            <button type="button" onClick={() => setTarget((t) => Math.min(50, t + 1))} aria-label="Hedefi artır" className="flex h-10 w-10 items-center justify-center rounded-full bg-panel"><Plus size={16} /></button>
          </div>
        </div>
        {target > 1 && (
          <input value={unit} onChange={(e) => setUnit(e.target.value)} maxLength={15} placeholder="Birim (ör. bardak, dk)" aria-label="Birim"
            className="mt-3 min-h-11 w-full rounded-[var(--radius-input)] border border-hair bg-panel px-3.5 text-[15px] outline-none focus:border-accentfg" />
        )}
        <button type="button" onClick={add} disabled={!name.trim() || create.isPending}
          className="mt-4 min-h-12 w-full rounded-full bg-accent text-[15px] font-semibold text-accentink disabled:opacity-40">
          {create.isPending ? "Ekleniyor…" : "Ekle"}
        </button>
      </div>
    </div>
  );
}
