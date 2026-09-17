import { useCallback } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi, type Session } from "../api/auth";

export const SESSION_KEY = ["session"] as const;

export function useSession() {
  return useQuery({
    queryKey: SESSION_KEY,
    queryFn: authApi.me,
    staleTime: Infinity,
    placeholderData: undefined, // önceki kullanıcının oturumu bir an bile gösterilmesin
  });
}

/**
 * Oturum değiştiğinde (giriş, çıkış, hesap silme) önbellekteki TÜM veri atılır.
 * Paylaşılan cihazda bir önceki kullanıcının öğünleri yeni girenin ekranında görünmesin.
 */
export function useResetSession() {
  const qc = useQueryClient();
  return useCallback(
    (next: Session | null) => {
      // `qc.clear()` DEĞİL: oturum sorgusunu da siler, `useSession` gözlemcisi silinmiş sorguya
      // bağlı kalır ve yeni değeri hiç görmez (giriş başarılı, ekran giriş formunda takılı kalır).
      qc.setQueryData(SESSION_KEY, next);
      qc.removeQueries({ predicate: (q) => q.queryKey[0] !== SESSION_KEY[0] });
      qc.getMutationCache().clear();
    },
    [qc],
  );
}

export function useLogout() {
  const reset = useResetSession();
  return useMutation({
    mutationFn: authApi.logout,
    onSettled: () => reset(null),
  });
}
