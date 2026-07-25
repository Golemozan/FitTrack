import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
} from "react";
import type { LucideIcon } from "lucide-react";

/* ────────────────────────────────────────────────────────────────
   Design tokens

   FitTrack is permanently dark (see ThemeContext) — no `dark:`
   variants anywhere below. Three rules hold the screens together:

   1. Radius scale: 2xl = surface, xl = control, full = pill.
   2. One hairline, one field look. Nothing redefines them locally.
   3. The accent is a *soft* fill (accent/12–20) everywhere except
      the single primary action on a screen. Saturated fills stop
      meaning "important" once everything wears one.
   ──────────────────────────────────────────────────────────────── */

/** Hairline shared by every bordered surface. */
export const HAIRLINE = "border-white/[0.07]";

/** One definition of what a text/number field looks like. */
export const fieldCls =
  "w-full rounded-xl border border-white/[0.07] bg-black/25 px-3 py-2.5 text-sm text-neutral-100 outline-none transition-colors placeholder:text-neutral-600 focus:border-accent/50 focus:bg-black/35 disabled:opacity-40";

// Retained for source compatibility: the palette is mono-accent, so every
// chromatic name resolves to the same brand colour.
type Accent = "blue" | "green" | "orange" | "neutral";

/* ---------- Surfaces ---------- */

export function Card({
  children,
  className = "",
  flat = false,
  padded = true,
}: {
  children: ReactNode;
  className?: string;
  /**
   * Drop the glass blur and drop shadow. Use inside lists and overlays —
   * stacked blur/shadow across many adjacent cards compounds into a heavy,
   * busy surface, which is the opposite of what these screens want.
   */
  flat?: boolean;
  padded?: boolean;
}) {
  const surface = flat
    ? "border-white/[0.06] bg-white/[0.025]"
    : "border-white/[0.08] bg-white/[0.04] shadow-lg shadow-black/20 backdrop-blur-xl";
  return (
    <div className={`rounded-2xl border ${surface} ${padded ? "p-5" : ""} ${className}`}>{children}</div>
  );
}

export function SectionTitle({ children, accent = "neutral" }: { children: ReactNode; accent?: Accent }) {
  return (
    <h2 className={`eyebrow mb-3 text-[12px] ${accent === "neutral" ? "text-neutral-400" : "text-accent"}`}>
      {children}
    </h2>
  );
}

/* ---------- Controls ---------- */

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  accent?: Accent;
  variant?: "solid" | "soft" | "ghost";
}

export function Button({ variant = "solid", className = "", ...props }: ButtonProps) {
  const base =
    "rounded-xl px-4 py-2.5 text-sm font-semibold transition-colors disabled:opacity-40 disabled:cursor-not-allowed";
  const styles: Record<NonNullable<ButtonProps["variant"]>, string> = {
    solid: "bg-accent text-accentink hover:brightness-110",
    soft: "bg-accent/12 text-accent hover:bg-accent/20",
    ghost: "text-neutral-300 hover:bg-white/[0.06]",
  };
  return <button className={`${base} ${styles[variant]} ${className}`} {...props} />;
}

type IconTone = "default" | "accent" | "danger" | "active";

const iconTone: Record<IconTone, string> = {
  default: "text-neutral-400 hover:bg-white/[0.07] hover:text-neutral-100",
  accent: "bg-accent/12 text-accent hover:bg-accent/20",
  danger: "text-neutral-500 hover:bg-gain/15 hover:text-gain",
  active: "bg-accent/20 text-accent",
};

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon: LucideIcon;
  tone?: IconTone;
  size?: number;
}

/**
 * Icon-only action with a real 36px touch target. Bare `<Icon>` elements as
 * buttons give a ~14px hit area, which is unusable on a phone — especially
 * for destructive actions sitting next to a primary one.
 */
