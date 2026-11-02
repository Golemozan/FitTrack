import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CalendarRange, Check, ChevronRight, Dumbbell, Flame, Frown, HeartPulse, Meh, Scale, Smile, Sparkles, UtensilsCrossed } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import ErrorBoundary from "../components/ErrorBoundary";
import { HabitsCard } from "../components/Habits";
import { CustomCards } from "../components/HomeWidgets";
import NutritionToday from "../components/NutritionToday";
import { useAchievements } from "../hooks/useHabits";
import { Skeleton } from "../components/ui";
import { useLogCheckin, useTodayCheckins } from "../hooks/useCoach";
import { useNutritionHistory, useNutritionStreak, useTodayMeals } from "../hooks/useNutrition";
import { useSession } from "../hooks/useSession";
import { useToast } from "../hooks/useToast";
import { useWeightStats } from "../hooks/useWeight";
import { useTodaySession } from "../hooks/useWorkout";
import { useSheets } from "../shell/AppShell";
import type { MealEntry } from "../types";

/* Ana sayfa. Sabit üst: haftalık seri (ilk bakışta motivasyon) → bugün ne yendi (kalori + makrolar + öğünler).
   Altı kullanıcının: "Kartlarım" — ekle, kaldır, sürükleyerek sırala (HomeWidgets). */

export default function HomePage() {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)] gap-4 lg:grid-cols-12 lg:gap-5">
      <div className="lg:col-span-12">
        <Greeting />
      </div>
      <div className="min-w-0 lg:col-span-5">
        <ErrorBoundary label="Seri"><StreakCard /></ErrorBoundary>
      </div>
      <div className="min-w-0 lg:col-span-7">
        <ErrorBoundary label="Bugünkü beslenme"><NutritionToday /></ErrorBoundary>
      </div>
      <div className="mt-2 min-w-0 lg:col-span-12">
        <ErrorBoundary label="Kartlarım">
          <CustomCards
            render={{
              mood: () => <MoodCheckCard />,
              habits: () => <HabitsSlot />,
              tiles: () => <DomainTiles />,
              coach: () => <CoachCard />,
            }}
          />
        </ErrorBoundary>
      </div>
    </div>
  );
}

function HabitsSlot() {
  const { open } = useSheets();
  return <HabitsCard onEdit={() => open("habits")} />;
}

/* ---------- Karşılama ---------- */

function Greeting() {
  const session = useSession().data;
  const first = (session?.displayName ?? "").trim().split(/\s+/)[0] || "Merhaba";
  const hour = new Date().getHours();
  const hello = hour < 5 ? "İyi geceler," : hour < 12 ? "Günaydın," : hour < 18 ? "Merhaba," : "İyi akşamlar,";
  return (
    <header className="flex items-center justify-between gap-4">
      <div className="flex min-w-0 items-center gap-3">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-move font-display text-lg font-bold text-navy">
          {first.slice(0, 1).toLocaleUpperCase("tr")}
        </span>
        <div className="min-w-0">
          <p className="text-sm text-neutral-400">{hello}</p>
          <h1 className="truncate font-display text-[26px] font-bold leading-tight sm:text-3xl">{first}</h1>
        </div>
      </div>
      <span className="shrink-0 rounded-full border border-hair bg-panel px-4 py-2.5 text-sm font-semibold capitalize shadow-card">
        {new Date().toLocaleDateString("tr-TR", { weekday: "short", day: "numeric", month: "short" })}
      </span>
    </header>
  );
}

/* ---------- Tek dokunuşla hissiyat ---------- */

// Emoji değil çizgi ikon: emoji her platformda farklı çizilir, ikon her yerde aynı durur.
const FACES = [
  { mood: 4, label: "İyi", icon: Smile, tone: "bg-accent" },
  { mood: 3, label: "İdare", icon: Meh, tone: "bg-sun" },
  { mood: 2, label: "Zor", icon: Frown, tone: "bg-move" },
] as const;

