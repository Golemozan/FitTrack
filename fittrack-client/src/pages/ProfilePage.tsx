import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { CalendarRange, CheckCheck, ChevronRight, Clock3, HeartPulse, KeyRound, LogOut, Moon, Scale, Target } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { checkinApi } from "../api/coach";
import { Skeleton } from "../components/ui";
import { AchievementsCard } from "../components/Achievements";
import { useTheme } from "../hooks/useTheme";
import { useNutritionStreak } from "../hooks/useNutrition";
import { useLogout, useSession } from "../hooks/useSession";
import { useWeightStats } from "../hooks/useWeight";
import { useRecentSessions } from "../hooks/useWorkout";
import { useSheets } from "../shell/AppShell";

/* Profil: haftayı puan değil yumuşak çubuklar olarak gösterir; ayarlar renkli ikonlu tek liste. */

export default function ProfilePage() {
  const session = useSession().data;
  const { open } = useSheets();
  const logout = useLogout();
  const { theme, toggle } = useTheme();
  const name = session?.displayName ?? "";

  return (
    <div className="mx-auto grid max-w-3xl grid-cols-[minmax(0,1fr)] gap-4 lg:gap-5">
      <header className="flex items-center gap-4">
        <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-move font-display text-2xl font-bold text-navy">
          {name.slice(0, 1).toLocaleUpperCase("tr") || "?"}
        </span>
        <div className="min-w-0">
          <h1 className="truncate font-display text-[26px] font-bold leading-tight">{name}</h1>
          <p className="truncate text-sm text-neutral-400">{session?.email}</p>
        </div>
      </header>

      <StatTrio />
      <AchievementsCard onOpen={() => open("achievements")} />
      <MoodWeek />

      <section className="overflow-hidden rounded-[var(--radius-panel)] border border-hair bg-panel shadow-card" aria-label="Ayarlar">
        <ul className="divide-y divide-hair">
          <Row icon={CalendarRange} tone="bg-accent text-accentink" label="Haftalık özet" hint="Son 7 gün, geçen haftayla karşılaştırma" onClick={() => open("weekly")} />
          <Row icon={CheckCheck} tone="bg-flow text-navy" label="Alışkanlıklar" hint="Su, takviye, adım…" onClick={() => open("habits")} />
          <Row icon={Scale} tone="bg-flow text-navy" label="Kilo takibi" hint="Geçmiş ve grafik" onClick={() => open("weight")} />
          <Row icon={HeartPulse} tone="bg-lav text-navy" label="Durum geçmişi" hint="Ruh hali, enerji, açlık" onClick={() => open("checkin")} />
          <Row icon={Target} tone="bg-sun text-navy" label="Hedefler ve profil" hint="Kalori, makro, boy" onClick={() => open("goals")} />
          <Row icon={Clock3} tone="bg-lift text-snow" label="Koç geçmişi" onClick={() => open("history")} />
          <Row icon={KeyRound} tone="bg-card2 text-neutral-300" label="Hesap ve güvenlik" hint="Parola, AI anahtarı" onClick={() => open("account")} />
          <li>
            <label className="flex min-h-16 cursor-pointer items-center gap-3.5 px-4 sm:px-5">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-navy text-snow"><Moon size={18} /></span>
              <span className="flex-1 text-[15px] font-semibold">Koyu tema</span>
              <Switch checked={theme === "dark"} onChange={toggle} label="Koyu tema" />
            </label>
          </li>
        </ul>
      </section>

      <button
        type="button"
        onClick={() => logout.mutate()}
        disabled={logout.isPending}
        className="flex min-h-14 items-center justify-center gap-2 rounded-full bg-move/15 text-[15px] font-semibold text-danger transition-colors hover:bg-move/25 disabled:opacity-50"
      >
        <LogOut size={18} /> Çıkış yap
      </button>
    </div>
  );
}

