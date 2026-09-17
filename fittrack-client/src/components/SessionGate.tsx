import { useEffect, type ReactNode } from "react";
import { ShieldAlert } from "lucide-react";
import { UNAUTHORIZED_EVENT } from "../api/auth";
import { useResetSession, useSession } from "../hooks/useSession";
import AuthScreen from "./AuthScreen";
import { AuthShell } from "./AuthShell";
import { Button, Spinner } from "./ui";

/** Oturum yoksa giriş/kayıt ekranı, varsa uygulama. Oturum düşünce önbellek temizlenir. */
export default function SessionGate({ children }: { children: ReactNode }) {
  const session = useSession();
  const reset = useResetSession();

  useEffect(() => {
    const onUnauthorized = () => reset(null);
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
  }, [reset]);

  if (session.isPending) {
    return <AuthShell><Spinner /></AuthShell>;
  }

  if (session.isError) {
    return (
      <AuthShell>
        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-2xl bg-card2 p-4 text-sm leading-6 text-neutral-400">
            <ShieldAlert size={19} className="mt-0.5 shrink-0 text-gain" />
            <p>Sunucuya ulaşılamadı. Uykudaysa uyanması yarım dakika sürebilir.</p>
          </div>
          <Button onClick={() => void session.refetch()} className="w-full">Tekrar dene</Button>
        </div>
      </AuthShell>
    );
  }

  if (!session.data) return <AuthScreen />;
  return <>{children}</>;
}
