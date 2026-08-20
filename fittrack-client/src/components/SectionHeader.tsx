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
    <div className={`flex flex-wrap items-center justify-between gap-3 pr-12 ${className}`}>
      <h2 className="flex min-w-0 items-center gap-3 text-2xl font-bold leading-tight text-white sm:text-3xl">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-accent/15 text-accent">
          <Icon size={20} strokeWidth={2.25} />
        </span>
        {title}
      </h2>
      {action && (
        <button
          onClick={action.onClick}
          disabled={action.disabled}
          className="flex min-h-10 items-center gap-1.5 whitespace-nowrap rounded-xl bg-accent px-4 text-sm font-semibold text-white transition-[filter,transform] hover:brightness-110 active:scale-[.98] disabled:opacity-40"
        >
          <Plus size={16} strokeWidth={2.5} /> {action.label}
        </button>
      )}
    </div>
  );
}
