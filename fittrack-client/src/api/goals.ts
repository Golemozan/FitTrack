import api from "./axios";
import type { UpdateGoalsRequest, UserGoals } from "../types";

export const goalsApi = {
  get: () => api.get<UserGoals>("/goals").then((r) => r.data),
  update: (body: UpdateGoalsRequest) =>
    api.put<UserGoals>("/goals", body).then((r) => r.data),
};