function StatTrio() {
  const streak = useNutritionStreak();
  const sessions = useRecentSessions(30);
  const stats = useWeightStats();
  const weekAgo = Date.now() - 7 * 86400_000;
  const workoutsThisWeek = (sessions.data ?? []).filter((s) => new Date(s.loggedAt).getTime() >= weekAgo).length;
  const change = stats.data?.weeklyChange;

  const items = [
    { value: streak.data?.days ?? 0, label: "gün seri", tone: "bg-accent text-accentink" },
    { value: workoutsThisWeek, label: "antrenman / 7 gün", tone: "bg-panel border border-hair shadow-card" },
    { value: change == null ? "—" : `${change > 0 ? "+" : change < 0 ? "−" : ""}${Math.abs(change).toLocaleString("tr-TR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}`, label: "kg / hafta", tone: "bg-panel border border-hair shadow-card" },
  ];

  return (
    <div className="grid grid-cols-3 gap-3">
      {items.map((it) => (
        <div key={it.label} className={`flex min-h-[6.5rem] flex-col justify-between rounded-[var(--radius-card)] p-4 ${it.tone}`}>
          {streak.isPending ? <Skeleton className="h-8 w-12" /> : <span className="num text-[28px]">{it.value}</span>}
          <span className="text-xs font-semibold leading-tight opacity-80">{it.label}</span>
        </div>
      ))}
    </div>
  );
}

const DAY_LABELS = ["Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz"];

/** Bu haftanın ruh hali — günün ortalaması çubuk boyu, renk: iyi limon · orta güneş · zor mercan. */
function MoodWeek() {
  const recent = useQuery({ queryKey: ["checkin", "recent", 60], queryFn: () => checkinApi.recent(60) });

  const days = useMemo(() => {
    const now = new Date();
    const monday = new Date(now);
    monday.setHours(0, 0, 0, 0);
    monday.setDate(now.getDate() - ((now.getDay() + 6) % 7));
    return DAY_LABELS.map((label, i) => {
      const start = new Date(monday);
      start.setDate(monday.getDate() + i);
      const end = new Date(start);
      end.setDate(start.getDate() + 1);
      const moods = (recent.data ?? [])
        .filter((c) => {
          const t = new Date(c.loggedAt).getTime();
          return t >= start.getTime() && t < end.getTime();
        })
        .map((c) => c.mood);
      const avg = moods.length ? moods.reduce((a, b) => a + b, 0) / moods.length : null;
      return { label, avg };
    });
  }, [recent.data]);

  const range = (() => {
    const now = new Date();
    const monday = new Date(now);
    monday.setDate(now.getDate() - ((now.getDay() + 6) % 7));
    const sunday = new Date(monday);
    sunday.setDate(monday.getDate() + 6);
    const f = new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short" });
    return `${f.format(monday)} – ${f.format(sunday)}`;
  })();

  return (
    <section className="rounded-[var(--radius-panel)] bg-navy p-5 text-snow sm:p-6" aria-label="Bu hafta ruh hali">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-[15px] font-bold">Bu hafta ruh hali</h2>
        <span className="text-xs text-snow/55">{range}</span>
      </div>
      <ol className="mt-5 grid h-32 grid-cols-7 items-end gap-2.5">
        {days.map((d) => {
          const tone = d.avg == null ? "bg-snow/10" : d.avg >= 4 ? "bg-accent" : d.avg >= 3 ? "bg-sun" : "bg-move";
          const h = d.avg == null ? 6 : 22 + ((d.avg - 1) / 4) * 78;
          return (
            <li key={d.label} className="flex h-full flex-col items-center justify-end gap-2">
              <span
                className={`w-full max-w-9 rounded-full ${tone}`}
                style={{ height: `${h}%` }}
                title={d.avg == null ? `${d.label}: kayıt yok` : `${d.label}: ${d.avg.toFixed(1)}/5`}
              />
              <span className="text-[11px] font-semibold text-snow/55">{d.label}</span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function Row({ icon: Icon, tone, label, hint, onClick }: { icon: LucideIcon; tone: string; label: string; hint?: string; onClick: () => void }) {
  return (
    <li>
      <button type="button" onClick={onClick} className="flex min-h-16 w-full items-center gap-3.5 px-4 text-left transition-colors hover:bg-card2 sm:px-5">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl ${tone}`}>
          <Icon size={18} />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-[15px] font-semibold">{label}</span>
          {hint && <span className="block truncate text-xs text-neutral-500">{hint}</span>}
        </span>
        <ChevronRight size={18} className="shrink-0 text-neutral-500" />
      </button>
    </li>
  );
}

function Switch({ checked, onChange, label }: { checked: boolean; onChange: () => void; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={(e) => {
        e.preventDefault();
        onChange();
      }}
      className={`relative h-8 w-14 shrink-0 rounded-full transition-colors duration-200 ${checked ? "bg-navy" : "bg-track"}`}
    >
      <span
        className={`absolute top-1 h-6 w-6 rounded-full transition-[left,background-color] duration-200 ${checked ? "left-7 bg-accent" : "left-1 bg-snow shadow-card"}`}
      />
    </button>
  );
}
