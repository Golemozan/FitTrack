import { useEffect, useMemo, useState } from "react";
import { ChevronRight, X } from "lucide-react";
import { useAchievements, useMarkAchievementsSeen } from "../hooks/useHabits";
import type { Achievement, AchievementCategory } from "../types";
import { Badge, TIER_NAMES, TierDots } from "./Badge";
import { EmptyState, Segmented, Skeleton } from "./ui";

/* Başarım sistemi arayüzü: tüm rozetler (sayfa), detay, Profil kartı ve yeni başarım kutlaması.
   Kutlama toast değil, tek seferlik bir an: rozet büyür, kısa bir parıltı patlar, kullanıcı kapatır.
   Sıradan kayıtta hiçbir şey olmaz — yalnız gerçekten yeni kademe açılınca. */

const CATEGORIES: Array<{ value: "all" | AchievementCategory; label: string }> = [
  { value: "all", label: "Tümü" },
  { value: "nutrition", label: "Beslenme" },
  { value: "workout", label: "Antrenman" },
  { value: "weight", label: "Kilo" },
  { value: "mind", label: "Durum" },
  { value: "general", label: "Genel" },
];

const fmt = (n: number) => n.toLocaleString("tr-TR", { maximumFractionDigits: 1 });

function levelText(a: Achievement) {
  if (a.level === 0) return "Kilitli";
  if (a.maxLevel === 1) return "Açıldı";
  const name = a.maxLevel === 2 && a.level === 2 ? "Altın" : TIER_NAMES[a.level];
  return a.level === a.maxLevel ? `${name} · tamamlandı` : name;
}

function progress(a: Achievement) {
  if (a.nextTarget == null) return 1;
  const prev = a.level > 0 ? a.tiers[a.level - 1].target : 0;
  return Math.max(0, Math.min(1, (a.value - prev) / (a.nextTarget - prev)));
}

/* ---------- Tüm başarımlar ---------- */