function MoodCheckCard() {
  const today = useTodayCheckins();
  const log = useLogCheckin();
  const toast = useToast();
  const [picked, setPicked] = useState<number | null>(null);
  const [energy, setEnergy] = useState(3);
  const [hunger, setHunger] = useState(3);
  const last = today.data?.[0];

  const save = async () => {
    if (picked == null) return;
    try {
      await log.mutateAsync({ mood: picked, energy, hunger, note: null, context: "general" });
      toast.success("Kaydedildi");
      setPicked(null);
      setEnergy(3);
      setHunger(3);
    } catch {
      toast.error("Kaydedilemedi. Tekrar dene.");
    }
  };

  return (
    <section className="rounded-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-card sm:p-6" aria-label="Bugün nasılsın">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-[17px] font-bold">Bugün nasılsın?</h2>
        {last && !picked && (
          <span className="text-xs text-neutral-500">
            Son: {new Date(last.loggedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
          </span>
        )}
      </div>
      <div className="mt-4 grid grid-cols-3 gap-2.5" role="radiogroup" aria-label="Ruh hali">
        {FACES.map((f) => {
          const active = picked === f.mood;
          return (
            <button
              key={f.mood}
              type="button"
              role="radio"
              aria-checked={active}
              onClick={() => setPicked(active ? null : f.mood)}
              className={`flex min-h-[5.5rem] flex-col items-center justify-center gap-1.5 rounded-[var(--radius-card)] border-2 transition-[background-color,border-color,transform] duration-150 active:translate-y-px ${
                active ? "border-neutral-100 bg-card2" : "border-transparent bg-card2 hover:border-hair"
              }`}
            >
              <span className={`flex h-11 w-11 items-center justify-center rounded-full text-navy ${f.tone}`} aria-hidden="true">
                <f.icon size={24} strokeWidth={2.2} />
              </span>
              <span className="text-sm font-semibold">{f.label}</span>
            </button>
          );
        })}
      </div>

      {picked != null && (
        <div className="mt-5 space-y-4 border-t border-hair pt-5">
          <ScaleRow label="Enerji" low="Bitkin" high="Dinç" value={energy} onChange={setEnergy} />
          <ScaleRow label="Açlık" low="Çok aç" high="Tok" value={hunger} onChange={setHunger} />
          <button
            type="button"
            onClick={save}
            disabled={log.isPending}
            className="flex min-h-12 w-full items-center justify-center rounded-full bg-accent text-[15px] font-semibold text-accentink transition-[filter] hover:brightness-[1.04] disabled:opacity-50"
          >
            {log.isPending ? "Kaydediliyor…" : "Kaydet"}
          </button>
        </div>
      )}
    </section>
  );
}

function ScaleRow({ label, low, high, value, onChange }: { label: string; low: string; high: string; value: number; onChange: (v: number) => void }) {
  return (
    <fieldset>
      <legend className="flex w-full items-baseline justify-between text-sm font-semibold">
        {label}
        <span className="text-xs font-normal text-neutral-500">{low} → {high}</span>
      </legend>
      <div className="mt-2 grid grid-cols-5 gap-1.5" role="radiogroup" aria-label={label}>
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={value === n}
            onClick={() => onChange(n)}
            className={`num min-h-11 rounded-full text-[15px] transition-colors ${value === n ? "bg-navy text-snow" : "bg-card2 text-neutral-300 hover:bg-hair"}`}
          >
            {n}
          </button>
        ))}
      </div>
    </fieldset>
  );
}

/* ---------- Haftalık seri ---------- */

const WEEKDAYS = ["Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz"];

