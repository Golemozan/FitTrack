import { Cell, Pie, PieChart, ResponsiveContainer } from "recharts";

/**
 * Calorie ring. The single signal colour marks progress; over-goal switches
 * to the semantic danger token. Decorative gradients are intentionally absent.
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
            <Cell fill={over ? "var(--color-danger)" : "var(--color-accent)"} />
            <Cell fill="var(--color-paper-3)" />
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
