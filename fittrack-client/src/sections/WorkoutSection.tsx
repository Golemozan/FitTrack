import { useEffect, useMemo, useRef, useState } from "react";
import { Check, Dumbbell, MoreHorizontal, Pencil, Plus, Trash2, X } from "lucide-react";
import {
  useAddSet,
  useCreateSession,
  useDeleteExercise,
  useDeleteSet,
  useExerciseHistory,
  useRecentSessions,
  useTodaySession,
  useUpdateExercise,
  useUpdateSet,
} from "../hooks/useWorkout";
import { useToast } from "../hooks/useToast";
import AddExerciseSheet from "../components/AddExerciseSheet";
import { Card, EmptyState, ListSkeleton, SectionTitle } from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";
import type { Exercise, ExerciseSet } from "../types";

export default function WorkoutSection() {
  const today = useTodaySession();
  const recent = useRecentSessions(10);
  const createSession = useCreateSession();
  const toast = useToast();
  const [sheetOpen, setSheetOpen] = useState(false);
  const didAutoStart = useRef(false);

  const session = today.data;

  useEffect(() => {
    if (didAutoStart.current) return;
    if (today.isLoading || session || createSession.isPending) return;

    didAutoStart.current = true;
    createSession.mutate(`Antrenman - ${new Date().toLocaleDateString("tr-TR")}`, {
      onError: () => {
        toast.error("Seans oluşturulamadı");
        didAutoStart.current = false;
      },
    });
  }, [today.isLoading, session, createSession.isPending, toast]);

  const isLoading = today.isLoading || createSession.isPending;

  return (
    <section id="antrenman" className="scroll-mt-24">
      <SectionHeader
        icon={Dumbbell}
        title="Antrenman"
        action={{
          label: "Egzersiz Ekle",
          onClick: () => setSheetOpen(true),
          disabled: !session,
        }}
      />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          {isLoading ? (
            <Card><ListSkeleton rows={4} /></Card>
          ) : !session ? (
            <Card>
              <EmptyState icon={Dumbbell} title="Seans oluşturulamadı">
                Kapatıp tekrar açmayı dene.
              </EmptyState>
            </Card>
          ) : session.exercises.length === 0 ? (
            <Card>
              <EmptyState icon={Plus}>Egzersiz eklemeye başla.</EmptyState>
            </Card>
          ) : (
            <div className="grid gap-3 xl:grid-cols-2">
              {session.exercises.map((ex) => (
                <ExerciseCard key={ex.id} exercise={ex} sessionId={session.id} />
              ))}
            </div>
          )}
        </div>

        <aside className="space-y-4 lg:col-span-1">
          <Card>
            <SectionTitle accent="orange">Son antrenmanlar</SectionTitle>
            {recent.isLoading ? (
              <ListSkeleton rows={3} />
            ) : recent.data?.length === 0 ? (
              <EmptyState>Kayıt yok.</EmptyState>
            ) : (
              <ul className="divide-y divide-white/10">
                {recent.data?.map((s) => (
                  <li key={s.id} className="flex items-center justify-between py-2.5 text-sm">
                    <div>
                      <div className="font-medium">{s.name}</div>
                      <div className="text-xs text-neutral-400">
                        {new Date(s.loggedAt).toLocaleDateString("tr-TR")}
                      </div>
                    </div>
                    <span className="rounded-full bg-accent/10 px-2 py-1 text-xs font-medium text-accent">
                      {s.exerciseCount} hareket
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </aside>
      </div>

      {session && (
        <AddExerciseSheet open={sheetOpen} sessionId={session.id} onClose={() => setSheetOpen(false)} />
      )}
    </section>
  );
}

function ExerciseCard({ exercise, sessionId }: { exercise: Exercise; sessionId: string }) {
  const history = useExerciseHistory(exercise.name);
  const deleteExercise = useDeleteExercise();
  const updateExercise = useUpdateExercise();
  const addSet = useAddSet();
  const [menuOpen, setMenuOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(exercise.name);

  const saveName = () => {
    const trimmed = name.trim();
    if (!trimmed) return;
    updateExercise.mutate(
      { id: exercise.id, body: { name: trimmed, muscleGroup: exercise.muscleGroup } },
      { onSuccess: () => setEditing(false) }
    );
  };

  const ghost = useMemo(() => {
    const prior = history.data?.find((h) => h.sessionId !== sessionId);
    const map = new Map<number, ExerciseSet>();
    prior?.sets.forEach((s) => map.set(s.setNumber, s));
    return map;
  }, [history.data, sessionId]);

  const sortedSets = [...exercise.sets].sort((a, b) => a.setNumber - b.setNumber);

  const priorBest = useMemo(() => {
    let best = 0;
    history.data?.forEach((h) => {
      if (h.sessionId !== sessionId) h.sets.forEach((s) => (best = Math.max(best, s.weightKg)));
    });
    return best;
  }, [history.data, sessionId]);

  const exVolume = exercise.sets.reduce((a, s) => a + (s.isCompleted ? s.weightKg * s.reps : 0), 0);
  const currentBest = exercise.sets.filter((s) => s.isCompleted).reduce((m, s) => Math.max(m, s.weightKg), 0);
  const isPR = currentBest > 0 && currentBest > priorBest;

  const addNext = () => {
    const last = sortedSets.at(-1);
    addSet.mutate({
      exerciseId: exercise.id,
      setNumber: sortedSets.length + 1,
      weightKg: last?.weightKg ?? 0,
      reps: last?.reps ?? 0,
      isCompleted: false,
    });
  };

  return (
    <Card>
      <div className="mb-3 flex items-start justify-between gap-2">
        {editing ? (
          <div className="flex flex-1 items-center gap-2">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && saveName()}
              autoFocus
              className="min-w-0 flex-1 rounded-lg border border-hair bg-card2 px-2 py-1 font-bold outline-none focus:border-accent"
            />
            <button onClick={saveName} className="rounded-lg bg-accent p-1.5 text-accentink" aria-label="Kaydet">
              <Check size={15} />
            </button>
            <button
              onClick={() => { setName(exercise.name); setEditing(false); }}
              className="rounded-lg bg-card2 p-1.5 text-neutral-400"
              aria-label="Vazgeç"
            >
              <X size={15} />
            </button>
          </div>
        ) : (
          <>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold">{exercise.name}</span>
                {isPR && (
                  <span className="rounded-full bg-accent px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-accentink">
                    PR
                  </span>
                )}
              </div>
              <div className="mt-1 flex items-center gap-2">
                <span className="inline-block rounded-full bg-accent/10 px-2 py-0.5 text-xs font-medium text-accent">
                  {exercise.muscleGroup}
                </span>
                {exVolume > 0 && (
                  <span className="text-[11px] text-neutral-400">
                    <span className="num text-neutral-300">{Math.round(exVolume)}</span> kg
                  </span>
                )}
              </div>
            </div>
            <div className="relative">
              <button
                onClick={() => setMenuOpen((v) => !v)}
                className="rounded-lg p-1 text-neutral-400 hover:bg-white/5"
                aria-label="Menü"
              >
                <MoreHorizontal size={18} />
              </button>
              {menuOpen && (
                <div className="absolute right-0 top-8 z-10 w-32 overflow-hidden rounded-xl border border-white/10 bg-card2 shadow-lg">
                  <button
                    onClick={() => { setMenuOpen(false); setName(exercise.name); setEditing(true); }}
                    className="flex w-full items-center gap-2 px-3 py-2 text-sm hover:bg-white/5"
                  >
                    <Pencil size={14} /> Düzenle
                  </button>
                  <button
                    onClick={() => { setMenuOpen(false); deleteExercise.mutate(exercise.id); }}
                    className="flex w-full items-center gap-2 px-3 py-2 text-sm text-gain hover:bg-white/5"
                  >
                    <Trash2 size={14} /> Sil
                  </button>
                </div>
              )}
            </div>
          </>
        )}
      </div>

      <div className="grid grid-cols-[2rem_1fr_1fr_2rem] items-center gap-2 px-1 text-[11px] font-semibold uppercase text-neutral-400">
        <span>Set</span>
        <span>Kg</span>
        <span>Tekrar</span>
        <span></span>
      </div>
      <div className="mt-1 space-y-1">
        {sortedSets.map((s) => (
          <SetRow key={s.id} set={s} ghost={ghost.get(s.setNumber)} />
        ))}
      </div>

      <button
        onClick={addNext}
        disabled={addSet.isPending}
        className="mt-2 flex items-center gap-1 px-1 text-sm font-medium text-accent"
      >
        <Plus size={14} /> Set Ekle
      </button>
    </Card>
  );
}

function SetRow({ set, ghost }: { set: ExerciseSet; ghost?: ExerciseSet }) {
  const updateSet = useUpdateSet();
  const deleteSet = useDeleteSet();
  const [weight, setWeight] = useState(set.weightKg ? String(set.weightKg) : "");
  const [reps, setReps] = useState(set.reps ? String(set.reps) : "");

  const commit = (completed = set.isCompleted) =>
    updateSet.mutate({
      id: set.id,
      body: { weightKg: Number(weight) || 0, reps: Number(reps) || 0, isCompleted: completed },
    });

  const inputCls =
    "w-full rounded-lg border border-hair bg-card2 px-2 py-1.5 text-center text-sm outline-none focus:border-accent placeholder:italic placeholder:text-neutral-500";

  return (
    <div className="grid grid-cols-[2rem_1fr_1fr_2rem] items-center gap-2">
      <span className="text-center text-sm text-neutral-400">{set.setNumber}</span>
      <input
        type="number"
        value={weight}
        placeholder={ghost ? String(ghost.weightKg) : "0"}
        onChange={(e) => setWeight(e.target.value)}
        onBlur={() => commit()}
        className={inputCls}
      />
      <input
        type="number"
        value={reps}
        placeholder={ghost ? String(ghost.reps) : "0"}
        onChange={(e) => setReps(e.target.value)}
        onBlur={() => commit()}
        className={inputCls}
      />
      <div className="flex items-center justify-end gap-1">
        <button
          onClick={() => commit(!set.isCompleted)}
          className={`rounded-full p-1.5 transition-colors ${
            set.isCompleted ? "bg-accent text-accentink" : "bg-card2 text-neutral-400"
          }`}
          aria-label="Tamamlandı"
        >
          <Check size={13} />
        </button>
        <button
          onClick={() => deleteSet.mutate(set.id)}
          className="text-neutral-400 hover:text-gain"
          aria-label="Seti sil"
        >
          <Trash2 size={13} />
        </button>
      </div>
    </div>
  );
}
