import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { nutritionApi } from "../api/nutrition";
import type { LogFoodRequest } from "../types";

export function useTodayMeals() {
  return useQuery({ queryKey: ["nutrition", "today"], queryFn: nutritionApi.today });
}

export function useNutritionSummary(date?: string) {
  return useQuery({
    queryKey: ["nutrition", "summary", date ?? "today"],
    queryFn: () => nutritionApi.summary(date),
  });
}

export function useLogFood() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogFoodRequest) => nutritionApi.log(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["nutrition"] }),
  });
}

export function useUpdateMeal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: LogFoodRequest }) =>
      nutritionApi.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["nutrition"] }),
  });
}

export function useDeleteMeal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => nutritionApi.deleteLog(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["nutrition"] }),
  });
}
