import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { Minus, Plus, Timer, X } from "lucide-react";

/* ────────────────────────────────────────────────────────────────
   Dinlenme sayacı. Set "tamamlandı" işaretlenince başlar; ekranın altında yüzer.

   Süre bitiş anı (Date.now tabanlı) olarak tutulur, kalan süre her karede ondan hesaplanır:
   telefon ekranı kilitlenip açılınca ya da sekme arka plana düşünce sayaç kaymaz.
   Bitişte titreşim + kısa iki bip. Varsayılan süre cihazda saklanır.
   ──────────────────────────────────────────────────────────────── */

const DURATION_KEY = "fittrack:rest-seconds";
const PRESETS = [60, 90, 120, 180];

interface RestTimerValue {
  start: (seconds?: number) => void;
  stop: () => void;
}

const RestTimerContext = createContext<RestTimerValue | null>(null);

/** Sağlayıcı yoksa (ör. test, tek başına bölüm) sessizce hiçbir şey yapmaz. */
// eslint-disable-next-line react-refresh/only-export-components
export function useRestTimer(): RestTimerValue {
  return useContext(RestTimerContext) ?? { start: () => {}, stop: () => {} };
}

function readDefault() {
  try {
    const v = Number(localStorage.getItem(DURATION_KEY));
    return PRESETS.includes(v) ? v : 90;
  } catch {
    return 90;
  }
}

function beep() {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    const ctx = new Ctx();
    [0, 0.22].forEach((offset) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.frequency.value = 880;
      gain.gain.setValueAtTime(0.0001, ctx.currentTime + offset);
      gain.gain.exponentialRampToValueAtTime(0.25, ctx.currentTime + offset + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + offset + 0.16);
      osc.connect(gain).connect(ctx.destination);
      osc.start(ctx.currentTime + offset);
      osc.stop(ctx.currentTime + offset + 0.18);
    });
    setTimeout(() => ctx.close(), 800);
  } catch {
    /* ses açılamazsa titreşim ve görsel yeter */
  }
}

export function RestTimerProvider({ children }: { children: ReactNode }) {
  const [endAt, setEndAt] = useState<number | null>(null);
  const [duration, setDuration] = useState(readDefault);
  const [remaining, setRemaining] = useState(0);
  const [done, setDone] = useState(false);
  const firedRef = useRef(false);

  const start = useCallback((seconds?: number) => {
    const s = seconds ?? readDefault();
    setDuration(s);
    setEndAt(Date.now() + s * 1000);
    setRemaining(s);
    setDone(false);
    firedRef.current = false;
  }, []);

  const stop = useCallback(() => {
    setEndAt(null);
    setDone(false);
  }, []);

  useEffect(() => {
    if (endAt == null) return;
    const tick = () => {
      const left = Math.max(0, Math.ceil((endAt - Date.now()) / 1000));
      setRemaining(left);
      if (left === 0 && !firedRef.current) {
        firedRef.current = true;
        setDone(true);
        navigator.vibrate?.([220, 100, 220]);
        beep();
      }
    };
    tick();
    const id = window.setInterval(tick, 250);
    return () => window.clearInterval(id);
  }, [endAt]);

  // Bittikten 5 sn sonra kendiliğinden kapanır.
  useEffect(() => {
    if (!done) return;
    const id = window.setTimeout(stop, 5000);
    return () => window.clearTimeout(id);
  }, [done, stop]);

  // Önceki değerden hesaplanır: art arda hızlı dokunuşlar birbirini ezmesin (her biri bir öncekine eklenir).
  const adjust = (delta: number) => {
    setEndAt((prev) => (prev == null ? prev : Math.max(Date.now() + 5000, prev + delta * 1000)));
    setDuration((d) => Math.max(5, d + delta));
    firedRef.current = false;
    setDone(false);
  };

  const choosePreset = (s: number) => {
    try {
      localStorage.setItem(DURATION_KEY, String(s));
    } catch {
      /* saklanamazsa yalnız bu sefer geçerli */
    }
    start(s);
  };

  return (
    <RestTimerContext.Provider value={{ start, stop }}>
      {children}
      {endAt != null && (
        <TimerCard
          remaining={remaining}
          duration={duration}
          done={done}
          onAdjust={adjust}
          onPreset={choosePreset}
          onClose={stop}
        />
      )}
    </RestTimerContext.Provider>
  );
}

