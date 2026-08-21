import { useState } from "react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { ArrowDown, ArrowUp, Check, Pencil, Scale, Trash2, X } from "lucide-react";
import {
  useDeleteWeight,
  useLogWeight,
  useRecentWeightLogs,
  useUpdateWeight,
  useWeightHistory,
  useWeightStats,
  useWeightToday,
} from "../hooks/useWeight";
import { useToast } from "../hooks/useToast";
import {
  Button,
  Card,
  EmptyState,
  Input,
  ListSkeleton,
  SectionTitle,
  Skeleton,
  Stat,
} from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";
import type { WeightLog } from "../types";

const RANGES = [7, 30, 90] as const;

export default function WeightSection() {
  const [days, setDays] = useState<number>(30);
  const stats = useWeightStats();
  const history = useWeightHistory(days);
  const recent = useRecentWeightLogs(10);
  const todayLog = useWeightToday();
  const logWeight = useLogWeight();
  const toast = useToast();

  const [editing, setEditing] = useState(false);
  const [weight, setWeight] = useState("");
  const [notes, setNotes] = useState("");
  const [logDate, setLogDate] = useState(() => new Date().toISOString().slice(0, 10)); // today as YYYY-MM-DD

  const s = stats.data;
  const fmt = (n: number | null | undefined, unit = " kg") =>
    n == null ? "—" : `${n.toFixed(1)}${unit}`;

  const chartData = (history.data ?? []).map((p) => ({
    date: new Date(p.loggedAt).toLocaleDateString("tr-TR", { month: "short", day: "numeric" }),
    kg: p.weightKg,
  }));

  const avg = chartData.length
    ? chartData.reduce((a, p) => a + p.kg, 0) / chartData.length
    : null;

  const grid = "var(--color-rule)";
  const axis = "var(--color-muted)";

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!weight) return;
    // build ISO date from the date picker value (local time → UTC midnight)
    const ts = new Date(logDate + "T00:00:00").toISOString();
    logWeight.mutate(
      { weightKg: Number(weight), notes: notes || null, loggedAt: ts },
      {
        onSuccess: () => {
          toast.success("Kilo kaydedildi");
          setWeight("");
          setNotes("");
          setEditing(false);
        },
        onError: () => toast.error("Kaydedilemedi"),
      }
    );
  };

  const weekly = s?.weeklyChange;
  const hasWeekly = weekly != null && weekly !== 0;
  const weeklyDown = (weekly ?? 0) < 0;

  return (
    <section id="kilo" className="scroll-mt-24">
      <SectionHeader icon={Scale} title="Kilo" />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* HERO */}
        <Card className="flex items-center justify-center lg:col-span-1">
          {stats.isLoading ? (
            <Skeleton className="h-20 w-full" />
          ) : s?.currentWeight == null ? (
            <EmptyState icon={Scale} title="İlk kilonuzu kaydedin">
              Takibe başlamak için aşağıdan kilonu gir.
            </EmptyState>
          ) : (
            <div className="flex flex-col items-center gap-1 text-center">
              <div className="num text-6xl text-accent">
                {s.currentWeight.toFixed(1)}
                <span className="ml-1 text-2xl text-neutral-400">kg</span>
              </div>
              {hasWeekly && (
                <div
                  className={`flex items-center gap-1 text-sm font-medium ${
                    weeklyDown ? "text-loss" : "text-gain"
                  }`}
                >
                  {weeklyDown ? <ArrowDown size={16} /> : <ArrowUp size={16} />}
                  {weekly! > 0 ? "+" : ""}{weekly!.toFixed(1)} kg bu hafta
                </div>
              )}
              {s.totalChange != null && (
                <div className="text-xs text-neutral-400">
                  Başlangıçtan: {s.totalChange > 0 ? "+" : ""}{s.totalChange.toFixed(1)} kg
                </div>
              )}
            </div>
          )}
        </Card>

        {/* CHART */}
        <Card className="lg:col-span-2">
          <div className="mb-3 flex items-center justify-between">
            <SectionTitle accent="orange">Grafik</SectionTitle>
            <div className="flex gap-1">
              {RANGES.map((r) => (
                <button
                  key={r}
                  onClick={() => setDays(r)}
                  className={`rounded-lg px-2 py-1 text-xs font-medium ${
                    days === r ? "bg-accent text-accentink" : "bg-card2 text-neutral-400"
                  }`}
                >
                  {r} gün
                </button>
              ))}
            </div>
          </div>
          {history.isLoading ? (
            <Skeleton className="h-56 w-full" />
          ) : chartData.length === 0 ? (
            <EmptyState icon={Scale}>Grafik için veri yok.</EmptyState>
          ) : (
            <div className="h-56 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={chartData} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={grid} />
                  {avg != null && (
                    <ReferenceLine
                      y={avg}
                      stroke={axis}
                      strokeDasharray="4 4"
                      strokeOpacity={0.6}
                      label={{ value: `ort ${avg.toFixed(1)}`, position: "insideTopRight", fontSize: 10, fill: axis }}
                    />
                  )}
                  <XAxis
                    dataKey="date"
                    fontSize={11}
                    stroke={axis}
                    tickLine={false}
                    interval={Math.max(0, Math.floor(chartData.length / 5))}
                  />
                  <YAxis
                    domain={["dataMin - 1", "dataMax + 1"]}
                    fontSize={11}
                    stroke={axis}
                    tickLine={false}
                    width={40}
                  />
                  <Tooltip
                    contentStyle={{
                      background: "var(--color-surface-raised)",
                      color: "var(--color-ink)",
                      border: "1px solid var(--color-border-strong)",
                      borderRadius: "var(--radius-input)",
                      fontSize: 12,
                    }}
                  />
                  <Area
                    type="monotone"
                    dataKey="kg"
                    name="Kilo (kg)"
                    stroke="var(--color-accent)"
                    strokeWidth={2.5}
                    fill="var(--color-accent-soft)"
                    fillOpacity={0.55}
                    dot={{ r: 2.5, fill: "var(--color-accent)" }}
                    activeDot={{ r: 5 }}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </div>

      {/* STATS ROW */}
      <div className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Stat label="En Düşük" value={fmt(s?.lowestWeight)} />
        <Stat label="En Yüksek" value={fmt(s?.highestWeight)} />
        <Stat label="Başlangıç" value={fmt(s?.startWeight)} />
        <Stat
          label="Toplam Δ"
          value={
            s?.totalChange == null ? (
              "—"
            ) : (
              <span className={s.totalChange <= 0 ? "text-loss" : "text-gain"}>
                {s.totalChange > 0 ? "+" : ""}
                {s.totalChange.toFixed(1)} kg
              </span>
            )
          }
        />
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
        {/* RECENT ENTRIES */}
        <Card>
          <SectionTitle accent="orange">Son kayıtlar</SectionTitle>
          {recent.isLoading ? (
            <ListSkeleton rows={4} />
          ) : recent.data?.length === 0 ? (
            <EmptyState>Kayıt yok.</EmptyState>
          ) : (
            <ul className="divide-y divide-white/10">
              {recent.data?.map((log) => (
                <WeightRow key={log.id} log={log} />
              ))}
            </ul>
          )}
        </Card>

        {/* LOG ENTRY */}
        <Card>
          <SectionTitle accent="orange">Kilo Gir</SectionTitle>
          {todayLog.data && !editing ? (
            <div className="flex items-center justify-between">
              <div>
                <span className="text-lg font-semibold">{todayLog.data.weightKg} kg</span>
                {todayLog.data.notes && (
                  <span className="ml-2 text-sm text-neutral-400">{todayLog.data.notes}</span>
                )}
              </div>
              <Button
                accent="orange"
                variant="ghost"
                onClick={() => {
                  setWeight(String(todayLog.data!.weightKg));
                  setNotes(todayLog.data!.notes ?? "");
                  setLogDate(new Date(todayLog.data!.loggedAt).toISOString().slice(0, 10));
                  setEditing(true);
                }}
              >
                Düzenle
              </Button>
            </div>
          ) : (
            <form onSubmit={submit} className="space-y-3">
              <div className="flex items-end gap-2">
                <Input
                  label="Kilo (kg)"
                  type="number"
                  step="0.1"
                  value={weight}
                  onChange={(e) => setWeight(e.target.value)}
                />
                <Input label="Not" value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Opsiyonel" />
                <Button type="submit" accent="orange" disabled={logWeight.isPending}>
                  Kaydet
                </Button>
              </div>
              <div>
                <label className="mb-1 block text-xs text-neutral-400">Tarih</label>
                <input
                  type="date"
                  value={logDate}
                  onChange={(e) => setLogDate(e.target.value)}
                  className="w-full rounded-lg border border-hair bg-card2 px-3 py-2 text-sm text-neutral-200 outline-none focus:border-accent [color-scheme:dark]"
                />
                <p className="mt-1 text-[11px] text-neutral-500">Geçmiş tarihli kilo girişi için tarihi değiştir.</p>
              </div>
              {editing && (
                <button
                  type="button"
                  onClick={() => {
                    setEditing(false);
                    setWeight("");
                    setNotes("");
                    setLogDate(new Date().toISOString().slice(0, 10));
                  }}
                  className="text-xs text-neutral-400 hover:text-white"
                >
                  Vazgeç
                </button>
              )}
            </form>
          )}
        </Card>
      </div>
    </section>
  );
}

