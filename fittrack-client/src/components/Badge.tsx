import {
  Barbell, Bookmarks, CalendarCheck, Crown, Drop, Egg, Fire, Flag, ForkKnife, Lightning, Lock, Mountains,
  Scales, Smiley, Sparkle, Star, SunHorizon, Target, Trophy,
} from "@phosphor-icons/react";
import type { Icon } from "@phosphor-icons/react";
import type { AchievementCategory } from "../types";

/* Rozet: madalya. Dış halka kademe (bronz/gümüş/altın), iç daire kategorinin rengi, ortada ikon.
   İkonlar Phosphor Icons (MIT lisansı) — tek elden çıkmış tutarlı bir set; dolu (fill) ağırlık rozette
   okunaklı. Kilitli rozet griye döner ve köşede kilit taşır; renk tek başına anlam taşımaz,
   kademe ayrıca alttaki noktalarla da gösterilir. */

// eslint-disable-next-line react-refresh/only-export-components
export const BADGE_ICONS: Record<string, Icon> = {
  ForkKnife, Fire, Egg, Target, SunHorizon, Barbell, CalendarCheck, Mountains, Trophy, Lightning,
  Bookmarks, Scales, Flag, Crown, Smiley, Drop, Sparkle, Star,
};

// eslint-disable-next-line react-refresh/only-export-components
export const TIER_NAMES = ["", "Bronz", "Gümüş", "Altın"];

/** Kademe halkası. Metal tonları temadan bağımsız — madalya her temada madalya gibi görünsün. */
const TIER_RING = ["", "oklch(64% 0.11 55)", "oklch(78% 0.02 260)", "oklch(83% 0.15 88)"];
const TIER_RING_DARK = ["", "oklch(48% 0.09 50)", "oklch(60% 0.02 260)", "oklch(66% 0.14 75)"];

const CATEGORY: Record<AchievementCategory, { fill: string; ink: string }> = {
  nutrition: { fill: "var(--color-move)", ink: "oklch(var(--navy-channels))" },
  workout: { fill: "var(--color-lift)", ink: "oklch(var(--snow-channels))" },
  weight: { fill: "var(--color-flow)", ink: "oklch(var(--navy-channels))" },
  mind: { fill: "var(--color-lav)", ink: "oklch(var(--navy-channels))" },
  general: { fill: "var(--color-accent)", ink: "oklch(var(--navy-channels))" },
};

export function Badge({
  icon,
  category,
  level,
  maxLevel,
  size = 72,
  className = "",
}: {
  icon: string;
  category: AchievementCategory;
  level: number;
  maxLevel: number;
  size?: number;
  className?: string;
}) {
  const Glyph = BADGE_ICONS[icon] ?? Star;
  const locked = level === 0;
  // Tek kademeli başarım açılınca altın sayılır (tamamlanmış madalya).
  const tier = locked ? 0 : maxLevel === 1 ? 3 : maxLevel === 2 && level === 2 ? 3 : level;
  const cat = CATEGORY[category];
  const ring = locked ? "var(--color-track)" : TIER_RING[tier];
  const ringShade = locked ? "var(--color-rule-2)" : TIER_RING_DARK[tier];

  return (
    <span className={`relative inline-flex shrink-0 ${className}`} style={{ width: size, height: size }} aria-hidden="true">
      <svg viewBox="0 0 96 96" width={size} height={size}>
        {/* Dış halka: altında hafif koyu ton → madalyaya kalınlık hissi (gölge değil, düz iki ton). */}
        <circle cx="48" cy="50" r="44" fill={ringShade} />
        <circle cx="48" cy="47" r="44" fill={ring} />
        {/* Kademe çentikleri: halkanın üstünde 12 kısa çizgi, madalya kenarı. */}
        {!locked &&
          Array.from({ length: 12 }).map((_, i) => {
            const a = (i / 12) * Math.PI * 2;
            return (
              <line key={i} x1={48 + Math.cos(a) * 39} y1={47 + Math.sin(a) * 39} x2={48 + Math.cos(a) * 42.5} y2={47 + Math.sin(a) * 42.5}
                stroke={ringShade} strokeWidth="2.5" strokeLinecap="round" />
            );
          })}
        <circle cx="48" cy="47" r="34" fill={locked ? "var(--color-paper-3)" : cat.fill} />
      </svg>
      <Glyph
        weight="fill"
        size={size * 0.38}
        className="absolute left-1/2 -translate-x-1/2"
        style={{ top: size * 0.3, color: locked ? "var(--color-muted)" : cat.ink, opacity: locked ? 0.55 : 1 }}
      />
      {locked && (
        <span
          className="absolute flex items-center justify-center rounded-full bg-panel text-neutral-400 shadow-card"
          style={{ right: 0, bottom: size * 0.04, width: size * 0.32, height: size * 0.32 }}
        >
          <Lock weight="fill" size={size * 0.17} />
        </span>
      )}
    </span>
  );
}

/** Kademe noktaları: kaç kademeden kaçı açık. Rengin yanında sayı da veren ikinci kanal. */
export function TierDots({ level, maxLevel }: { level: number; maxLevel: number }) {
  if (maxLevel <= 1) return null;
  return (
    <span className="flex items-center gap-1" aria-label={`${maxLevel} kademeden ${level} açık`}>
      {Array.from({ length: maxLevel }).map((_, i) => (
        <span key={i} className={`h-1.5 w-4 rounded-full ${i < level ? "bg-neutral-100" : "bg-track"}`} />
      ))}
    </span>
  );
}
