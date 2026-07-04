import { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { Search, X } from "lucide-react";
import { useAddExercise, useExerciseCatalog } from "../hooks/useWorkout";
import { useToast } from "../hooks/useToast";
import { ListSkeleton } from "./ui";

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

  const groups = catalog.data ?? [];
  const tabs = ["Tümü", ...groups.map((g) => g.muscleGroup)];

  const visible = useMemo(() => {
    // First filter by selected muscle group (or all)
    let filtered = filter === "Tümü" ? groups : groups.filter((g) => g.muscleGroup === filter);

    // Then apply search filter across ALL exercises (overrides group filter)
    if (search.trim()) {
      const q = search.trim().toLowerCase().replace(/ı/g,"i").replace(/ğ/g,"g").replace(/ü/g,"u").replace(/ş/g,"s").replace(/ö/g,"o").replace(/ç/g,"c");
      filtered = groups
        .map(g => ({
          muscleGroup: g.muscleGroup,
          exercises: g.exercises.filter(e =>
            e.toLowerCase().replace(/ı/g,"i").replace(/ğ/g,"g").replace(/ü/g,"u").replace(/ş/g,"s").replace(/ö/g,"o").replace(/ç/g,"c").includes(q)
          )
        }))
        .filter(g => g.exercises.length > 0);
    }

    return filtered;
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
        className={`absolute inset-0 bg-black/50 transition-opacity duration-300 ${
          shown ? "opacity-100" : "opacity-0"
        }`}
      />
      <div
        className={`absolute inset-x-0 bottom-0 mx-auto flex max-h-[80vh] max-w-md flex-col rounded-t-3xl bg-card shadow-2xl transition-transform duration-300 ${
          shown ? "translate-y-0" : "translate-y-full"
        }`}
      >
        {/* fixed header */}
        <div className="shrink-0 border-b border-hair px-5 pb-4 pt-5">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-bold">Egzersiz Ekle</h2>
            <button onClick={close} className="text-neutral-400 hover:text-white">
              <X size={22} />
            </button>
          </div>

          {/* search */}
          <div className="relative mb-3">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-500" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Hareket ara... (örn: chest press)"
              className="w-full rounded-xl border border-hair bg-card2 py-2.5 pl-9 pr-4 text-sm text-white outline-none placeholder:text-neutral-500 focus:border-accent"
            />
          </div>

          {/* muscle group filter tabs (hide when searching) */}
          {!search.trim() && (
            <div className="flex flex-wrap gap-1.5">
              {tabs.map((t) => (
                <button
                  key={t}
                  onClick={() => setFilter(t)}
                  className={`rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
                    filter === t
                      ? "bg-accent text-accentink"
                      : "bg-card2 text-neutral-300 hover:bg-white/10"
                  }`}
                >
                  {t}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* scrollable body */}
        <div className="flex-1 overflow-y-auto px-5 pb-6 pt-3 scrollbar-thin">
          {catalog.isLoading ? (
            <ListSkeleton rows={5} />
          ) : (
            <div className="space-y-5">
              {visible.map((g) => (
                <div key={g.muscleGroup}>
                  <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-neutral-500">{g.muscleGroup}</div>
                  <div className="grid grid-cols-2 gap-2">
                    {g.exercises.map((ex) => (
                      <button
                        key={ex}
                        onClick={() => add(ex, g.muscleGroup)}
                        disabled={addExercise.isPending}
                        className="rounded-xl bg-card2 px-3.5 py-3 text-left text-sm font-medium transition-colors hover:bg-white/10 disabled:opacity-50"
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
