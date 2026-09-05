import api from "./axios";
import type { CheckIn, CoachChatResponse, CoachMessage, LogCheckInRequest, Profile, UpdateProfileRequest } from "../types";

export const coachApi = {
  chat: (messages: CoachMessage[], signal?: AbortSignal) =>
    api
      .post<CoachChatResponse>("/coach/chat", { messages }, { timeout: 65000, signal })
      .then((r) => r.data),
  history: () => api.get<CoachMessage[]>("/coach/history").then((r) => r.data),
};

export const checkinApi = {
  log: (body: LogCheckInRequest) => api.post<CheckIn>("/checkin", body).then((r) => r.data),
  today: () => api.get<CheckIn[]>("/checkin/today").then((r) => r.data),
  recent: (take = 20) =>
    api.get<CheckIn[]>("/checkin/recent", { params: { take } }).then((r) => r.data),
  delete: (id: string) => api.delete(`/checkin/${id}`).then((r) => r.data),
};

export const profileApi = {
  get: () => api.get<Profile>("/profile").then((r) => r.data),
  update: (body: UpdateProfileRequest) => api.put<Profile>("/profile", body).then((r) => r.data),
};
