import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { goalsApi } from "../api/goals";
import type { UpdateGoalsRequest } from "../types";

export function useGoals() {
  return useQuery({ queryKey: ["goals"], queryFn: goalsApi.get });
}

export function useUpdateGoals() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateGoalsRequest) => goalsApi.update(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["goals"] });
      qc.invalidateQueries({ queryKey: ["nutrition"] });
    },
  });
}
