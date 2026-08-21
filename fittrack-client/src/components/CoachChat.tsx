import { useEffect, useRef, useState } from "react";
import { Pencil, Send, Trash2, X } from "lucide-react";
import { useCoachChat } from "../hooks/useCoach";
import type { CoachMessage } from "../types";

const GREETING =
  "Selam Ozan. Ben Koç. Spor bilimleri ve beslenme biyokimyası temelinde çalışıyorum. Ne yediğini, nasıl hissettiğini, antrenmanını anlat — verilerinle birlikte analiz edip yönlendireyim.";

const SUGGESTIONS = [
  "Kilo trendimi yorumla",
  "Bugünkü protein yeterli mi?",
  "Antrenman hacmim nasıl gidiyor?",
  "Bu hafta beslenmem nasıl?",
];

/** Full-height coach conversation — fills whatever container it's dropped into. */
export default function CoachChat() {
  const [messages, setMessages] = useState<CoachMessage[]>([]);
  const [input, setInput] = useState("");
  const [editingIdx, setEditingIdx] = useState<number | null>(null);
  const chat = useCoachChat();
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (messages.length === 0 && !chat.isPending) return;
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, chat.isPending]);

  const send = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || chat.isPending) return;

    // If we're editing an existing message, replace it and resend the updated history
    if (editingIdx !== null) {
      const updated = [...messages];
      updated[editingIdx] = { role: "user" as const, content: trimmed };
      setMessages(updated);
      setEditingIdx(null);
      setInput("");
      chat.mutate(updated, {
        onSuccess: (res) => setMessages((m) => [...m, { role: "assistant", content: res.reply }]),
        onError: () =>
          setMessages((m) => [
            ...m,
            { role: "assistant", content: "⚠️ Sana ulaşamadım. Backend + API anahtarı çalışıyor mu bir bak." },
          ]),
      });
      return;
    }

    const next: CoachMessage[] = [...messages, { role: "user", content: trimmed }];
    setMessages(next);
    setInput("");
    chat.mutate(next, {
      onSuccess: (res) => setMessages((m) => [...m, { role: "assistant", content: res.reply }]),
      onError: () =>
        setMessages((m) => [
          ...m,
          { role: "assistant", content: "⚠️ Sana ulaşamadım. Backend + API anahtarı çalışıyor mu bir bak." },
        ]),
    });
  };

  const deleteMessage = (idx: number) => {
    // Remove this user message + the assistant reply right after it (if any)
    const next = [...messages];
    const isUser = next[idx]?.role === "user";
    if (isUser && idx + 1 < next.length && next[idx + 1].role === "assistant") {
      next.splice(idx, 2); // delete user msg + coach reply
    } else {
      next.splice(idx, 1);
    }
    setMessages(next);
    if (editingIdx === idx) setEditingIdx(null);
  };

  const startEdit = (idx: number) => {
    setEditingIdx(idx);
    setInput(messages[idx].content);
    // Focus the textarea after state update — handled by the autoFocus below
  };

  const cancelEdit = () => {
    setEditingIdx(null);
    setInput("");
  };

  return (
    <div className="flex h-full flex-col">
      <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto pr-1">
        <Bubble
          role="assistant"
          content={GREETING}
          index={-1}
          onDelete={undefined}
          onEdit={undefined}
        />
        {messages.map((m, i) => (
          <Bubble
            key={i}
            role={m.role}
            content={m.content}
            index={i}
            onDelete={deleteMessage}
            onEdit={m.role === "user" ? startEdit : undefined}
            isEditing={editingIdx === i}
          />
        ))}
        {chat.isPending && <Typing />}

        {messages.length === 0 && !chat.isPending && (
          <div className="grid gap-2 pt-2 sm:grid-cols-2 lg:grid-cols-1">
            {SUGGESTIONS.map((s) => (
              <button
                key={s}
                onClick={() => send(s)}
                className="min-h-11 rounded-xl bg-card2 px-3.5 py-2.5 text-left text-sm text-neutral-300 transition-colors hover:bg-white/[0.09] hover:text-white"
              >
                {s}
              </button>
            ))}
          </div>
        )}
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          send(input);
        }}
        className="mt-3 flex flex-wrap items-end gap-2 border-t border-hair pt-3"
      >
        {editingIdx !== null && (
          <span className="flex w-full items-center gap-1 rounded-lg bg-accent/10 px-2 py-1 text-xs text-accent">
            <Pencil size={11} /> düzenleniyor
            <button type="button" onClick={cancelEdit} className="ml-auto flex h-9 w-9 items-center justify-center rounded-md hover:bg-accent/10">
              <X size={12} />
            </button>
          </span>
        )}
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              send(input);
            }
            if (e.key === "Escape" && editingIdx !== null) {
              cancelEdit();
            }
          }}
          rows={1}
          placeholder={editingIdx !== null ? "Mesajı düzelt..." : "Nasıl hissediyorsun?"}
          className="max-h-28 min-h-11 min-w-0 flex-1 resize-none rounded-[var(--radius-input)] border border-hair bg-card2 px-3.5 py-2.5 text-sm text-neutral-100 outline-none transition-colors placeholder:text-neutral-500 focus:border-accent focus:bg-panel"
        />
        <button
          type="submit"
          disabled={!input.trim() || chat.isPending}
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-accent text-white press hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
          aria-label={editingIdx !== null ? "Düzelt ve gönder" : "Gönder"}
        >
          {editingIdx !== null ? <Pencil size={18} /> : <Send size={18} />}
        </button>
      </form>
    </div>
  );
}

