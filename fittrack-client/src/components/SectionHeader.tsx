import type { LucideIcon } from "lucide-react";
import { Plus } from "lucide-react";

/** Consistent header for each one-page section: icon + condensed title + optional action. */
export function SectionHeader({
  icon: Icon,
  title,
  action,
}: {
  icon: LucideIcon;
  title: string;
  action?: { label: string; onClick: () => void; disabled?: boolean };
}) {
  return (
    <div className="mb-4 flex items-center justify-between">
      <h2 className="flex items-center gap-2.5 font-cond text-2xl font-bold uppercase tracking-wide text-neutral-100">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent/12 text-accent">
          <Icon size={18} strokeWidth={2.25} />
        </span>
        {title}
      </h2>
      {action && (
        <button
          onClick={action.onClick}
          disabled={action.disabled}
          className="flex items-center gap-1.5 rounded-xl bg-accent px-3.5 py-2 text-sm font-semibold text-accentink shadow-glow transition-all hover:brightness-110 disabled:opacity-40 disabled:shadow-none"
        >
          <Plus size={16} strokeWidth={2.5} /> {action.label}
        </button>
      )}
    </div>
  );
}