function TimerCard({ remaining, duration, done, onAdjust, onPreset, onClose }: {
  remaining: number;
  duration: number;
  done: boolean;
  onAdjust: (d: number) => void;
  onPreset: (s: number) => void;
  onClose: () => void;
}) {
  const mm = Math.floor(remaining / 60);
  const ss = String(remaining % 60).padStart(2, "0");
  const R = 22;
  const C = 2 * Math.PI * R;
  const progress = duration > 0 ? remaining / duration : 0;

  return (
    <div
      role="timer"
      aria-live="polite"
      aria-label={done ? "Dinlenme bitti" : `Dinlenme: ${mm} dakika ${remaining % 60} saniye`}
      className="fixed inset-x-0 z-[45] mx-auto w-[min(22rem,calc(100%-2rem))] rounded-[var(--radius-card)] bg-navy p-3 text-snow shadow-float lg:inset-x-auto lg:bottom-6 lg:right-6"
      style={{ bottom: "calc(env(safe-area-inset-bottom) + 5.75rem)" }}
    >
      <div className="flex items-center gap-3">
        <span className="relative flex h-14 w-14 shrink-0 items-center justify-center">
          <svg viewBox="0 0 56 56" className="absolute inset-0 -rotate-90" aria-hidden="true">
            <circle cx="28" cy="28" r={R} fill="none" stroke="currentColor" strokeOpacity="0.15" strokeWidth="5" />
            <circle
              cx="28" cy="28" r={R} fill="none" stroke="var(--color-accent)" strokeWidth="5" strokeLinecap="round"
              strokeDasharray={C} strokeDashoffset={C * (1 - progress)}
              style={{ transition: "stroke-dashoffset 250ms linear" }}
            />
          </svg>
          <Timer size={18} className={done ? "text-accent" : "text-snow/70"} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold text-snow/60">{done ? "Dinlenme bitti" : "Dinlenme"}</p>
          <p className="num text-[28px] leading-none">{done ? "Sıradaki set 💪" : `${mm}:${ss}`}</p>
        </div>
        {!done && (
          <div className="flex items-center gap-1">
            <button type="button" onClick={() => onAdjust(-15)} aria-label="15 saniye azalt" className="flex h-10 w-10 items-center justify-center rounded-full bg-snow/10 hover:bg-snow/20">
              <Minus size={16} />
            </button>
            <button type="button" onClick={() => onAdjust(15)} aria-label="15 saniye ekle" className="flex h-10 w-10 items-center justify-center rounded-full bg-snow/10 hover:bg-snow/20">
              <Plus size={16} />
            </button>
          </div>
        )}
        <button type="button" onClick={onClose} aria-label="Sayacı kapat" className="flex h-10 w-10 items-center justify-center rounded-full text-snow/70 hover:bg-snow/10 hover:text-snow">
          <X size={18} />
        </button>
      </div>
      {!done && (
        <div className="mt-2.5 flex gap-1.5" role="radiogroup" aria-label="Varsayılan dinlenme süresi">
          {PRESETS.map((s) => (
            <button
              key={s}
              type="button"
              role="radio"
              aria-checked={duration === s}
              onClick={() => onPreset(s)}
              className={`min-h-9 flex-1 rounded-full text-xs font-semibold transition-colors ${duration === s ? "bg-accent text-accentink" : "bg-snow/10 text-snow/75 hover:bg-snow/20"}`}
            >
              {s < 120 ? `${s} sn` : `${s / 60} dk`}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