export function AchievementsSheet() {
  const q = useAchievements();
  const [filter, setFilter] = useState<"all" | AchievementCategory>("all");
  const [selected, setSelected] = useState<Achievement | null>(null);
  const list = q.data ?? [];
  const shown = filter === "all" ? list : list.filter((a) => a.category === filter);
  const totalTiers = list.reduce((n, a) => n + a.maxLevel, 0);
  const unlockedTiers = list.reduce((n, a) => n + a.level, 0);

  return (
    <div className="mx-auto max-w-4xl">
      <div className="pr-12">
        <h2 className="font-display text-2xl font-semibold">Başarımlar</h2>
        <p className="mt-1 text-sm text-neutral-400">Girdiğin her kayıt bir şeye doğru ilerler.</p>
      </div>

      {q.isPending ? (
        <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">{Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-44" />)}</div>
      ) : q.isError ? (
        <EmptyState tone="error" title="Başarımlar yüklenemedi">Bağlantını kontrol edip tekrar dene.</EmptyState>
      ) : (
        <>
          <div className="mt-5 rounded-[var(--radius-card)] bg-navy p-4 text-snow">
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-sm font-semibold text-snow/70">Açılan kademe</span>
              <span className="num text-2xl">{unlockedTiers}<span className="font-sans text-base font-semibold text-snow/50"> / {totalTiers}</span></span>
            </div>
            <div className="mt-3 h-2.5 overflow-hidden rounded-full bg-snow/10" role="progressbar" aria-valuemin={0} aria-valuemax={totalTiers} aria-valuenow={unlockedTiers}>
              <div className="h-full rounded-full bg-accent" style={{ width: `${(unlockedTiers / Math.max(1, totalTiers)) * 100}%` }} />
            </div>
          </div>

          <div className="-mx-1 mt-4 overflow-x-auto px-1 no-scrollbar">
            <Segmented value={filter} onChange={setFilter} options={CATEGORIES} size="sm" />
          </div>

          <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
            {shown.map((a) => (
              <li key={a.key}>
                <button
                  type="button"
                  onClick={() => setSelected(a)}
                  className="flex h-full w-full flex-col items-center rounded-[var(--radius-card)] border border-hair bg-panel p-4 text-center shadow-card transition-colors hover:bg-card2"
                >
                  <Badge icon={a.icon} category={a.category} level={a.level} maxLevel={a.maxLevel} size={72} />
                  <span className="mt-3 text-[15px] font-bold leading-tight">{a.title}</span>
                  <span className={`mt-0.5 text-xs ${a.level ? "font-semibold text-neutral-300" : "text-neutral-500"}`}>{levelText(a)}</span>
                  <span className="mt-2"><TierDots level={a.level} maxLevel={a.maxLevel} /></span>
                  {a.nextTarget != null && (
                    <span className="mt-auto w-full pt-3">
                      <span className="block h-1.5 overflow-hidden rounded-full bg-track">
                        <span className="block h-full rounded-full bg-neutral-100" style={{ width: `${progress(a) * 100}%` }} />
                      </span>
                      <span className="mt-1.5 block text-[11px] text-neutral-500">
                        <span className="tnum text-neutral-300">{fmt(Math.min(a.value, a.nextTarget))}</span> / {fmt(a.nextTarget)} {a.unit}
                      </span>
                    </span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {selected && <AchievementDetail a={list.find((x) => x.key === selected.key) ?? selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function AchievementDetail({ a, onClose }: { a: Achievement; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && (e.stopPropagation(), onClose());
    window.addEventListener("keydown", onKey, true);
    return () => window.removeEventListener("keydown", onKey, true);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-[60] flex items-end justify-center sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-label={a.title}>
      <div className="absolute inset-0" style={{ background: "var(--scrim)" }} onClick={onClose} />
      <div className="relative w-full max-w-md rounded-t-[var(--radius-panel)] bg-panel p-6 pb-[calc(env(safe-area-inset-bottom)+1.5rem)] shadow-[var(--shadow-overlay)] sm:rounded-[var(--radius-panel)]">
        <button type="button" onClick={onClose} aria-label="Kapat" className="absolute right-4 top-4 flex h-10 w-10 items-center justify-center rounded-full text-neutral-400 hover:bg-card2 hover:text-neutral-100">
          <X size={20} />
        </button>
        <div className="flex flex-col items-center text-center">
          <Badge icon={a.icon} category={a.category} level={a.level} maxLevel={a.maxLevel} size={112} />
          <h3 className="mt-4 font-display text-xl font-semibold">{a.title}</h3>
          <p className="mt-1 text-sm text-neutral-400">{a.level === a.maxLevel ? "Bütün kademeler açık." : a.description}</p>
          {a.hint && <p className="mt-3 rounded-full bg-card2 px-4 py-2 text-xs font-semibold">{a.hint}</p>}
        </div>
        <ol className="mt-6 space-y-2">
          {a.tiers.map((t) => {
            const open = t.unlockedAt != null;
            const tierName = a.maxLevel === 1 ? "Rozet" : a.maxLevel === 2 && t.level === 2 ? "Altın" : TIER_NAMES[t.level];
            return (
              <li key={t.level} className={`flex items-center gap-3 rounded-2xl p-3 ${open ? "bg-card2" : "border border-dashed border-hair"}`}>
                <Badge icon={a.icon} category={a.category} level={open ? t.level : 0} maxLevel={a.maxLevel} size={40} />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-bold">{tierName}</span>
                  <span className="block text-xs text-neutral-500">{fmt(t.target)} {a.unit}</span>
                </span>
                <span className="text-right text-xs text-neutral-400">
                  {open ? new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short", year: "numeric" }).format(new Date(t.unlockedAt!)) : `${fmt(Math.min(a.value, t.target))} / ${fmt(t.target)}`}
                </span>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}

/* ---------- Profil kartı ---------- */

export function AchievementsCard({ onOpen }: { onOpen: () => void }) {
  const q = useAchievements();
  const list = q.data ?? [];
  const recent = useMemo(
    () =>
      list
        .filter((a) => a.level > 0)
        .map((a) => ({ a, at: Math.max(...a.tiers.filter((t) => t.unlockedAt).map((t) => new Date(t.unlockedAt!).getTime())) }))
        .sort((x, y) => y.at - x.at)
        .slice(0, 4)
        .map((x) => x.a),
    [list],
  );
  // Sıradaki hedef: kademesine en yakın kilitli/eksik başarım.
  const next = useMemo(() => list.filter((a) => a.nextTarget != null && !a.hint).sort((x, y) => progress(y) - progress(x))[0], [list]);
  const unlocked = list.reduce((n, a) => n + a.level, 0);
  const total = list.reduce((n, a) => n + a.maxLevel, 0);

  return (
    <button type="button" onClick={onOpen} className="w-full rounded-[var(--radius-panel)] border border-hair bg-panel p-5 text-left shadow-card transition-colors hover:bg-card2 sm:p-6">
      <span className="flex items-center justify-between gap-3">
        <span className="text-[15px] font-bold">Başarımlar</span>
        <span className="flex items-center gap-1 text-sm font-semibold text-neutral-400">
          <span className="tnum">{unlocked}/{total}</span> <ChevronRight size={17} />
        </span>
      </span>
      {q.isPending ? (
        <Skeleton className="mt-4 h-16 w-full" />
      ) : recent.length === 0 ? (
        <span className="mt-3 block text-sm text-neutral-400">İlk rozetin bir kayıt uzağında — bir öğün ya da tartı gir.</span>
      ) : (
        <span className="mt-4 flex items-center gap-2">
          {recent.map((a) => <Badge key={a.key} icon={a.icon} category={a.category} level={a.level} maxLevel={a.maxLevel} size={56} />)}
        </span>
      )}
      {next && (
        <span className="mt-4 block rounded-2xl bg-card2 p-3">
          <span className="flex items-baseline justify-between gap-2 text-xs">
            <span className="font-semibold">Sıradaki: {next.title}</span>
            <span className="tnum text-neutral-400">{fmt(Math.min(next.value, next.nextTarget!))} / {fmt(next.nextTarget!)} {next.unit}</span>
          </span>
          <span className="mt-2 block h-1.5 overflow-hidden rounded-full bg-track">
            <span className="block h-full rounded-full bg-accent" style={{ width: `${progress(next) * 100}%` }} />
          </span>
        </span>
      )}
    </button>
  );
}

/* ---------- Yeni başarım kutlaması ---------- */

/**
 * Yeni açılan kademeleri bir kez kutlar. Birden fazlaysa sırayla gösterir; son "Harika"da sunucuya
 * "görüldü" gider ve bir daha çıkmaz. Kapatmak (dışına dokunmak) da görüldü sayılır.
 */
export function AchievementCelebration() {
  const q = useAchievements();
  const seen = useMarkAchievementsSeen();
  const fresh = useMemo(() => (q.data ?? []).filter((a) => a.isNew), [q.data]);
  const [index, setIndex] = useState(0);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    if (fresh.length > 0) {
      setIndex(0);
      setDismissed(false);
    }
  }, [fresh.length]);

  if (fresh.length === 0 || dismissed) return null;
  const a = fresh[Math.min(index, fresh.length - 1)];
  const last = index >= fresh.length - 1;
  const close = () => {
    setDismissed(true);
    seen.mutate();
  };
  const tier = a.maxLevel === 1 ? "Yeni rozet" : `${a.maxLevel === 2 && a.level === 2 ? "Altın" : TIER_NAMES[a.level]} kademe`;

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center p-6" role="dialog" aria-modal="true" aria-label={`Yeni başarım: ${a.title}`}>
      <div className="absolute inset-0" style={{ background: "var(--scrim)" }} onClick={close} />
      <div key={a.key} className="celebrate-in relative w-full max-w-sm overflow-hidden rounded-[var(--radius-panel)] bg-navy px-6 pb-6 pt-8 text-center text-snow shadow-[var(--shadow-overlay)]">
        <span className="text-xs font-bold uppercase tracking-[0.18em] text-accent">Yeni başarım</span>
        <div className="relative mx-auto mt-5 flex h-36 w-36 items-center justify-center">
          <span className="burst" aria-hidden="true">{Array.from({ length: 12 }).map((_, i) => <i key={i} style={{ ["--a" as string]: `${i * 30}deg` }} />)}</span>
          <span className="pop-once"><Badge icon={a.icon} category={a.category} level={a.level} maxLevel={a.maxLevel} size={132} /></span>
        </div>
        <h2 className="mt-4 font-display text-2xl font-semibold">{a.title}</h2>
        <p className="mt-1 text-sm font-semibold text-snow/70">{tier}</p>
        <p className="mt-3 text-sm text-snow/60">
          {a.tiers[a.level - 1] ? `${fmt(a.tiers[a.level - 1].target)} ${a.unit}` : ""}
          {a.nextTarget != null ? ` · sıradaki ${fmt(a.nextTarget)} ${a.unit}` : " · bütün kademeler açık"}
        </p>
        <button
          type="button"
          onClick={() => (last ? close() : setIndex((i) => i + 1))}
          className="mt-6 min-h-12 w-full rounded-full bg-accent text-[15px] font-semibold text-accentink"
          autoFocus
        >
          {last ? "Harika" : `Sonraki (${index + 1}/${fresh.length})`}
        </button>
      </div>
    </div>
  );
}
