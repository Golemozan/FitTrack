import api from "./axios";
import type {
  LogFoodRequest,
  MealEntry,
  NutritionSummary,
  TodayMeals,
} from "../types";

export const nutritionApi = {
  log: (body: LogFoodRequest) =>
    api.post<MealEntry>("/nutrition/log", body).then((r) => r.data),

  update: (id: string, body: LogFoodRequest) =>
    api.put<MealEntry>(`/nutrition/log/${id}`, body).then((r) => r.data),

  today: () => api.get<TodayMeals>("/nutrition/today").then((r) => r.data),

  summary: (date?: string) =>
    api
      .get<NutritionSummary>("/nutrition/summary", { params: date ? { date } : {} })
      .then((r) => r.data),

  deleteLog: (id: string) => api.delete(`/nutrition/log/${id}`).then((r) => r.data),
};
