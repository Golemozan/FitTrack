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

  const grid = "#221F1A";
  const axis = "#8A8178";

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
              stroke="#FF5A2C"
              strokeDasharray="6 3"
              strokeOpacity={0.6}
              label={{
                value: `hedef ${goal}`,
                position: "insideTopRight",
                fontSize: 10,
                fill: "#FF5A2C",
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
            fill="#FF5A2C"
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
            fill="#22C55E"
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
            stroke="#F59E0B"
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
        background: "#1A1815",
        border: "1px solid rgba(148,163,184,0.3)",
        borderRadius: 12,
        padding: "10px 14px",
        fontSize: 12,
        minWidth: 160,
      }}
    >
      <div style={{ fontWeight: 600, marginBottom: 4, color: "#e2e8f0" }}>{d.fullDate}</div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#94a3b8" }}>Kalori</span>
        <span style={{ fontWeight: 600 }}>
          {Math.round(d.calories)} kcal
          {goal > 0 && (
            <span style={{ marginLeft: 4, fontSize: 11, color: diff > 0 ? "#ef4444" : diff < 0 ? "#22C55E" : "#94a3b8" }}>
              {diff > 0 ? "+" : ""}{Math.round(diff)}
            </span>
          )}
        </span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#94a3b8" }}>Protein</span>
        <span style={{ color: "#60a5fa" }}>{Math.round(d.protein)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#94a3b8" }}>Karb</span>
        <span style={{ color: "#fbbf24" }}>{Math.round(d.carbs)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#94a3b8" }}>Yağ</span>
        <span style={{ color: "#f472b6" }}>{Math.round(d.fat)}g</span>
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#94a3b8" }}>Öğün</span>
        <span>{d.mealCount}</span>
      </div>
      {d.ma7 != null && (
        <div style={{ display: "flex", justifyContent: "space-between", gap: 16, marginTop: 2, borderTop: "1px solid rgba(148,163,184,0.15)", paddingTop: 4 }}>
          <span style={{ color: "#94a3b8" }}>7G Ort.</span>
          <span style={{ color: "#F59E0B", fontWeight: 600 }}>{Math.round(d.ma7)} kcal</span>
        </div>
      )}
    </div>
  );
}
