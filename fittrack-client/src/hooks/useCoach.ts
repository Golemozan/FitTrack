import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { checkinApi, coachApi, profileApi } from "../api/coach";
import type { CoachMessage, LogCheckInRequest, UpdateProfileRequest } from "../types";

/** `signal` ile gönderilir — kullanıcı "durdur"a basınca istek iptal edilebilsin diye. */
export function useCoachChat() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ messages, signal }: { messages: CoachMessage[]; signal?: AbortSignal }) =>
      coachApi.chat(messages, signal),
    // Coach may have logged food/weight/check-ins via tools → refresh those tiles.
    onSuccess: (res) => {
      res.actions?.forEach((domain) => qc.invalidateQueries({ queryKey: [domain] }));
      qc.invalidateQueries({ queryKey: ["coach", "history"] });
    },
  });
}

export function useCoachHistory() {
  return useQuery({ queryKey: ["coach", "history"], queryFn: coachApi.history });
}

export function useTodayCheckins() {
  return useQuery({ queryKey: ["checkin", "today"], queryFn: checkinApi.today });
}

export function useLogCheckin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogCheckInRequest) => checkinApi.log(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["checkin"] }),
  });
}

export function useDeleteCheckin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => checkinApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["checkin"] }),
  });
}

export function useProfile() {
  return useQuery({ queryKey: ["profile"], queryFn: profileApi.get });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateProfileRequest) => profileApi.update(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["profile"] }),
  });
}
