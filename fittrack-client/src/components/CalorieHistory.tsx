import { useMemo } from "react";
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useNutritionHistory } from "../hooks/useNutrition";
import { useGoals } from "../hooks/useGoals";
import { Card, EmptyState, Skeleton } from "./ui";
import type { DailyNutritionSummary } from "../types";

interface ChartPoint {
  date: string;
  fullDate: string;
  isoDate: string;
  calories: number;
  protein: number;
  carbs: number;
  fat: number;
  mealCount: number;
  ma7: number | null;
  aboveGoal: number;
  belowGoal: number;
}

/** 7-day simple moving average; null for first 6 points. */
function computeMA7(data: DailyNutritionSummary[]): (number | null)[] {
  return data.map((_, i) => {
    if (i < 6) return null;
    const slice = data.slice(i - 6, i + 1);
    return slice.reduce((s, d) => s + d.totalCalories, 0) / 7;
  });
}

interface Props {
  days: number;
  onSelectDay?: (date: string) => void;
  selectedDate?: string | null;
}

export default function CalorieHistory({ days, onSelectDay }: Props) {
  const history = useNutritionHistory(days);
  const goals = useGoals();

  const goal = goals.data?.calorieGoal ?? 0;

  const chartData: ChartPoint[] = useMemo(() => {
    if (!history.data) return [];
    const ma7 = computeMA7(history.data);
    return history.data.map((d, i) => {
      const above = d.totalCalories >= goal ? d.totalCalories : 0;
      const below = d.totalCalories < goal ? d.totalCalories : 0;
      const dateStr = d.date.toString().split("T")[0]; // YYYY-MM-DD
      return {
        date: new Date(d.date).toLocaleDateString("tr-TR", { day: "numeric", month: "short" }),
        fullDate: new Date(d.date).toLocaleDateString("tr-TR", { day: "numeric", month: "long", weekday: "short" }),
        isoDate: dateStr,
        calories: d.totalCalories,
        protein: d.totalProtein,
        carbs: d.totalCarbs,
        fat: d.totalFat,
        mealCount: d.mealCount,
        ma7: ma7[i],
        aboveGoal: above,
        belowGoal: below,
      };
    });
  }, [history.data, goal]);

  const grid = "var(--color-rule)";
  const axis = "var(--color-muted)";

  if (history.isLoading) return <Skeleton className="h-64 w-full" />;

  if (chartData.length === 0) {
    return (
      <Card>
        <EmptyState>Henüz yemek kaydı yok. Birkaç gün log yaptıktan sonra grafik burada belirecek.</EmptyState>
      </Card>
    );
  }

  return (
    <div className="h-72 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={chartData} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke={grid} />
          {goal > 0 && (
            <ReferenceLine
              y={goal}
              stroke="var(--color-accent)"
              strokeDasharray="6 3"
              strokeOpacity={0.6}
              label={{
                value: `hedef ${goal}`,
                position: "insideTopRight",
                fontSize: 10,
                fill: "var(--color-accent)",
              }}
            />
          )}
          <XAxis
            dataKey="date"
            fontSize={11}
            stroke={axis}
            tickLine={false}
            interval={Math.max(0, Math.floor(chartData.length / 6))}
          />
          <YAxis
            fontSize={11}
            stroke={axis}
            tickLine={false}
            width={44}
          />
          <Tooltip content={<CustomTooltip goal={goal} />} />
          {/* Dual-color bars: above goal = accent orange, below goal = green */}
          <Bar
            dataKey="aboveGoal"
            name="Hedef üstü"
            stackId="cal"
            fill="var(--color-danger)"
            radius={[3, 3, 0, 0]}
            maxBarSize={32}
            onClick={(data: any) => onSelectDay?.(data?.isoDate)}
            cursor="pointer"
            opacity={0.85}
          />
          <Bar
            dataKey="belowGoal"
            name="Hedef altı"
            stackId="cal"
            fill="var(--color-success)"
            radius={[3, 3, 0, 0]}
            maxBarSize={32}
            onClick={(data: any) => onSelectDay?.(data?.isoDate)}
            cursor="pointer"
            opacity={0.85}
          />
          {/* 7-day moving average line */}
          <Line
            type="monotone"
            dataKey="ma7"
            name="7 günlük ort."
            stroke="var(--color-protein)"
            strokeWidth={2}
            strokeDasharray="4 3"
            dot={false}
            connectNulls
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}

/** Rich tooltip: full date, calories, P/K/Y, meal count, MA7. */
function CustomTooltip({
  active,
  payload,
  goal,
}: {
  active?: boolean;
  payload?: any[];
  goal: number;
}) {
  if (!active || !payload?.length) return null;
  const d = payload[0]?.payload as ChartPoint | undefined;
  if (!d) return null;

  const diff = goal > 0 ? d.calories - goal : 0;

  return (
    <div
      style={{
        background: "var(--color-ink)",
        color: "var(--color-paper-2)",
        border: "var(--rule-hair) solid var(--color-rule-2)",
        borderRadius: "var(--radius-input)",
        padding: "var(--space-xs) var(--space-sm)",
        fontSize: 12,
        minWidth: 160,
      }}
    >
      <div style={{ fontWeight: 600, marginBottom: "var(--space-3xs)", color: "var(--color-paper-2)" }}>{d.fullDate}</div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "var(--color-rule-2)" }}>Kalori</span>
        <span style={{ fontWeight: 600 }}>
          {Math.round(d.calories)} kcal
          {goal > 0 && (
            <span style={{ marginLeft: "var(--space-3xs)", fontSize: 11, color: diff > 0 ? "var(--color-danger)" : diff < 0 ? "var(--color-success)" : "var(--color-rule-2)" }}>
              {diff > 0 ? "+" : ""}{Math.round(diff)}
            </span>
          )}
        </span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "var(--color-rule-2)" }}>Protein</span>
        <span style={{ color: "var(--color-protein)" }}>{Math.round(d.protein)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "var(--color-rule-2)" }}>Karb</span>
        <span style={{ color: "var(--color-carb)" }}>{Math.round(d.carbs)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "var(--color-rule-2)" }}>Yağ</span>
        <span style={{ color: "var(--color-fat)" }}>{Math.round(d.fat)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "var(--color-rule-2)" }}>Öğün</span>
        <span>{d.mealCount}</span>
      </div>
      {d.ma7 != null && (
        <div style={{ display: "flex", justifyContent: "space-between", gap: "var(--space-sm)", marginTop: "var(--space-3xs)", borderTop: "var(--rule-hair) solid var(--color-rule-2)", paddingTop: "var(--space-3xs)" }}>
          <span style={{ color: "var(--color-rule-2)" }}>7G Ort.</span>
          <span style={{ color: "var(--color-protein)", fontWeight: 600 }}>{Math.round(d.ma7)} kcal</span>
        </div>
      )}
    </div>
  );
}
