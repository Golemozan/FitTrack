import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import axios from "axios";
import { useQueryClient } from "@tanstack/react-query";
import { KeyRound, ShieldAlert } from "lucide-react";
import api from "../api/axios";
import { clearApiKey, setApiKey, UNAUTHORIZED_EVENT } from "../api/auth";
import { Button, Input, Spinner } from "./ui";

type Status = "checking" | "locked" | "offline" | "unlocked";

const isUnauthorized = (err: unknown) => axios.isAxiosError(err) && err.response?.status === 401;

// Ucuz doğrulama isteği: /goals seed'li olduğu için 200 = anahtar geçerli.
const probe = () => api.get("/goals");

/**
 * Parola kapısı. Uygulama açılmadan önce saklanan anahtarı sunucuya doğrular.
 * Sunucuda anahtar tanımlı değilse (yerel geliştirme) doğrulama 200 döner ve kapı hiç görünmez.
 */
export default function AuthGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<Status>("checking");
  const [pass, setPass] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const check = useCallback(async () => {
    setStatus("checking");
    try {
      await probe();
      setStatus("unlocked");
    } catch (err) {
      setStatus(isUnauthorized(err) ? "locked" : "offline");
    }
  }, []);

  useEffect(() => {
    void check();
  }, [check]);

  // Anahtar herhangi bir istekte reddedilirse kapıya geri dön.
  useEffect(() => {
    const relock = () => {
      queryClient.clear(); // önbellekteki veri kilitten sonra ekranda kalmasın
      setStatus("locked");
      setPass("");
    };
    window.addEventListener(UNAUTHORIZED_EVENT, relock);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, relock);
  }, [queryClient]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const value = pass.trim();
    if (!value || busy) return;

    setBusy(true);
    setError("");
    setApiKey(value);
    try {
      await probe();
      setPass("");
      setStatus("unlocked");
    } catch (err) {
      clearApiKey();
      setError(isUnauthorized(err) ? "Parola yanlış." : "Sunucuya ulaşılamadı.");
    } finally {
      setBusy(false);
    }
  }

  if (status === "unlocked") return <>{children}</>;

  return (
    <div className="flex min-h-dvh items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-2xl border border-white/[0.08] bg-white/[0.04] p-6 shadow-lg shadow-black/20 backdrop-blur-xl">
        <div className="mb-5 flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-accent-grad shadow-glow">
            <span className="font-cond text-lg font-bold text-accentink">FT</span>
          </div>
          <div className="leading-none">
            <div className="font-cond text-2xl font-bold tracking-tight grad-text">FitTrack</div>
            <div className="mt-1 text-xs text-neutral-500">kişisel · parola gerekli</div>
          </div>
        </div>

        {status === "checking" && <Spinner />}

        {status === "offline" && (
          <div className="space-y-4">
            <div className="flex items-start gap-2.5 text-sm text-neutral-400">
              <ShieldAlert size={18} className="mt-0.5 shrink-0 text-accent" />
              <p>Sunucuya ulaşılamadı. Uykudaysa uyanması yarım dakika sürebilir.</p>
            </div>
            <Button onClick={() => void check()} className="w-full">
              Tekrar dene
            </Button>
          </div>
        )}

        {status === "locked" && (
          <form onSubmit={submit} className="space-y-4">
            <Input
              label="Parola"
              type="password"
              value={pass}
              onChange={(e) => setPass(e.target.value)}
              autoComplete="current-password"
              autoFocus
              disabled={busy}
            />
            {error && <p className="text-sm text-loss">{error}</p>}
            <Button type="submit" disabled={busy || !pass.trim()} className="flex w-full items-center justify-center gap-2">
              <KeyRound size={15} />
              {busy ? "Kontrol ediliyor…" : "Aç"}
            </Button>
          </form>
        )}
      </div>
    </div>
  );
}
