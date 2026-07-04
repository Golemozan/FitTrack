import api from "./axios";
import type {
  AddExerciseRequest,
  AddSetRequest,
  CreateSessionRequest,
  Exercise,
  ExerciseHistoryItem,
  ExerciseSet,
  MuscleGroupExercises,
  SessionSummary,
  UpdateExerciseRequest,
  UpdateSetRequest,
  WorkoutSession,
} from "../types";

export const workoutApi = {
  createSession: (body: CreateSessionRequest) =>
    api.post<WorkoutSession>("/workout/session", body).then((r) => r.data),

  renameSession: (id: string, body: CreateSessionRequest) =>
    api.put<WorkoutSession>(`/workout/session/${id}`, body).then((r) => r.data),

  deleteSession: (id: string) =>
    api.delete(`/workout/session/${id}`).then((r) => r.data),

  today: () =>
    api.get<WorkoutSession | null>("/workout/session/today").then((r) => r.data),

  sessions: (limit = 10) =>
    api
      .get<SessionSummary[]>("/workout/sessions", { params: { limit } })
      .then((r) => r.data),

  addExercise: (body: AddExerciseRequest) =>
    api.post<Exercise>("/workout/exercise", body).then((r) => r.data),

  updateExercise: (id: string, body: UpdateExerciseRequest) =>
    api.put<Exercise>(`/workout/exercise/${id}`, body).then((r) => r.data),

  deleteExercise: (id: string) =>
    api.delete(`/workout/exercise/${id}`).then((r) => r.data),

  addSet: (body: AddSetRequest) =>
    api.post<ExerciseSet>("/workout/set", body).then((r) => r.data),

  updateSet: (id: string, body: UpdateSetRequest) =>
    api.put<ExerciseSet>(`/workout/set/${id}`, body).then((r) => r.data),

  deleteSet: (id: string) => api.delete(`/workout/set/${id}`).then((r) => r.data),

  history: (name: string) =>
    api
      .get<ExerciseHistoryItem[]>(`/workout/exercise/${encodeURIComponent(name)}/history`)
      .then((r) => r.data),

  catalog: () =>
    api.get<MuscleGroupExercises[]>("/workout/exercises/list").then((r) => r.data),
};
