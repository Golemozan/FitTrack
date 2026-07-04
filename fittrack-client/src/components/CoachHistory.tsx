import { useCoachHistory } from "../hooks/useCoach";

export default function CoachHistory({ onClose }: { onClose: () => void }) {
  const history = useCoachHistory();
  const items = history.data ?? [];

  return (
    <div className="flex max-h-[70vh] flex-col">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-bold">Koç Geçmişi</h2>
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
                      ? "bg-accent-grad text-accentink"
                      : "border border-hair bg-white/[0.05] text-neutral-100"
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
        className="mt-4 w-full rounded-xl border border-hair py-2 text-sm text-neutral-400 transition-colors hover:text-white"
      >
        Kapat
      </button>
    </div>
  );
}
