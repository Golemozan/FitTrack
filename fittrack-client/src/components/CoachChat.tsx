import { useEffect, useRef, useState } from "react";
import { Send } from "lucide-react";
import { useCoachChat } from "../hooks/useCoach";
import type { CoachMessage } from "../types";

const GREETING =
  "Selam Ozan 👋 Ben Koç. Nasıl hissediyorsun? Açlık/tokluk, enerji, antrenman... anlat, verilerinle karşılaştırıp yol gösteririm.";

const SUGGESTIONS = [
  "Bugün nasıl gidiyorum?",
  "Canım tatlı çekiyor",
  "Antrenman öncesi ne yesem?",
  "Hedefime yaklaşıyor muyum?",
];

/** Full-height coach conversation — fills whatever container it's dropped into. */
export default function CoachChat() {
  const [messages, setMessages] = useState<CoachMessage[]>([]);
  const [input, setInput] = useState("");
  const chat = useCoachChat();
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, chat.isPending]);

  const send = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || chat.isPending) return;
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

  return (
    <div className="flex h-full flex-col">
      <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto pr-1">
        <Bubble role="assistant" content={GREETING} />
        {messages.map((m, i) => (
          <Bubble key={i} role={m.role} content={m.content} />
        ))}
        {chat.isPending && <Typing />}

        {messages.length === 0 && !chat.isPending && (
          <div className="flex flex-wrap gap-2 pt-1">
            {SUGGESTIONS.map((s) => (
              <button
                key={s}
                onClick={() => send(s)}
                className="rounded-full border border-hair bg-white/[0.03] px-3 py-1.5 text-xs text-neutral-300 transition-colors hover:border-accent/50 hover:text-white"
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
        className="mt-3 flex items-end gap-2"
      >
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              send(input);
            }
          }}
          rows={1}
          placeholder="Nasıl hissediyorsun?"
          className="max-h-28 flex-1 resize-none rounded-xl border border-hair bg-white/[0.04] px-3 py-2.5 text-sm text-white outline-none focus:border-accent"
        />
        <button
          type="submit"
          disabled={!input.trim() || chat.isPending}
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-accent-grad text-accentink shadow-glow transition-all hover:brightness-110 disabled:opacity-40 disabled:shadow-none"
          aria-label="Gönder"
        >
          <Send size={18} />
        </button>
      </form>
    </div>
  );
}

function Bubble({ role, content }: { role: string; content: string }) {
  const isUser = role === "user";
  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[88%] whitespace-pre-wrap rounded-2xl px-3.5 py-2 text-sm leading-relaxed ${
          isUser ? "bg-accent-grad text-accentink" : "border border-hair bg-white/[0.05] text-neutral-100"
        }`}
      >
        {content}
      </div>
    </div>
  );
}

function Typing() {
  return (
    <div className="flex justify-start">
      <div className="flex gap-1 rounded-2xl border border-hair bg-white/[0.05] px-4 py-3">
        {[0, 1, 2].map((i) => (
          <span
            key={i}
            className="h-1.5 w-1.5 animate-bounce rounded-full bg-accent"
            style={{ animationDelay: `${i * 0.15}s` }}
          />
        ))}
      </div>
    </div>
  );
}
