import { useEffect, useRef, useState } from "react";
import { Flame } from "lucide-react";

export type Ring = {
  /** Halkanın adı — legend'da ve aria etiketinde geçer. */
  label: string;
  value: number;
  goal: number;
  /** Birim ("kcal", "g", "set") — legend'da sayının yanında. */
  unit: string;
  /** Renk token'ının adı. Her makro kendi aksanına sahiptir. */
  tone: "carb" | "protein" | "fat";
};

const TONE: Record<Ring["tone"], { stroke: string; text: string; dot: string }> = {
  carb: { stroke: "var(--color-carb)", text: "text-carb", dot: "bg-carb" },
  protein: { stroke: "var(--color-protein)", text: "text-pro", dot: "bg-pro" },
  fat: { stroke: "var(--color-fat)", text: "text-fat", dot: "bg-fat" },
};

// Dıştan içe üç eş merkezli yol. Aralarında 2px boşluk kalacak şekilde seçildi.
const RADII = [64, 50, 36];
const STROKE = 12;
const BOX = 160;

/**
 * Günün üç halkası — Apple Fitness registeri.
 *
 * Uydurma metrik yok: üç halka da uygulamanın tuttuğu makro verisinden beslenir.
 * Kalori, halkanın altında ayrı bir toplam olarak gösterilir. Hedefi olmayan bir
 * ölçü halkaya girmez — dolmayan bir halka bilgi değil, süstür.
 */
export default function TodayRings({
  rings,
  calories,
  calorieGoal,
  streakDays,
}: {
  rings: Ring[];
  calories: number;
  calorieGoal: number;
  streakDays: number;
}) {
  const empty = rings.every((ring) => ring.value === 0);

  return (
    <div className="grid min-w-0 gap-7 sm:grid-cols-[auto_minmax(0,1fr)] sm:items-center sm:gap-9">
      <div className="flex min-w-0 flex-col items-center">
        <Rings rings={rings} />
        <div className="mt-3 flex max-w-full items-baseline justify-center gap-2 text-center">
          <span className="num truncate text-3xl text-neutral-100">{Math.round(calories)}</span>
          <span className="tnum shrink-0 text-sm text-neutral-500">/ {Math.round(calorieGoal)} kcal</span>
        </div>
        <span className="mt-1 text-xs text-neutral-500">Bugünün kalorisi</span>
      </div>

      <ul className="flex w-full min-w-0 flex-col border-t border-hair/70 pt-4 sm:border-l sm:border-t-0 sm:pl-7 sm:pt-0">
        {rings.map((ring) => {
          const pct = ring.goal > 0 ? Math.min(1, ring.value / ring.goal) : 0;
          return (
            <li key={ring.label} className="grid min-w-0 grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 border-b border-hair/60 py-3 first:pt-0">
              <span className="flex min-w-0 items-center gap-3">
                <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${TONE[ring.tone].dot}`} aria-hidden="true" />
                <span className="truncate text-sm font-medium text-neutral-300">{ring.label}</span>
              </span>
              <span className="tnum whitespace-nowrap text-right text-sm text-neutral-500">
                <span className={`font-semibold ${TONE[ring.tone].text}`}>{Math.round(ring.value)}</span>
                {ring.goal > 0 ? ` / ${Math.round(ring.goal)}` : ""} {ring.unit}
              </span>
              <span className="col-start-2 mt-1 text-right text-[11px] text-neutral-600">
                {ring.goal > 0 ? `%${Math.round(pct * 100)}` : "Hedef yok"}
              </span>
            </li>
          );
        })}

        {empty && (
          <li className="pt-4 text-sm leading-6 text-neutral-500">
            Bugün henüz öğün kaydı yok. İlk kayıtla halkalar dolmaya başlar.
          </li>
        )}

        {!empty && streakDays > 0 && (
          <li className="pt-4">
            {/* Kutlama #2: seri rozeti. Toast degil — kalici, sessiz, sadece gercekten
                bir seri varken gorunur. Sifirsa hic cizilmez. */}
            <span className="inline-flex items-center gap-2 rounded-full bg-move/[0.12] px-3 py-1.5 text-sm text-move">
              <Flame size={15} aria-hidden="true" />
              <span className="tnum font-semibold">{streakDays} gün</span>
              <span className="text-neutral-400">üst üste kayıt</span>
            </span>
          </li>
        )}
      </ul>
    </div>
  );
}

function Rings({ rings }: { rings: Ring[] }) {
  const summary = rings
    .map((r) => `${r.label} ${Math.round(r.value)} / ${Math.round(r.goal)} ${r.unit}`)
    .join(", ");

  return (
    <svg
      viewBox={`0 0 ${BOX} ${BOX}`}
      className="h-48 w-48 shrink-0 sm:h-52 sm:w-52"
      role="img"
      aria-label={`Bugünün halkaları: ${summary}`}
    >
      {/* Yollar saat 12'den başlasın diye merkez etrafında çeyrek tur geri döndürülür. */}
      <g transform={`rotate(-90 ${BOX / 2} ${BOX / 2})`}>
        {rings.map((ring, i) => (
          <Arc key={ring.label} ring={ring} radius={RADII[i]} />
        ))}
      </g>
    </svg>
  );
}

function Arc({ ring, radius }: { ring: Ring; radius: number }) {
  const circumference = 2 * Math.PI * radius;
  const pct = ring.goal > 0 ? Math.min(1, ring.value / ring.goal) : 0;
  const complete = pct >= 1;

  // Kutlama: halka YENİ kapandığında bir kez. Her render'da değil — yoksa
  // "her kayıtta konfeti" olur ve kutlama anlam kaybeder.
  const [celebrate, setCelebrate] = useState(false);
  const wasComplete = useRef(complete);
  useEffect(() => {
    if (complete && !wasComplete.current) {
      setCelebrate(true);
      const timer = setTimeout(() => setCelebrate(false), 400);
      return () => clearTimeout(timer);
    }
    wasComplete.current = complete;
  }, [complete]);

  const style = {
    "--ring-empty": circumference,
    "--ring-fill": circumference * (1 - pct),
  } as React.CSSProperties;

  return (
    <g
      className={celebrate ? "pop-once" : undefined}
      style={{ transformOrigin: `${BOX / 2}px ${BOX / 2}px` }}
    >
      {/* Boş yol — dolmamış kısmı okunur tutar, aksan rengi taşımaz. */}
      <circle
        cx={BOX / 2}
        cy={BOX / 2}
        r={radius}
        fill="none"
        stroke="var(--color-track)"
        strokeWidth={STROKE}
      />
      <circle
        className="ring-arc"
        cx={BOX / 2}
        cy={BOX / 2}
        r={radius}
        fill="none"
        stroke={TONE[ring.tone].stroke}
        strokeWidth={STROKE}
        strokeLinecap="round"
        strokeDasharray={circumference}
        style={style}
      />
    </g>
  );
}
