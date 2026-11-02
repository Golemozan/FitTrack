import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { achievementsApi, habitsApi, homeLayoutApi, summaryApi, templatesApi } from "../api/habits";
import type { Habit, HabitRequest, HomeWidget } from "../types";

const HABITS = ["habits"] as const;

export function useHabits() {
  return useQuery({ queryKey: HABITS, queryFn: () => habitsApi.list() });
}

/** Sayaç anında güncellenir (iyimser); sunucu reddederse eski değere döner. Su bardağına art arda dokunmak beklemez. */
export function useLogHabit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, count }: { id: string; count: number }) => habitsApi.log(id, count),
    onMutate: async ({ id, count }) => {
      await qc.cancelQueries({ queryKey: HABITS });
      const prev = qc.getQueryData<Habit[]>(HABITS);
      qc.setQueryData<Habit[]>(HABITS, (cur) => cur?.map((h) => (h.id === id ? { ...h, count } : h)));
      return { prev };
    },
    onError: (_e, _v, ctx) => ctx?.prev && qc.setQueryData(HABITS, ctx.prev),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: HABITS });
      qc.invalidateQueries({ queryKey: ["summary"] });
    },
  });
}

export function useCreateHabit() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (b: HabitRequest) => habitsApi.create(b), onSuccess: () => qc.invalidateQueries({ queryKey: HABITS }) });
}

export function useUpdateHabit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: HabitRequest }) => habitsApi.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: HABITS }),
  });
}

export function useArchiveHabit() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => habitsApi.archive(id), onSuccess: () => qc.invalidateQueries({ queryKey: HABITS }) });
}

export function useTemplates() {
  return useQuery({ queryKey: ["workout", "templates"], queryFn: templatesApi.list });
}

export function useSaveTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ name, sessionId }: { name: string; sessionId: string }) => templatesApi.save(name, sessionId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout", "templates"] }),
  });
}

export function useApplyTemplate() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => templatesApi.apply(id), onSuccess: () => qc.invalidateQueries({ queryKey: ["workout"] }) });
}

export function useDeleteTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => templatesApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout", "templates"] }),
  });
}

export function useWeeklySummary(enabled = true) {
  return useQuery({ queryKey: ["summary", "weekly"], queryFn: summaryApi.weekly, enabled, staleTime: 60_000 });
}

export function useAchievements() {
  return useQuery({ queryKey: ["achievements"], queryFn: achievementsApi.list, staleTime: 30_000, refetchOnWindowFocus: true });
}

export function useMarkAchievementsSeen() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ["achievements", "seen"],
    mutationFn: achievementsApi.seen,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["achievements"] }),
  });
}

const LAYOUT = ["home-layout"] as const;

export function useHomeLayout() {
  return useQuery({ queryKey: LAYOUT, queryFn: homeLayoutApi.get, staleTime: Infinity });
}

/** Düzen anında değişir (iyimser); sunucu reddederse eskisine döner. Sürükle-bırak beklemesin. */
export function useSaveHomeLayout() {
  const qc = useQueryClient();
  return useMutation({
    mutationKey: ["achievements", "layout"], // başarım yeniden hesabını tetiklemesin
    mutationFn: (widgets: HomeWidget[]) => homeLayoutApi.put(widgets),
    onMutate: async (widgets) => {
      await qc.cancelQueries({ queryKey: LAYOUT });
      const prev = qc.getQueryData<HomeWidget[]>(LAYOUT);
      qc.setQueryData(LAYOUT, widgets);
      return { prev };
    },
    onError: (_e, _v, ctx) => ctx?.prev && qc.setQueryData(LAYOUT, ctx.prev),
    onSuccess: (saved) => qc.setQueryData(LAYOUT, saved),
  });
}
