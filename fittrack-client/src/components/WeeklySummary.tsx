import { useState } from "react";
import { Check, Dumbbell, Droplets, Flame, Scale, Smile, Sparkles, Target, UtensilsCrossed } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { coachApi } from "../api/coach";
import { useWeeklySummary } from "../hooks/useHabits";
import { useSession } from "../hooks/useSession";
import type { PeriodStats, WeeklySummary as Summary } from "../types";
import { HABIT_ICONS } from "./Habits";
import { EmptyState, Skeleton } from "./ui";

/* Haftalık özet: son 7 gün, önceki 7 günle yan yana. Sayılar sunucudan; kayıt yoksa "—", sıfır gibi
   gösterilmez. Koç yorumu isteğe bağlı ve yalnız AI anahtarı olan kullanıcıda. */

const fmt = (n: number, digits = 0) => n.toLocaleString("tr-TR", { maximumFractionDigits: digits });
const dayLabel = (iso: string) => new Intl.DateTimeFormat("tr-TR", { weekday: "short" }).format(new Date(iso));
const range = (s: PeriodStats) => {
  const f = new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short" });
  return `${f.format(new Date(s.from))} – ${f.format(new Date(s.to))}`;
};

export default function WeeklySummary() {
  const summary = useWeeklySummary();

  if (summary.isPending)
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-8 w-48" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">{Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-24" />)}</div>
        <Skeleton className="h-40 w-full" />
      </div>
    );
  if (summary.isError || !summary.data)
    return <EmptyState tone="error" title="Özet yüklenemedi">Bağlantını kontrol edip tekrar dene.</EmptyState>;

  return <SummaryBody s={summary.data} />;
}

