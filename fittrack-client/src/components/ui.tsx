import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
} from "react";
import type { LucideIcon } from "lucide-react";

// The palette is deliberately mono-accent: every chromatic accent resolves to
// the molten brand color. Macro/semantic hues live on their own tokens.
type Accent = "blue" | "green" | "orange" | "neutral";

const accentText: Record<Accent, string> = {
  blue: "text-accent",
  green: "text-accent",
  orange: "text-accent",
  neutral: "text-neutral-500 dark:text-neutral-400",
};

const accentBtn: Record<Accent, string> = {
  blue: "bg-accent-grad text-accentink hover:brightness-110 shadow-glow",
  green: "bg-accent-grad text-accentink hover:brightness-110 shadow-glow",
  orange: "bg-accent-grad text-accentink hover:brightness-110 shadow-glow",
  neutral: "bg-white/[0.06] hover:bg-white/10 text-neutral-100",
};

export function Card({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`rounded-2xl border border-white/[0.08] bg-white/[0.04] p-5 shadow-lg shadow-black/20 backdrop-blur-xl ${className}`}
    >
      {children}
    </div>
  );
}

export function SectionTitle({
  children,
  accent = "neutral",
}: {
  children: ReactNode;
  accent?: Accent;
}) {
  return (
    <h2 className={`eyebrow mb-3 text-[13px] ${accent === "neutral" ? "text-neutral-400" : accentText[accent]}`}>
      {children}
    </h2>
  );
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  accent?: Accent;
  variant?: "solid" | "ghost";
}

export function Button({
  accent = "blue",
  variant = "solid",
  className = "",
  ...props
}: ButtonProps) {
  const base =
    "rounded-xl px-4 py-2 text-sm font-semibold transition-all duration-200 disabled:opacity-40 disabled:cursor-not-allowed";
  const style =
    variant === "ghost"
      ? "bg-transparent text-neutral-600 hover:bg-black/5 dark:text-neutral-300 dark:hover:bg-white/5"
      : accentBtn[accent];
  return <button className={`${base} ${style} ${className}`} {...props} />;
}

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
}

export function Input({ label, className = "", ...props }: InputProps) {
  return (
    <label className="flex w-full flex-col gap-1 text-sm">
      {label && <span className="text-neutral-500 dark:text-neutral-400">{label}</span>}
      <input
        className={`rounded-xl border border-black/10 bg-white px-3 py-2 text-neutral-900 outline-none transition-colors focus:border-accent dark:border-hair dark:bg-card2 dark:text-neutral-100 dark:focus:border-accent ${className}`}
        {...props}
      />
    </label>
  );
}

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  children: ReactNode;
}

export function Select({ label, children, className = "", ...props }: SelectProps) {
  return (
    <label className="flex w-full flex-col gap-1 text-sm">
      {label && <span className="text-neutral-500 dark:text-neutral-400">{label}</span>}
      <select
        className={`rounded-xl border border-black/10 bg-white px-3 py-2 text-neutral-900 outline-none transition-colors focus:border-accent dark:border-hair dark:bg-card2 dark:text-neutral-100 ${className}`}
        {...props}
      >
        {children}
      </select>
    </label>
  );
}

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
      <span className={`num text-2xl ${accent === "neutral" ? "text-neutral-900 dark:text-neutral-100" : accentText[accent]}`}>
        {value}
      </span>
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
    <div className="h-2 w-full overflow-hidden rounded-full bg-black/[0.08] dark:bg-card2">
      <div className={`h-full rounded-full transition-all ${colorClass}`} style={{ width: `${pct}%` }} />
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
  children,
}: {
  icon?: LucideIcon;
  title?: string;
  children?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center">
      {Icon && (
        <div className="rounded-2xl bg-black/[0.05] p-3 text-neutral-400 dark:bg-card2">
          <Icon size={28} strokeWidth={1.75} />
        </div>
      )}
      {title && <p className="text-sm font-semibold text-neutral-600 dark:text-neutral-300">{title}</p>}
      {children && <p className="text-sm text-neutral-400">{children}</p>}
    </div>
  );
}

export function Skeleton({ className = "" }: { className?: string }) {
  return <div className={`animate-pulse rounded-lg bg-black/[0.06] dark:bg-card2 ${className}`} />;
}

export function ListSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-12 w-full" />
      ))}
    </div>
  );
}
