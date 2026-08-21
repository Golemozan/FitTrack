import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import axios from "axios";
import { useQueryClient } from "@tanstack/react-query";
import { KeyRound, ShieldAlert } from "lucide-react";
import api from "../api/axios";
import { clearApiKey, setApiKey, UNAUTHORIZED_EVENT } from "../api/auth";
import { Button, Input, Spinner } from "./ui";

type Status = "checking" | "locked" | "offline" | "unlocked";
const isUnauthorized = (err: unknown) => axios.isAxiosError(err) && err.response?.status === 401;
const probe = () => api.get("/goals");

export default function AuthGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<Status>("checking");
  const [pass, setPass] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const check = useCallback(async () => {
    setStatus("checking");
    try { await probe(); setStatus("unlocked"); }
    catch (err) { setStatus(isUnauthorized(err) ? "locked" : "offline"); }
  }, []);

  useEffect(() => { void check(); }, [check]);
  useEffect(() => {
    const relock = () => { queryClient.clear(); setStatus("locked"); setPass(""); };
    window.addEventListener(UNAUTHORIZED_EVENT, relock);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, relock);
  }, [queryClient]);

  async function submit(e: FormEvent) {
    e.preventDefault(); const value = pass.trim(); if (!value || busy) return;
    setBusy(true); setError(""); setApiKey(value);
    try { await probe(); setPass(""); setStatus("unlocked"); }
    catch (err) { clearApiKey(); setError(isUnauthorized(err) ? "Parola yanlış." : "Sunucuya ulaşılamadı."); }
    finally { setBusy(false); }
  }

  if (status === "unlocked") return <>{children}</>;

  return <div className="flex min-h-dvh items-center justify-center bg-ink p-5">
    <div className="w-full max-w-sm rounded-[var(--radius-panel)] border border-hair bg-panel p-6 shadow-[var(--shadow-overlay)] sm:p-8">
      <div className="mb-8 flex flex-col items-center text-center">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-[var(--radius-card)] bg-accent text-2xl font-bold text-accentink">F</div>
        <h1 className="text-2xl font-bold text-neutral-100">FitTrack</h1>
        <p className="mt-1 text-sm text-neutral-500">Kişisel sağlık günlüğün</p>
      </div>
      {status === "checking" && <Spinner />}
      {status === "offline" && <div className="space-y-4"><div className="flex items-start gap-3 rounded-2xl bg-card2 p-4 text-sm leading-6 text-neutral-400"><ShieldAlert size={19} className="mt-0.5 shrink-0 text-gain" /><p>Sunucuya ulaşılamadı. Uykudaysa uyanması yarım dakika sürebilir.</p></div><Button onClick={() => void check()} className="w-full">Tekrar dene</Button></div>}
      {status === "locked" && <form onSubmit={submit} className="space-y-4"><div><h2 className="text-xl font-semibold text-neutral-100">Kilidi aç</h2><p className="mt-1 text-sm text-neutral-500">Verilerine erişmek için parolanı gir.</p></div><Input label="Parola" type="password" value={pass} onChange={(e) => setPass(e.target.value)} autoComplete="current-password" autoFocus disabled={busy} />{error && <p className="text-sm font-medium text-gain">{error}</p>}<Button type="submit" disabled={busy || !pass.trim()} className="flex w-full items-center justify-center gap-2"><KeyRound size={16} />{busy ? "Kontrol ediliyor…" : "Devam et"}</Button></form>}
    </div>
  </div>;
}
