import { Cell, Pie, PieChart, ResponsiveContainer } from "recharts";

/**
 * Calorie ring. The fill rides the brand cyan→violet gradient rather than a
 * one-off hex, so the largest element on the nutrition screen belongs to the
 * same colour identity as everything around it. Over-goal switches to the
 * `gain` rose — the one place on this screen allowed to read as a warning.
 */
export default function MacroRing({ consumed, goal }: { consumed: number; goal: number }) {
  const safeGoal = goal > 0 ? goal : 1;
  const eaten = Math.min(consumed, safeGoal);
  const remaining = Math.max(0, safeGoal - consumed);
  const over = consumed > safeGoal;

  const data = [
    { name: "eaten", value: over ? safeGoal : eaten },
    { name: "remaining", value: over ? 0 : remaining },
  ];

  return (
    <div className="relative mx-auto h-48 w-48">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <defs>
            <linearGradient id="macroRingFill" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" stopColor="#22D3EE" />
              <stop offset="100%" stopColor="#A855F7" />
            </linearGradient>
          </defs>
          <Pie
            data={data}
            dataKey="value"
            innerRadius={74}
            outerRadius={92}
            startAngle={90}
            endAngle={-270}
            stroke="none"
            cornerRadius={9}
            paddingAngle={0}
          >
            <Cell fill={over ? "#FB7185" : "url(#macroRingFill)"} />
            <Cell fill="rgba(255,255,255,0.06)" />
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-1">
        <span className={`num text-5xl ${over ? "text-gain" : "text-neutral-100"}`}>
          {over ? `+${Math.round(consumed - safeGoal)}` : Math.round(remaining)}
        </span>
        <span className="eyebrow text-[10px] text-neutral-500">{over ? "aşıldı" : "kalan kcal"}</span>
      </div>
    </div>
  );
}
