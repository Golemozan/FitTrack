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
import { Card, Chip, EmptyState, IconButton, ListSkeleton, SectionTitle } from "../components/ui";
import { SectionHeader } from "../components/SectionHeader";
import type { Exercise, ExerciseSet } from "../types";

/**
 * Every set row and its header share this template, so the column labels sit
 * exactly over the fields they describe. The last track is `auto` — the action
 * column holds two 36px buttons and must not be squeezed into a fixed 2rem.
 */
const SET_GRID = "grid grid-cols-[1.75rem_1fr_1fr_auto] items-center gap-2";

export default function WorkoutSection() {
  const today = useTodaySession();
  const recent = useRecentSessions(10);
  const { mutate: createSession, isPending: isCreatingSession } = useCreateSession();
  const toast = useToast();
  const [sheetOpen, setSheetOpen] = useState(false);
  const didAutoStart = useRef(false);

  const session = today.data;

  useEffect(() => {
    if (didAutoStart.current) return;
    if (today.isLoading || session || isCreatingSession) return;

    didAutoStart.current = true;
    createSession(`Antrenman - ${new Date().toLocaleDateString("tr-TR")}`, {
      onError: () => {
        toast.error("Seans oluşturulamadı");
        didAutoStart.current = false;
      },
    });
  }, [today.isLoading, session, isCreatingSession, createSession, toast]);

  const isLoading = today.isLoading || isCreatingSession;

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
            <Card>
              <ListSkeleton rows={3} height="h-24" />
            </Card>
          ) : !session ? (
            <Card>
              {/* A real failure — kept visually distinct from an empty workout. */}
              <EmptyState icon={X} tone="error" title="Seans oluşturulamadı">
                Bağlantıyı kontrol et, sonra kapatıp tekrar aç.
              </EmptyState>
            </Card>
          ) : session.exercises.length === 0 ? (
            <Card>
              <EmptyState icon={Dumbbell} title="Antrenman boş">
                Yukarıdaki Egzersiz Ekle ile ilk hareketini seç.
              </EmptyState>
            </Card>
          ) : (
            <div className="grid gap-3 xl:grid-cols-2">
              {session.exercises.map((ex) => (
                <ExerciseCard key={ex.id} exercise={ex} sessionId={session.id} />
              ))}
            </div>
          )}
        </div>

        <aside className="lg:col-span-1">
          <Card>
            <SectionTitle>Son antrenmanlar</SectionTitle>
            {recent.isLoading ? (
              <ListSkeleton rows={3} />
            ) : recent.data?.length === 0 ? (
              <EmptyState icon={Dumbbell} title="Kayıt yok">
                Tamamladığın seanslar burada birikir.
              </EmptyState>
            ) : (
              <ul className="divide-y divide-white/[0.06]">
                {recent.data?.map((s) => (
                  <li key={s.id} className="flex items-center justify-between gap-3 py-3">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium text-neutral-200">{s.name}</div>
                      <div className="mt-0.5 text-xs text-neutral-500">
                        {new Date(s.loggedAt).toLocaleDateString("tr-TR")}
                      </div>
                    </div>
                    <Chip tone="accent" className="shrink-0">
                      {s.exerciseCount} hareket
                    </Chip>
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
    <Card flat className="p-4">
      <div className="mb-4 flex items-start justify-between gap-2">
        {editing ? (
          <div className="flex flex-1 items-center gap-2">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && saveName()}
              autoFocus
              className="h-9 min-w-0 flex-1 rounded-xl border border-white/[0.07] bg-black/25 px-3 text-sm font-semibold text-neutral-100 outline-none transition-colors focus:border-accent/50"
            />
            <IconButton icon={Check} tone="accent" size={15} onClick={saveName} aria-label="Kaydet" />
            <IconButton
              icon={X}
              size={15}
              onClick={() => {
                setName(exercise.name);
                setEditing(false);
              }}
              aria-label="Vazgeç"
            />
          </div>
        ) : (
          <>
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="truncate text-[15px] font-semibold text-neutral-100">{exercise.name}</span>
                {isPR && <Chip tone="accent">PR</Chip>}
              </div>
              <div className="mt-1.5 flex items-center gap-2">
                <Chip>{exercise.muscleGroup}</Chip>
                {exVolume > 0 && (
                  <span className="text-[11px] text-neutral-500">
                    <span className="num text-neutral-400">{Math.round(exVolume)}</span> kg hacim
                  </span>
                )}
              </div>
            </div>
            <div className="relative shrink-0">
              <IconButton
                icon={MoreHorizontal}
                size={17}
                onClick={() => setMenuOpen((v) => !v)}
                aria-label="Menü"
              />
              {menuOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setMenuOpen(false)} />
                  <div className="absolute right-0 top-10 z-20 w-36 overflow-hidden rounded-xl border border-white/[0.07] bg-card2 shadow-xl shadow-black/40">
                    <button
                      onClick={() => {
                        setMenuOpen(false);
                        setName(exercise.name);
                        setEditing(true);
                      }}
                      className="flex w-full items-center gap-2.5 px-3 py-2.5 text-sm text-neutral-200 transition-colors hover:bg-white/[0.06]"
                    >
                      <Pencil size={14} /> Düzenle
                    </button>
                    <button
                      onClick={() => {
                        setMenuOpen(false);
                        deleteExercise.mutate(exercise.id);
                      }}
                      className="flex w-full items-center gap-2.5 px-3 py-2.5 text-sm text-gain transition-colors hover:bg-gain/10"
                    >
                      <Trash2 size={14} /> Sil
                    </button>
                  </div>
                </>
              )}
            </div>
          </>
        )}
      </div>

      <div className={`${SET_GRID} mb-2 text-[11px] font-medium uppercase tracking-wider text-neutral-600`}>
        <span className="text-center">Set</span>
        <span className="text-center">Kg</span>
        <span className="text-center">Tekrar</span>
        <span className="w-[76px]" />
      </div>

      <div className="space-y-1.5">
        {sortedSets.map((s) => (
          <SetRow key={s.id} set={s} ghost={ghost.get(s.setNumber)} />
        ))}
      </div>

      <button
        onClick={addNext}
        disabled={addSet.isPending}
        className="mt-3 flex w-full items-center justify-center gap-1.5 rounded-xl py-2.5 text-[13px] font-medium text-accent transition-colors hover:bg-accent/10 disabled:opacity-40"
      >
        <Plus size={15} /> Set Ekle
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

  // Height, not vertical padding — keeps the field exactly as tall as the 36px
  // buttons beside it so the row reads as one aligned band.
  const inputCls =
    "h-10 w-full rounded-xl border border-white/[0.07] bg-black/25 text-center text-sm tabular-nums text-neutral-100 outline-none transition-colors placeholder:text-neutral-600 focus:border-accent/50 focus:bg-black/35";

  return (
    <div className={`${SET_GRID} ${set.isCompleted ? "opacity-70" : ""}`}>
      <span className="num text-center text-[13px] text-neutral-500">{set.setNumber}</span>
      <input
        type="number"
        inputMode="decimal"
        value={weight}
        placeholder={ghost ? String(ghost.weightKg) : "0"}
        onChange={(e) => setWeight(e.target.value)}
        onBlur={() => commit()}
        className={inputCls}
        aria-label={`Set ${set.setNumber} ağırlık`}
      />
      <input
        type="number"
        inputMode="numeric"
        value={reps}
        placeholder={ghost ? String(ghost.reps) : "0"}
        onChange={(e) => setReps(e.target.value)}
        onBlur={() => commit()}
        className={inputCls}
        aria-label={`Set ${set.setNumber} tekrar`}
      />
      <div className="flex items-center gap-1">
        <IconButton
          icon={Check}
          size={15}
          tone={set.isCompleted ? "active" : "default"}
          onClick={() => commit(!set.isCompleted)}
          aria-label={set.isCompleted ? "Tamamlandı işaretini kaldır" : "Tamamlandı işaretle"}
        />
        <IconButton
          icon={Trash2}
          size={15}
          tone="danger"
          onClick={() => deleteSet.mutate(set.id)}
          aria-label="Seti sil"
        />
      </div>
    </div>
  );
}
