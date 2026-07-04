import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { weightApi } from "../api/weight";
import type { LogWeightRequest } from "../types";

export function useWeightToday() {
  return useQuery({ queryKey: ["weight", "today"], queryFn: weightApi.today });
}

export function useWeightHistory(days = 30) {
  return useQuery({
    queryKey: ["weight", "history", days],
    queryFn: () => weightApi.history(days),
  });
}

export function useWeightStats() {
  return useQuery({ queryKey: ["weight", "stats"], queryFn: weightApi.stats });
}

export function useRecentWeightLogs(take = 10) {
  return useQuery({
    queryKey: ["weight", "logs", take],
    queryFn: () => weightApi.logs(take),
  });
}

export function useLogWeight() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogWeightRequest) => weightApi.log(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["weight"] }),
  });
}

export function useUpdateWeight() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: LogWeightRequest }) =>
      weightApi.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["weight"] }),
  });
}

export function useDeleteWeight() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => weightApi.deleteLog(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["weight"] }),
  });
}