// One past weight log — tap the pencil to edit weight + note inline.
function WeightRow({ log }: { log: WeightLog }) {
  const updateWeight = useUpdateWeight();
  const deleteWeight = useDeleteWeight();
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [w, setW] = useState(String(log.weightKg));
  const [n, setN] = useState(log.notes ?? "");

  const start = () => {
    setW(String(log.weightKg));
    setN(log.notes ?? "");
    setEditing(true);
  };

  const save = () => {
    if (!w) return;
    updateWeight.mutate(
      { id: log.id, body: { weightKg: Number(w), notes: n || null } },
      {
        onSuccess: () => {
          toast.success("Kilo güncellendi");
          setEditing(false);
        },
        onError: () => toast.error("Güncellenemedi"),
      }
    );
  };

  if (editing) {
    return (
      <li className="flex items-center gap-2 py-2.5 text-sm">
        <input
          type="number"
          step="0.1"
          value={w}
          onChange={(e) => setW(e.target.value)}
          className="w-20 rounded-lg border border-hair bg-card2 px-2 py-1 text-center tabular-nums outline-none focus:border-accent"
          autoFocus
        />
        <input
          value={n}
          onChange={(e) => setN(e.target.value)}
          placeholder="Not"
          className="min-w-0 flex-1 rounded-lg border border-hair bg-card2 px-2 py-1 outline-none focus:border-accent"
        />
        <button onClick={save} className="rounded-lg bg-accent p-1.5 text-accentink" aria-label="Kaydet">
          <Check size={14} />
        </button>
        <button
          onClick={() => setEditing(false)}
          className="rounded-lg bg-card2 p-1.5 text-neutral-400"
          aria-label="Vazgeç"
        >
          <X size={14} />
        </button>
      </li>
    );
  }

  return (
    <li className="group flex items-center justify-between py-2.5 text-sm">
      <div className="min-w-0">
        <span className="text-neutral-400">{new Date(log.loggedAt).toLocaleDateString("tr-TR")}</span>
        {log.notes && <span className="ml-2 truncate text-xs text-neutral-400">· {log.notes}</span>}
      </div>
      <div className="flex items-center gap-3">
        <span className="num text-base tabular-nums">{log.weightKg}<span className="ml-0.5 text-[10px] font-normal text-neutral-400">kg</span></span>
        <button
          onClick={start}
          className="text-neutral-400 opacity-0 transition-opacity hover:text-accent group-hover:opacity-100"
          aria-label="Düzenle"
        >
          <Pencil size={15} />
        </button>
        <button
          onClick={() => deleteWeight.mutate(log.id)}
          className="text-neutral-400 hover:text-gain"
          aria-label="Sil"
        >
          <Trash2 size={16} />
        </button>
      </div>
    </li>
  );
}