function SummaryBody({ s }: { s: Summary }) {
  const t = s.thisWeek;
  const p = s.previousWeek;
  const delta = (a: number, b: number, unit = "") => {
    const d = a - b;
    if (b === 0 && a === 0) return "Geçen hafta da yok";
    if (d === 0) return "Geçen haftayla aynı";
    return `${d > 0 ? "+" : "−"}${fmt(Math.abs(d))}${unit} geçen haftaya göre`;
  };

  const tiles: Array<{ icon: LucideIcon; tone: string; label: string; value: string; sub: string }> = [
    { icon: Dumbbell, tone: "bg-lift text-snow", label: "Antrenman", value: fmt(t.workouts), sub: delta(t.workouts, p.workouts) },
    { icon: Flame, tone: "bg-lift text-snow", label: "Hacim", value: t.volumeKg ? `${fmt(t.volumeKg)} kg` : "—", sub: `${fmt(t.completedSets)} set tamamlandı` },
    { icon: UtensilsCrossed, tone: "bg-move text-navy", label: "Ort. kalori", value: t.mealDays ? fmt(t.avgCalories) : "—", sub: s.calorieGoal ? `Hedef ${fmt(s.calorieGoal)} kcal` : "Hedef yok" },
    { icon: Target, tone: "bg-pro text-navy", label: "Protein hedefi", value: `${t.proteinGoalDays} / 7`, sub: t.mealDays ? `Ort. ${fmt(t.avgProtein)} g` : "Öğün kaydı yok" },
    { icon: Scale, tone: "bg-flow text-navy", label: "Kilo", value: t.weightChange == null ? "—" : `${t.weightChange > 0 ? "+" : t.weightChange < 0 ? "−" : "±"}${Math.abs(t.weightChange).toLocaleString("tr-TR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} kg`, sub: t.weightEnd != null ? `Son: ${fmt(t.weightEnd, 1)} kg` : "Tartı yok" },
    { icon: Smile, tone: "bg-lav text-navy", label: "Ruh hali", value: t.avgMood == null ? "—" : `${fmt(t.avgMood, 1)} / 5`, sub: t.checkIns ? `${t.checkIns} kayıt` : "Kayıt yok" },
    { icon: Droplets, tone: "bg-flow text-navy", label: "Su", value: t.avgWater == null ? "—" : `${fmt(t.avgWater, 1)}`, sub: "bardak / gün ort." },
    { icon: Check, tone: "bg-accent text-accentink", label: "Alışkanlık", value: t.habitRate == null ? "—" : `%${fmt(t.habitRate * 100)}`, sub: t.habitRate == null || p.habitRate == null ? "Tamamlama oranı" : delta(Math.round(t.habitRate * 100), Math.round(p.habitRate * 100), " puan") },
  ];

  const maxCal = Math.max(s.calorieGoal, ...s.days.map((d) => d.calories), 1);

  return (
    <div className="mx-auto max-w-3xl">
      <div className="pr-12">
        <h2 className="font-display text-2xl font-semibold">Haftalık özet</h2>
        <p className="mt-1 text-sm text-neutral-400">{range(t)} · son 7 gün</p>
      </div>

      <ul className="mt-5 space-y-2 rounded-[var(--radius-card)] bg-navy p-4 text-snow">
        {s.highlights.map((h) => (
          <li key={h} className="flex items-start gap-2.5 text-[15px]">
            <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-accent text-accentink"><Check size={12} strokeWidth={3} /></span>
            {h}
          </li>
        ))}
      </ul>

      <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
        {tiles.map((tile) => (
          <div key={tile.label} className="flex min-h-[7.5rem] flex-col rounded-[var(--radius-card)] border border-hair bg-panel p-3.5 shadow-card">
            <span className={`flex h-8 w-8 items-center justify-center rounded-xl ${tile.tone}`}><tile.icon size={15} /></span>
            <span className="mt-2 text-xs font-semibold text-neutral-400">{tile.label}</span>
            <span className="num mt-1 text-[22px]">{tile.value}</span>
            <span className="mt-auto pt-1 text-[11px] leading-4 text-neutral-500">{tile.sub}</span>
          </div>
        ))}
      </div>

      <section className="mt-4 rounded-[var(--radius-card)] border border-hair bg-panel p-4 shadow-card" aria-label="Günlük kalori">
        <div className="flex items-baseline justify-between gap-3">
          <h3 className="text-sm font-bold">Günlük kalori</h3>
          {s.calorieGoal > 0 && <span className="text-xs text-neutral-500">Çizgi: hedef {fmt(s.calorieGoal)} kcal</span>}
        </div>
        <div className="relative mt-4 h-32">
          {s.calorieGoal > 0 && (
            <span aria-hidden="true" className="absolute inset-x-0 border-t border-neutral-500/50" style={{ bottom: `${(s.calorieGoal / maxCal) * 100}%` }} />
          )}
          <ol className="relative grid h-full grid-cols-7 items-end gap-2">
            {s.days.map((d) => (
              <li key={d.date} className="flex h-full flex-col items-center justify-end" title={`${dayLabel(d.date)}: ${fmt(d.calories)} kcal${d.workout ? " · antrenman" : ""}`}>
                <span className={`w-full max-w-7 rounded-t-[4px] ${d.meals ? "bg-move" : "bg-track"}`} style={{ height: d.meals ? `${Math.max(4, (d.calories / maxCal) * 100)}%` : "4%" }} />
              </li>
            ))}
          </ol>
        </div>
        <ol className="mt-2 grid grid-cols-7 gap-2">
          {s.days.map((d) => (
            <li key={d.date} className="flex flex-col items-center gap-1">
              <span className="text-[11px] font-semibold text-neutral-400">{dayLabel(d.date)}</span>
              <span className={`h-1.5 w-1.5 rounded-full ${d.workout ? "bg-lift" : "bg-transparent"}`} aria-label={d.workout ? "Antrenman var" : undefined} />
            </li>
          ))}
        </ol>
        <p className="mt-2 flex items-center gap-1.5 text-[11px] text-neutral-500"><span className="h-1.5 w-1.5 rounded-full bg-lift" /> antrenman yapılan gün</p>
      </section>

      {s.habits.length > 0 && (
        <section className="mt-4 rounded-[var(--radius-card)] border border-hair bg-panel p-4 shadow-card" aria-label="Alışkanlıklar">
          <h3 className="text-sm font-bold">Alışkanlıklar</h3>
          <ul className="mt-3 space-y-3">
            {s.habits.map((h) => {
              const meta = HABIT_ICONS[h.icon] ?? HABIT_ICONS.check;
              return (
                <li key={h.id} className="flex items-center gap-3">
                  <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${meta.tone}`}><meta.icon size={16} /></span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-baseline justify-between gap-2">
                      <span className="truncate text-sm font-semibold">{h.name}</span>
                      <span className="text-xs text-neutral-500"><span className="num text-neutral-300">{h.daysDone}</span> / 7 gün</span>
                    </span>
                    <span className="mt-1.5 grid grid-cols-7 gap-1">
                      {h.counts.map((c, i) => (
                        <span key={i} className={`h-2.5 rounded-full ${c >= h.target ? "bg-accent" : c > 0 ? "bg-sun" : "bg-track"}`} title={`${dayLabel(s.days[i].date)}: ${c}/${h.target}`} />
                      ))}
                    </span>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <CoachReview s={s} />
    </div>
  );
}

function CoachReview({ s }: { s: Summary }) {
  const aiEnabled = useSession().data?.aiEnabled ?? false;
  const [reply, setReply] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const t = s.thisWeek;

  const ask = async () => {
    setLoading(true);
    setError(null);
    const prompt = [
      "Son 7 günümü kısaca değerlendir: neyi iyi yaptım, gelecek hafta neye odaklanayım? En fazla 5 cümle.",
      `Antrenman ${t.workouts} (geçen hafta ${s.previousWeek.workouts}), ${t.completedSets} set, ${fmt(t.volumeKg)} kg hacim.`,
      `Öğün girilen gün ${t.mealDays}/7, ort. ${fmt(t.avgCalories)} kcal (hedef ${fmt(s.calorieGoal)}), protein hedefi ${t.proteinGoalDays}/7 gün.`,
      t.weightChange != null ? `Kilo değişimi ${fmt(t.weightChange, 1)} kg.` : "Tartı yok.",
      t.avgMood != null ? `Ruh hali ort. ${fmt(t.avgMood, 1)}/5, enerji ${fmt(t.avgEnergy ?? 0, 1)}/5.` : "",
      t.habitRate != null ? `Alışkanlık tamamlama %${fmt(t.habitRate * 100)}.` : "",
    ].filter(Boolean).join(" ");
    try {
      const res = await coachApi.chat([{ role: "user", content: prompt }]);
      setReply(res.reply);
    } catch (e) {
      setError((e as { response?: { data?: { error?: string } } }).response?.data?.error ?? "Koç şu an cevap veremedi.");
    } finally {
      setLoading(false);
    }
  };

  if (!aiEnabled)
    return (
      <p className="mt-4 rounded-[var(--radius-card)] bg-card2 p-4 text-sm text-neutral-400">
        Koçtan haftalık yorum almak için Profil → Hesap ve güvenlik'ten AI anahtarını ekle.
      </p>
    );

  return (
    <section className="mt-4 rounded-[var(--radius-card)] bg-navy p-4 text-snow" aria-label="Koç yorumu">
      {reply ? (
        <>
          <p className="flex items-center gap-2 text-sm font-bold"><Sparkles size={16} className="text-accent" /> Koçun yorumu</p>
          <p className="mt-2 whitespace-pre-line text-[15px] leading-relaxed text-snow/90">{reply}</p>
        </>
      ) : (
        <button type="button" onClick={ask} disabled={loading}
          className="flex min-h-12 w-full items-center justify-center gap-2 rounded-full bg-accent text-[15px] font-semibold text-accentink disabled:opacity-60">
          <Sparkles size={17} /> {loading ? "Koç değerlendiriyor…" : "Koçtan yorum al"}
        </button>
      )}
      {error && <p role="alert" className="mt-2 text-sm text-move">{error}</p>}
    </section>
  );
}
