import { useState } from "react";
import { HeartPulse, Trash2 } from "lucide-react";
import { useDeleteCheckin, useLogCheckin, useTodayCheckins } from "../hooks/useCoach";
import { useToast } from "../hooks/useToast";
import { Button, Card, EmptyState } from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";

const SCALES = [
  { key: "mood", label: "Ruh hali", emojis: ["😩", "🙁", "😐", "🙂", "😄"] },
  { key: "energy", label: "Enerji", emojis: ["🪫", "😴", "😌", "⚡", "🔥"] },
  { key: "hunger", label: "Açlık → Tokluk", emojis: ["🍽️", "😋", "🤔", "😊", "🥱"] },
] as const;

const CONTEXTS = [
  { key: "general", tr: "Genel" },
  { key: "pre-workout", tr: "Antrenman öncesi" },
  { key: "post-workout", tr: "Antrenman sonrası" },
];

export default function CheckInSection() {
  const today = useTodayCheckins();
  const logCheckin = useLogCheckin();
  const deleteCheckin = useDeleteCheckin();
  const toast = useToast();

  const [mood, setMood] = useState(3);
  const [energy, setEnergy] = useState(3);
  const [hunger, setHunger] = useState(3);
  const [context, setContext] = useState("general");
  const [note, setNote] = useState("");

  const values: Record<string, [number, (n: number) => void]> = {
    mood: [mood, setMood],
    energy: [energy, setEnergy],
    hunger: [hunger, setHunger],
  };

  const save = () => {
    logCheckin.mutate(
      { mood, energy, hunger, note: note.trim() || null, context },
      {
        onSuccess: () => {
          toast.success("Check-in kaydedildi");
          setNote("");
        },
        onError: () => toast.error("Kaydedilemedi"),
      }
    );
  };

  return (
    <section id="checkin" className="scroll-mt-24">
      <SectionHeader icon={HeartPulse} title="Nasılsın?" />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* LOG */}
        <Card className="lg:col-span-2">
          <div className="space-y-4">
            {SCALES.map((s) => {
              const [val, setVal] = values[s.key];
              return (
                <div key={s.key}>
                  <div className="mb-1.5 text-sm font-medium text-neutral-300">{s.label}</div>
                  <div className="grid grid-cols-5 gap-2">
                    {s.emojis.map((e, i) => {
                      const n = i + 1;
                      const active = val === n;
                      return (
                        <button
                          key={n}
                          onClick={() => setVal(n)}
                          className={`flex flex-col items-center gap-0.5 rounded-xl border py-2 text-2xl transition-[background-color,border-color,opacity] ${
                            active
                              ? "border-accent bg-accent/10"
                              : "border-hair bg-card2 opacity-60 hover:opacity-100"
                          }`}
                        >
                          {e}
                          <span className="num text-[10px] text-neutral-400">{n}</span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              );
            })}

            {/* context */}
            <div className="flex flex-wrap gap-2">
              {CONTEXTS.map((c) => (
                <button
                  key={c.key}
                  onClick={() => setContext(c.key)}
                  className={`rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
                    context === c.key ? "bg-accent text-accentink" : "bg-card2 text-neutral-300"
                  }`}
                >
                  {c.tr}
                </button>
              ))}
            </div>

            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={2}
              placeholder="Not (opsiyonel) — ne hissediyorsun, ne yedin, nasıl geçti?"
              className="w-full resize-none rounded-xl border border-hair bg-card2 px-3 py-2 text-sm text-neutral-100 outline-none focus:border-accent"
            />

            <Button onClick={save} disabled={logCheckin.isPending} className="w-full py-2.5">
              Check-in Kaydet
            </Button>
          </div>
        </Card>

        {/* TODAY LIST */}
        <Card>
          <div className="mb-3 eyebrow text-[13px] text-neutral-400">Bugünkü check-in'ler</div>
          {today.isLoading ? (
            <p className="text-sm text-neutral-500">Yükleniyor…</p>
          ) : (today.data?.length ?? 0) === 0 ? (
            <EmptyState icon={HeartPulse}>Bugün henüz check-in yok.</EmptyState>
          ) : (
            <ul className="space-y-2">
              {today.data!.map((c) => (
                <li
                  key={c.id}
                  className="group flex items-center justify-between gap-2 rounded-xl border border-hair bg-card2 px-3 py-2"
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 text-sm">
                      <span title="ruh">{SCALES[0].emojis[c.mood - 1]}</span>
                      <span title="enerji">{SCALES[1].emojis[c.energy - 1]}</span>
                      <span title="açlık">{SCALES[2].emojis[c.hunger - 1]}</span>
                      <span className="text-[11px] text-neutral-500">
                        {new Date(c.loggedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
                      </span>
                    </div>
                    {c.note && <div className="mt-0.5 truncate text-xs text-neutral-400">{c.note}</div>}
                  </div>
                  <button
                    onClick={() => deleteCheckin.mutate(c.id)}
                    className="shrink-0 text-neutral-500 opacity-0 transition-opacity hover:text-gain group-hover:opacity-100"
                    aria-label="Sil"
                  >
                    <Trash2 size={15} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </section>
  );
}