function isoDay(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** Cezasız seri: kaçan gün sönük kalır, haftayı kırmızıya boyamaz; bugün kayıt yoksa soru işareti bekler. */
function StreakCard() {
  const { open } = useSheets();
  const history = useNutritionHistory(14);
  const streak = useNutritionStreak().data?.days ?? 0;

  const week = useMemo(() => {
    const logged = new Set((history.data ?? []).filter((d) => d.mealCount > 0).map((d) => d.date.slice(0, 10)));
    // Son 7 gün, bugün en sağda — serideki sayıyla aynı şeyi göstersin. Takvim haftası pazartesi
    // sıfırlanınca "7 gün seri" yanında tek işaret kalıyordu.
    const now = new Date();
    const todayKey = isoDay(now);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(now);
      d.setDate(now.getDate() - (6 - i));
      const key = isoDay(d);
      const state = logged.has(key) ? "done" : key === todayKey ? "today" : "missed";
      return { label: WEEKDAYS[(d.getDay() + 6) % 7], key, state, isToday: key === todayKey };
    });
  }, [history.data]);

  const loggedToday = week.find((d) => d.isToday)?.state === "done";
  const fire = useAchievements().data?.find((a) => a.key === "ates");
  const toNext = fire?.nextTarget != null ? fire.nextTarget - streak : null;
  const message = streak === 0
    ? "Bugün bir öğün gir, serine başla."
    : loggedToday ? "Bugün kayıtlı. Yarın da gel, seri büyüsün." : "Bugün henüz öğün yok — seriyi koru.";

  return (
    <section className="relative overflow-hidden rounded-[var(--radius-panel)] bg-navy p-5 text-snow sm:p-6" aria-label="Haftalık seri">
      <span aria-hidden="true" className="pointer-events-none absolute -right-12 -top-12 h-40 w-40 rounded-full bg-move/15" />
      <div className="relative flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-snow/55">Seri</p>
          <p className="mt-1 flex items-baseline gap-2">
            <span className="num text-[56px] leading-none">{streak}</span>
            <span className="text-lg font-semibold text-snow/70">gün</span>
          </p>
          <p className="mt-2 text-sm text-snow/70">{message}</p>
        </div>
        <span className={`flex h-14 w-14 shrink-0 items-center justify-center rounded-full ${streak > 0 ? "bg-move text-navy" : "bg-snow/10 text-snow/50"}`}>
          <Flame size={28} strokeWidth={2.2} />
        </span>
      </div>
      {history.isPending ? (
        <Skeleton className="mt-5 h-14 w-full !bg-snow/10" />
      ) : (
        <ol className="relative mt-5 grid grid-cols-7 gap-1.5">
          {week.map((d) => (
            <li key={d.key} className="flex flex-col items-center gap-1.5">
              <DayDot state={d.state} />
              <span className={`text-[11px] font-semibold ${d.isToday ? "text-snow" : "text-snow/45"}`}>{d.label}</span>
            </li>
          ))}
        </ol>
      )}
      {toNext != null && toNext > 0 && (
        <p className="relative mt-4 rounded-2xl bg-snow/10 px-3.5 py-2.5 text-sm">
          <span className="font-semibold text-accent">{toNext} gün</span> sonra <span className="font-semibold">{fire!.title}</span> rozetinin sıradaki kademesi
        </p>
      )}
      <button
        type="button"
        onClick={() => open("weekly")}
        className="relative mt-4 flex min-h-12 w-full items-center justify-center gap-2 rounded-full bg-snow/10 text-[15px] font-semibold text-snow transition-colors hover:bg-snow/15"
      >
        <CalendarRange size={18} /> Haftalık özet
      </button>
    </section>
  );
}

function DayDot({ state }: { state: string }) {
  const base = "flex aspect-square w-full max-w-10 items-center justify-center rounded-full";
  if (state === "done")
    return <span className={`${base} bg-accent text-accentink`} aria-label="Kayıt var"><Check size={17} strokeWidth={3} /></span>;
  // Kaçan gün cezalandırılmaz: renksiz, sessiz bir çizgi. Yeni kullanıcı ilk gün 6 kırmızı ünlem görmesin.
  if (state === "missed")
    return <span className={`${base} bg-snow/10 text-snow/35`} aria-label="Kayıt yok"><span className="h-0.5 w-3 rounded-full bg-current" /></span>;
  if (state === "today")
    return <span className={`${base} border-2 border-dashed border-snow/50 text-snow/70`} aria-label="Bugün, henüz kayıt yok"><span className="text-sm font-bold">?</span></span>;
  return <span className={`${base} bg-snow/10`} aria-label="Gelecek gün" />;
}

/* ---------- Alan kutuları ---------- */

