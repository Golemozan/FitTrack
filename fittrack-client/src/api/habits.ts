import api from "./axios";
import type { Habit, HabitRequest, WeeklySummary, WorkoutSession, WorkoutTemplate } from "../types";

export const habitsApi = {
  list: (date?: string) => api.get<Habit[]>("/habits", { params: date ? { date } : {} }).then((r) => r.data),
  create: (body: HabitRequest) => api.post<Habit>("/habits", body).then((r) => r.data),
  update: (id: string, body: HabitRequest) => api.put<Habit>(`/habits/${id}`, body).then((r) => r.data),
  archive: (id: string) => api.delete(`/habits/${id}`).then((r) => r.data),
  log: (id: string, count: number, date?: string) =>
    api.put<Habit>(`/habits/${id}/log`, { count, date }).then((r) => r.data),
};

export const templatesApi = {
  list: () => api.get<WorkoutTemplate[]>("/workout/templates").then((r) => r.data),
  save: (name: string, sessionId: string) =>
    api.post<WorkoutTemplate>("/workout/templates", { name, sessionId }).then((r) => r.data),
  remove: (id: string) => api.delete(`/workout/templates/${id}`).then((r) => r.data),
  apply: (id: string) => api.post<WorkoutSession>(`/workout/templates/${id}/apply`).then((r) => r.data),
};

export const summaryApi = {
  weekly: () => api.get<WeeklySummary>("/summary/weekly").then((r) => r.data),
};

export const achievementsApi = {
  list: () => api.get<import("../types").Achievement[]>("/achievements").then((r) => r.data),
  seen: () => api.post("/achievements/seen").then((r) => r.data),
};

export const homeLayoutApi = {
  get: () => api.get<{ widgets: import("../types").HomeWidget[] }>("/home-layout").then((r) => r.data.widgets),
  put: (widgets: import("../types").HomeWidget[]) =>
    api.put<{ widgets: import("../types").HomeWidget[] }>("/home-layout", { widgets }).then((r) => r.data.widgets),
};
