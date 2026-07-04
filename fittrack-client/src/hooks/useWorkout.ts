import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { workoutApi } from "../api/workout";
import type {
  AddExerciseRequest,
  AddSetRequest,
  UpdateExerciseRequest,
  UpdateSetRequest,
  WorkoutSession,
} from "../types";

const TODAY_KEY = ["workout", "today"] as const;

export function useTodaySession() {
  return useQuery({ queryKey: ["workout", "today"], queryFn: workoutApi.today });
}

export function useRecentSessions(limit = 10) {
  return useQuery({
    queryKey: ["workout", "sessions", limit],
    queryFn: () => workoutApi.sessions(limit),
  });
}

export function useExerciseCatalog() {
  return useQuery({
    queryKey: ["workout", "catalog"],
    queryFn: workoutApi.catalog,
    staleTime: Infinity,
  });
}

export function useExerciseHistory(name: string, enabled = true) {
  return useQuery({
    queryKey: ["workout", "history", name],
    queryFn: () => workoutApi.history(name),
    enabled: enabled && name.trim().length > 0,
    staleTime: 60_000,
  });
}

function useWorkoutInvalidate() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ["workout"] });
}

export function useCreateSession() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (name: string) => workoutApi.createSession({ name }),
    onSuccess: invalidate,
  });
}

export function useRenameSession() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      workoutApi.renameSession(id, { name }),
    onSuccess: invalidate,
  });
}

export function useDeleteSession() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (id: string) => workoutApi.deleteSession(id),
    onSuccess: invalidate,
  });
}

export function useUpdateExercise() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateExerciseRequest }) =>
      workoutApi.updateExercise(id, body),
    onSuccess: invalidate,
  });
}

export function useAddExercise() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (body: AddExerciseRequest) => workoutApi.addExercise(body),
    onSuccess: invalidate,
  });
}

export function useDeleteExercise() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (id: string) => workoutApi.deleteExercise(id),
    onSuccess: invalidate,
  });
}

export function useAddSet() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (body: AddSetRequest) => workoutApi.addSet(body),
    onSuccess: invalidate,
  });
}

export function useUpdateSet() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateSetRequest }) =>
      workoutApi.updateSet(id, body),
    // Optimistically patch the set in today's session cache (instant ✓ toggle / edits).
    onMutate: async ({ id, body }) => {
      await qc.cancelQueries({ queryKey: TODAY_KEY });
      const prev = qc.getQueryData<WorkoutSession | null>(TODAY_KEY);
      qc.setQueryData<WorkoutSession | null>(TODAY_KEY, (cur) =>
        cur
          ? {
              ...cur,
              exercises: cur.exercises.map((ex) => ({
                ...ex,
                sets: ex.sets.map((s) => (s.id === id ? { ...s, ...body } : s)),
              })),
            }
          : cur
      );
      return { prev };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev !== undefined) qc.setQueryData(TODAY_KEY, ctx.prev);
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ["workout"] }),
  });
}

export function useDeleteSet() {
  const invalidate = useWorkoutInvalidate();
  return useMutation({
    mutationFn: (id: string) => workoutApi.deleteSet(id),
    onSuccess: invalidate,
  });
}
