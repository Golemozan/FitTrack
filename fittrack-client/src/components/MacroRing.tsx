import { Cell, Pie, PieChart, ResponsiveContainer } from "recharts";

export default function MacroRing({
  consumed,
  goal,
}: {
  consumed: number;
  goal: number;
}) {
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
            innerRadius={72}
            outerRadius={92}
            startAngle={90}
            endAngle={-270}
            stroke="none"
            paddingAngle={0}
          >
            <Cell fill={over ? "#F26D5B" : "#FF5A2C"} />
            <Cell fill="rgba(255,255,255,0.08)" />
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
        <span className="num text-5xl text-neutral-900 dark:text-neutral-100">
          {Math.max(0, Math.round(remaining))}
        </span>
        <span className="eyebrow text-[11px] text-neutral-400">kalan kcal</span>
      </div>
    </div>
  );
}
