import api from "./axios";
import type { LogWeightRequest, WeightLog, WeightPoint, WeightStats } from "../types";

export const weightApi = {
  log: (body: LogWeightRequest) =>
    api.post<WeightLog>("/weight/log", body).then((r) => r.data),

  update: (id: string, body: LogWeightRequest) =>
    api.put<WeightLog>(`/weight/log/${id}`, body).then((r) => r.data),

  today: () => api.get<WeightLog | null>("/weight/today").then((r) => r.data),

  history: (days = 30) =>
    api.get<WeightPoint[]>("/weight/history", { params: { days } }).then((r) => r.data),

  logs: (take = 10) =>
    api.get<WeightLog[]>("/weight/logs", { params: { take } }).then((r) => r.data),

  stats: () => api.get<WeightStats>("/weight/stats").then((r) => r.data),

  deleteLog: (id: string) => api.delete(`/weight/log/${id}`).then((r) => r.data),
};