export function IconButton({ icon: Icon, tone = "default", size = 16, className = "", ...props }: IconButtonProps) {
  return (
    <button
      type="button"
      className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl transition-colors disabled:opacity-40 ${iconTone[tone]} ${className}`}
      {...props}
    >
      <Icon size={size} />
    </button>
  );
}

export interface SegmentOption<T> {
  value: T;
  label: ReactNode;
  icon?: LucideIcon;
}

/** Segmented control — the single visual language for every tab row. */
export function Segmented<T extends string | number>({
  value,
  onChange,
  options,
  size = "md",
}: {
  value: T;
  onChange: (v: T) => void;
  options: SegmentOption<T>[];
  size?: "sm" | "md";
}) {
  const pad = size === "sm" ? "px-2.5 py-1.5 text-xs" : "px-3.5 py-2 text-sm";
  return (
    <div className="inline-flex gap-1 rounded-xl border border-white/[0.06] bg-black/20 p-1">
      {options.map((o) => {
        const active = o.value === value;
        const Icon = o.icon;
        return (
          <button
            key={String(o.value)}
            type="button"
            onClick={() => onChange(o.value)}
            className={`flex items-center gap-1.5 rounded-lg font-medium transition-colors ${pad} ${
              active ? "bg-accent/15 text-accent" : "text-neutral-400 hover:text-neutral-100"
            }`}
          >
            {Icon && <Icon size={14} />}
            {o.label}
          </button>
        );
      })}
    </div>
  );
}

type ChipTone = "neutral" | "accent" | "pro" | "carb" | "fat";

const chipTone: Record<ChipTone, string> = {
  neutral: "bg-white/[0.06] text-neutral-300",
  accent: "bg-accent/12 text-accent",
  pro: "bg-pro/12 text-pro",
  carb: "bg-carb/12 text-carb",
  fat: "bg-fat/12 text-fat",
};

/** Small pill label. Soft fill by design — a chip never shouts. */
export function Chip({
  children,
  tone = "neutral",
  className = "",
}: {
  children: ReactNode;
  tone?: ChipTone;
  className?: string;
}) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${chipTone[tone]} ${className}`}
    >
      {children}
    </span>
  );
}

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
}

export function Input({ label, className = "", ...props }: InputProps) {
  return (
    <label className="flex w-full flex-col gap-1.5 text-sm">
      {label && <span className="text-neutral-400">{label}</span>}
      <input className={`${fieldCls} ${className}`} {...props} />
    </label>
  );
}

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  children: ReactNode;
}

export function Select({ label, children, className = "", ...props }: SelectProps) {
  return (
    <label className="flex w-full flex-col gap-1.5 text-sm">
      {label && <span className="text-neutral-400">{label}</span>}
      <select className={`${fieldCls} ${className}`} {...props}>
        {children}
      </select>
    </label>
  );
}

/* ---------- Data display ---------- */

export function Stat({
  label,
  value,
  sub,
  accent = "neutral",
}: {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  accent?: Accent;
}) {
  return (
    <Card className="flex flex-col gap-1">
      <span className="eyebrow text-[11px] text-neutral-400">{label}</span>
      <span className={`num text-2xl ${accent === "neutral" ? "text-neutral-100" : "text-accent"}`}>{value}</span>
      {sub != null && <span className="text-xs text-neutral-400">{sub}</span>}
    </Card>
  );
}

export function ProgressBar({
  value,
  max,
  colorClass = "bg-accent",
}: {
  value: number;
  max: number;
  colorClass?: string;
}) {
  const pct = max > 0 ? Math.min(100, (value / max) * 100) : 0;
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/[0.07]">
      <div className={`h-full rounded-full transition-all duration-500 ${colorClass}`} style={{ width: `${pct}%` }} />
    </div>
  );
}

export function Spinner() {
  return (
    <div className="flex justify-center py-8 text-accent">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-current border-t-transparent" />
    </div>
  );
}

export function EmptyState({
  icon: Icon,
  title,
  tone = "empty",
  children,
}: {
  icon?: LucideIcon;
  title?: string;
  /** `error` marks a genuine failure so it stops looking like a normal empty list. */
  tone?: "empty" | "error";
  children?: ReactNode;
}) {
  const isError = tone === "error";
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center">
      {Icon && (
        <div
          className={`rounded-2xl p-3 ${isError ? "bg-gain/10 text-gain" : "bg-white/[0.04] text-neutral-500"}`}
        >
          <Icon size={26} strokeWidth={1.75} />
        </div>
      )}
      {title && (
        <p className={`text-sm font-semibold ${isError ? "text-gain" : "text-neutral-300"}`}>{title}</p>
      )}
      {children && <p className="max-w-xs text-sm text-neutral-500">{children}</p>}
    </div>
  );
}

/** Inline "nothing here yet" line for a sub-list, where a full EmptyState is too heavy. */
export function EmptyLine({ children }: { children: ReactNode }) {
  return <p className="py-2 text-sm text-neutral-600">{children}</p>;
}

export function Skeleton({ className = "" }: { className?: string }) {
  return <div className={`animate-pulse rounded-lg bg-white/[0.05] ${className}`} />;
}

export function ListSkeleton({ rows = 3, height = "h-12" }: { rows?: number; height?: string }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className={`w-full ${height}`} />
      ))}
    </div>
  );
}
