import { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { Dumbbell, Search, X } from "lucide-react";
import { useAddExercise, useExerciseCatalog } from "../hooks/useWorkout";
import { useToast } from "../hooks/useToast";
import { EmptyState, IconButton, ListSkeleton } from "./ui";

/** Turkish-insensitive fold so "gogus" matches "göğüs". */
const fold = (s: string) =>
  s
    .toLowerCase()
    .replace(/ı/g, "i")
    .replace(/ğ/g, "g")
    .replace(/ü/g, "u")
    .replace(/ş/g, "s")
    .replace(/ö/g, "o")
    .replace(/ç/g, "c");

export default function AddExerciseSheet({
  open,
  sessionId,
  onClose,
}: {
  open: boolean;
  sessionId: string;
  onClose: () => void;
}) {
  const catalog = useExerciseCatalog();
  const addExercise = useAddExercise();
  const toast = useToast();

  const [shown, setShown] = useState(false);
  const [filter, setFilter] = useState("Tümü");
  const [search, setSearch] = useState("");

  useEffect(() => {
    if (open) {
      setShown(true);
      setFilter("Tümü");
      setSearch("");
    }
  }, [open]);

  const close = () => {
    setShown(false);
    setTimeout(onClose, 300);
  };

  const groups = useMemo(() => catalog.data ?? [], [catalog.data]);
  const tabs = ["Tümü", ...groups.map((g) => g.muscleGroup)];

  const visible = useMemo(() => {
    // Search spans the whole catalog and overrides the group filter.
    if (search.trim()) {
      const q = fold(search.trim());
      return groups
        .map((g) => ({ muscleGroup: g.muscleGroup, exercises: g.exercises.filter((e) => fold(e).includes(q)) }))
        .filter((g) => g.exercises.length > 0);
    }
    return filter === "Tümü" ? groups : groups.filter((g) => g.muscleGroup === filter);
  }, [groups, filter, search]);

  const add = (name: string, muscleGroup: string) => {
    addExercise.mutate(
      { workoutSessionId: sessionId, name, muscleGroup },
      {
        onSuccess: () => {
          toast.success(`${name} eklendi`);
          close();
        },
        onError: () => toast.error("Egzersiz eklenemedi"),
      }
    );
  };

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-[60]">
      <div
        onClick={close}
        className={`absolute inset-0 bg-ink/70 backdrop-blur-sm transition-opacity duration-300 ${
          shown ? "opacity-100" : "opacity-0"
        }`}
      />
      <div
        className={`absolute inset-x-0 bottom-0 mx-auto flex max-h-[80vh] max-w-md flex-col rounded-t-3xl border-t border-white/[0.07] bg-panel shadow-2xl shadow-black/50 transition-transform duration-300 ${
          shown ? "translate-y-0" : "translate-y-full"
        }`}
      >
        <div className="shrink-0 border-b border-white/[0.06] px-5 pb-4 pt-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h2 className="text-lg font-semibold text-neutral-100">Egzersiz ekle</h2>
            <IconButton icon={X} size={19} onClick={close} aria-label="Kapat" />
          </div>

          <div className="relative">
            <Search size={17} className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-500" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Hareket ara — örn. chest press"
              className="h-11 w-full rounded-xl border border-white/[0.07] bg-black/25 pl-10 pr-3 text-sm text-neutral-100 outline-none transition-colors placeholder:text-neutral-600 focus:border-accent/50 focus:bg-black/35"
            />
          </div>

          {!search.trim() && (
            <div className="mt-3 flex flex-wrap gap-1.5">
              {tabs.map((t) => {
                const active = filter === t;
                return (
                  <button
                    key={t}
                    onClick={() => setFilter(t)}
                    className={`flex h-9 items-center rounded-full px-3.5 text-xs font-medium transition-colors ${
                      active
                        ? "bg-accent/15 text-accent ring-1 ring-inset ring-accent/40"
                        : "bg-white/[0.04] text-neutral-400 hover:bg-white/[0.08] hover:text-neutral-200"
                    }`}
                  >
                    {t}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        <div className="flex-1 overflow-y-auto px-5 pb-6 pt-4">
          {catalog.isLoading ? (
            <ListSkeleton rows={5} />
          ) : visible.length === 0 ? (
            <EmptyState icon={Dumbbell} title="Eşleşen hareket yok">
              Aramayı kısalt ya da başka bir kas grubu seç.
            </EmptyState>
          ) : (
            <div className="space-y-5">
              {visible.map((g) => (
                <div key={g.muscleGroup}>
                  <div className="eyebrow mb-2.5 text-[11px] text-neutral-500">{g.muscleGroup}</div>
                  <div className="grid grid-cols-2 gap-2">
                    {g.exercises.map((ex) => (
                      <button
                        key={ex}
                        onClick={() => add(ex, g.muscleGroup)}
                        disabled={addExercise.isPending}
                        className="flex min-h-[3rem] items-center rounded-xl bg-white/[0.04] px-3.5 py-2.5 text-left text-sm font-medium text-neutral-200 transition-colors hover:bg-white/[0.08] disabled:opacity-40"
                      >
                        {ex}
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