function DomainTiles() {
  const navigate = useNavigate();
  const { open } = useSheets();
  const meals = useTodayMeals();
  const session = useTodaySession();
  const stats = useWeightStats();
  const checkins = useTodayCheckins();

  const entries = (meals.data ? Object.values(meals.data).flat() : []) as MealEntry[];
  const lastMeal = entries.slice().sort((a, b) => b.loggedAt.localeCompare(a.loggedAt))[0];
  const weekly = stats.data?.weeklyChange;
  const lastMood = checkins.data?.[0];

  return (
    <div className="grid grid-cols-2 gap-3">
      <Tile
        tone="bg-move text-navy"
        icon={UtensilsCrossed}
        title="Beslenme"
        sub={entries.length ? `${entries.length} kayıt · son: ${lastMeal?.foodName}` : "Öğün ekle"}
        onClick={() => navigate("/beslenme")}
      />
      <Tile
        tone="bg-lift text-snow"
        icon={Dumbbell}
        title="Antrenman"
        sub={session.data ? `${session.data.name} · ${session.data.exercises.length} hareket` : "Bugün başlat"}
        onClick={() => navigate("/antrenman")}
      />
      <Tile
        tone="bg-flow text-navy"
        icon={Scale}
        title="Kilo"
        big={stats.data?.currentWeight != null ? stats.data.currentWeight.toLocaleString("tr-TR", { minimumFractionDigits: 1, maximumFractionDigits: 1 }) : undefined}
        sub={weekly == null || weekly === 0 ? "Bu hafta değişim yok" : `${weekly > 0 ? "+" : "−"}${Math.abs(weekly).toLocaleString("tr-TR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} kg / hafta`}
        onClick={() => open("weight")}
      />
      <Tile
        tone="bg-lav text-navy"
        icon={HeartPulse}
        title="Durum"
        sub={lastMood ? `Ruh hali ${lastMood.mood}/5 · enerji ${lastMood.energy}/5` : "Bugün kayıt yok"}
        onClick={() => open("checkin")}
      />
    </div>
  );
}

function Tile({ tone, icon: Icon, title, sub, big, onClick }: { tone: string; icon: LucideIcon; title: string; sub: string; big?: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`relative flex min-h-[9.5rem] flex-col justify-between overflow-hidden rounded-[var(--radius-card)] p-4 text-left transition-[transform,filter] duration-150 hover:brightness-[1.03] active:translate-y-px ${tone}`}
    >
      {/* Dekor: köşede tek yumuşak daire — alanı tanınır kılar, bilgi taşımaz. */}
      <span aria-hidden="true" className="pointer-events-none absolute -right-8 -top-8 h-24 w-24 rounded-full bg-snow/20" />
      <span className="relative flex h-10 w-10 items-center justify-center rounded-full bg-snow/30">
        <Icon size={19} strokeWidth={2.2} />
      </span>
      <span className="relative mt-4 block min-w-0">
        {big && <span className="num block text-[26px]">{big}<span className="ml-1 font-sans text-sm font-semibold opacity-70">kg</span></span>}
        <span className="block text-[15px] font-bold">{title}</span>
        <span className="mt-0.5 line-clamp-2 block text-xs font-medium opacity-75">{sub}</span>
      </span>
    </button>
  );
}

/* ---------- Koç ---------- */

function CoachCard() {
  const { open } = useSheets();
  const aiEnabled = useSession().data?.aiEnabled ?? false;
  return (
    <button
      type="button"
      onClick={() => open("coach")}
      className="flex w-full items-center gap-4 rounded-[var(--radius-panel)] border border-hair bg-panel p-5 text-left shadow-card transition-colors hover:bg-card2"
    >
      <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-navy text-accent">
        <Sparkles size={20} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-bold">Koçuna sor</span>
        <span className="block truncate text-sm text-neutral-400">
          {aiEnabled ? "“Akşam ne yesem?” — verine göre cevap verir" : "Koç kapalı · anahtar ekleyerek aç"}
        </span>
      </span>
      <ChevronRight size={18} className="shrink-0 text-neutral-500" />
    </button>
  );
}
