import type { LucideIcon } from "lucide-react";
import { Plus } from "lucide-react";

/**
 * Consistent work-area header: inline icon + title + optional primary action.
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
    <div className={`flex flex-wrap items-center justify-between gap-4 border-b border-hair/70 pb-5 pr-12 ${className}`}>
      <h2 className="flex min-w-0 items-center gap-3 text-2xl font-bold leading-tight text-neutral-100 sm:text-3xl">
        <Icon size={22} strokeWidth={2.1} className="shrink-0 text-accent" />
        {title}
      </h2>
      {action && (
        <button
          onClick={action.onClick}
          disabled={action.disabled}
          className="press flex min-h-11 items-center gap-1.5 whitespace-nowrap rounded-xl bg-accent px-4 text-sm font-semibold text-accentink hover:brightness-110 disabled:opacity-40"
        >
          <Plus size={16} strokeWidth={2.5} /> {action.label}
        </button>
      )}
    </div>
  );
}
