import { useCoachHistory } from "../hooks/useCoach";

export default function CoachHistory({ onClose }: { onClose: () => void }) {
  const history = useCoachHistory();
  const items = history.data ?? [];

  return (
    <div className="flex max-h-[70vh] flex-col">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-2xl font-bold text-neutral-100">Koç geçmişi</h2>
        <span className="text-xs text-neutral-500">son 3 gün</span>
      </div>

      <div className="flex-1 space-y-3 overflow-y-auto pr-1">
        {history.isLoading ? (
          <p className="text-sm text-neutral-400">Yükleniyor...</p>
        ) : items.length === 0 ? (
          <p className="text-sm text-neutral-400">Henüz konuşma yok.</p>
        ) : (
          items.map((m, i) => {
            const isUser = m.role === "user";
            return (
              <div key={i} className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
                <div
                  className={`max-w-[88%] whitespace-pre-wrap rounded-2xl px-3.5 py-2 text-sm leading-relaxed ${
                    isUser
                      ? "bg-accent text-white"
                      : "bg-card2 text-neutral-100"
                  }`}
                >
                  {m.content}
                </div>
              </div>
            );
          })
        )}
      </div>

      <button
        onClick={onClose}
        className="mt-4 min-h-10 w-full rounded-[var(--radius-input)] border border-hair bg-card2 py-2 text-sm font-medium text-neutral-400 transition-colors hover:text-neutral-100"
      >
        Kapat
      </button>
    </div>
  );
}