function Bubble({
  role,
  content,
  index,
  onDelete,
  onEdit,
  isEditing,
}: {
  role: string;
  content: string;
  index: number;
  onDelete?: (idx: number) => void;
  onEdit?: (idx: number) => void;
  isEditing?: boolean;
}) {
  const isUser = role === "user";
  const canEdit = onDelete != null; // only real messages (not greeting)
  return (
    <div className={`flex group/bubble ${isUser ? "justify-end" : "justify-start"}`}>
      <div className="relative max-w-[88%]">
        <div
          className={`whitespace-pre-wrap rounded-[var(--radius-card)] px-3.5 py-2.5 text-sm leading-relaxed ${
            isEditing
              ? "ring-2 ring-accent"
              : isUser
                ? "bg-accent text-white"
                : "bg-card2 text-neutral-100"
          }`}
        >
          {content}
        </div>
        {canEdit && (
          <div
            className={`absolute -top-4 flex gap-1 opacity-0 transition-opacity group-focus-within/bubble:opacity-100 group-hover/bubble:opacity-100 ${
              isUser ? "-left-2 flex-row-reverse" : "-right-2"
            }`}
          >
            {onEdit && (
              <button
                onClick={(e) => { e.stopPropagation(); onEdit(index); }}
                className="flex h-9 w-9 items-center justify-center rounded-full border border-hair bg-panel text-neutral-400 transition-colors hover:text-neutral-100"
                title="Düzenle"
              >
                <Pencil size={10} />
              </button>
            )}
            <button
              onClick={(e) => { e.stopPropagation(); onDelete!(index); }}
              className="flex h-9 w-9 items-center justify-center rounded-full border border-hair bg-panel text-neutral-400 transition-colors hover:bg-gain/10 hover:text-gain"
              title={isUser ? "Mesajı ve yanıtı sil" : "Sil"}
            >
              <Trash2 size={10} />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function Typing() {
  return (
    <div className="flex justify-start">
      <div className="flex gap-1 rounded-[var(--radius-card)] border border-hair bg-card2 px-4 py-3">
        {[0, 1, 2].map((i) => (
          <span
            key={i}
            className="h-1.5 w-1.5 animate-pulse rounded-full bg-accent"
            style={{ animationDelay: `${i * 0.15}s` }}
          />
        ))}
      </div>
    </div>
  );
}
