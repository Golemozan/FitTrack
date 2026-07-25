import type { LucideIcon } from "lucide-react";
import { Plus } from "lucide-react";

/**
 * Consistent header for each section: icon + condensed title + optional action.
 * The action is the one saturated fill allowed on a screen — everything else
 * uses a soft accent, so this stays readable as "the primary thing to do".
 */
export function SectionHeader({
  icon: Icon,
  title,
  action,
  className = "mb-4",
}: {
  icon: LucideIcon;
  title: string;
  action?: { label: string; onClick: () => void; disabled?: boolean };
  /** Override the default bottom margin when the caller owns the spacing. */
  className?: string;
}) {
  return (
    <div className={`flex flex-wrap items-center justify-between gap-3 ${className}`}>
      <h2 className="flex items-center gap-2.5 font-cond text-xl font-bold uppercase tracking-wide text-neutral-100">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-accent/12 text-accent">
          <Icon size={18} strokeWidth={2.25} />
        </span>
        {title}
      </h2>
      {action && (
        <button
          onClick={action.onClick}
          disabled={action.disabled}
          className="flex h-10 items-center gap-1.5 rounded-xl bg-accent px-4 text-sm font-semibold text-accentink transition-all hover:brightness-110 disabled:opacity-40"
        >
          <Plus size={16} strokeWidth={2.5} /> {action.label}
        </button>
      )}
    </div>
  );
}
