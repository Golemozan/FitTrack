import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  Archive,
  KeyRound,
  LogOut,
  MessageCircle,
  ShieldCheck,
  Trash2,
  UserRound,
} from "lucide-react";
import { accountApi, apiError, type Session } from "../api/auth";
import { FieldError, FormError } from "../components/AuthScreen";
import { SectionHeader } from "../components/SectionHeader";
import { Button, Card, Chip, EmptyState, Input, ListSkeleton } from "../components/ui";
import { SESSION_KEY, useLogout, useResetSession, useSession } from "../hooks/useSession";
import { useToast } from "../hooks/useToast";

const fmt = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString("tr-TR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) : "—";

export default function AccountSection() {
  const session = useSession().data;
  if (!session) return null;
  return (
    <section>
      <SectionHeader icon={UserRound} title="Hesap" />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="space-y-4">
          <ProfileCard session={session} />
          <AiKeyCard />
          <TelegramCard />
          <LegacyCard />
        </div>
        <div className="space-y-4">
          <PasswordCard />
          <DangerCard />
        </div>
      </div>
    </section>
  );
}

function CardTitle({ icon: Icon, children }: { icon: typeof KeyRound; children: string }) {
  return (
    <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-neutral-300">
      <Icon size={16} className="text-accent" />
      {children}
    </h3>
  );
}

function ProfileCard({ session }: { session: Session }) {
  const logout = useLogout();
  return (
    <Card>
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <p className="truncate text-lg font-semibold text-neutral-100">{session.displayName}</p>
          <p className="truncate text-sm text-neutral-500">{session.email}</p>
        </div>
        <Button variant="soft" onClick={() => logout.mutate()} disabled={logout.isPending} className="flex min-h-11 items-center gap-2">
          <LogOut size={16} /> Çıkış
        </Button>
      </div>
    </Card>
  );
}

/* ---------------- AI anahtarı ---------------- */

const keySchema = z.object({
  key: z.string().trim().min(20, "Anahtar çok kısa.").max(256, "Anahtar çok uzun.").regex(/^\S+$/, "Anahtarda boşluk olamaz."),
});

function AiKeyCard() {
  const qc = useQueryClient();
  const toast = useToast();
  const status = useQuery({ queryKey: ["account", "ai-key"], queryFn: accountApi.aiKey });
  const [replacing, setReplacing] = useState(false);
  const [error, setError] = useState("");
  const { register, handleSubmit, reset, formState } = useForm<z.infer<typeof keySchema>>({ resolver: zodResolver(keySchema) });

  const syncSession = (aiEnabled: boolean) =>
    qc.setQueryData<Session | null>(SESSION_KEY, (s) => (s ? { ...s, aiEnabled } : s));

  const save = useMutation({
    mutationFn: (key: string) => accountApi.saveAiKey(key),
    onSuccess: (data) => {
      qc.setQueryData(["account", "ai-key"], data);
      syncSession(true);
      reset({ key: "" }); // anahtar formda bir saniye bile fazla durmasın
      setReplacing(false);
      toast.success("Anahtar doğrulandı ve şifreli kaydedildi");
    },
    onError: (err) => setError(apiError(err, "Anahtar kaydedilemedi.")),
  });

  const remove = useMutation({
    mutationFn: accountApi.deleteAiKey,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["account", "ai-key"] });
      syncSession(false);
      toast.success("Anahtar silindi");
    },
    onError: (err) => toast.error(apiError(err, "Anahtar silinemedi")),
  });

  const onSubmit = handleSubmit((v) => {
    setError("");
    save.mutate(v.key.trim());
  });

  return (
    <Card>
      <CardTitle icon={KeyRound}>AI anahtarı</CardTitle>

      {status.isPending && <ListSkeleton rows={2} />}
      {status.isError && (
        <EmptyState tone="error" title="Durum okunamadı">
          <button onClick={() => void status.refetch()} className="min-h-10 text-accent underline-offset-2 hover:underline">Tekrar dene</button>
        </EmptyState>
      )}

      {status.data && !status.data.storageAvailable && (
        <p className="rounded-xl bg-card2 p-3 text-sm leading-6 text-neutral-400">
          Anahtar saklama bu sunucuda yapılandırılmamış. AI özellikleri şu an kullanılamıyor.
        </p>
      )}

      {status.data?.storageAvailable && (
        <>
          {status.data.hasKey && !replacing ? (
            <div className="space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-card2 p-3">
                <div className="min-w-0">
                  <p className="font-mono text-sm text-neutral-100">{status.data.hint}</p>
                  <p className="mt-0.5 text-xs text-neutral-500">
                    Eklendi {fmt(status.data.createdAt)} · Son kullanım {fmt(status.data.lastUsedAt)}
                  </p>
                </div>
                <Chip tone="accent">Aktif</Chip>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="soft" onClick={() => setReplacing(true)} className="min-h-11">Değiştir</Button>
                <Button
                  variant="ghost"
                  disabled={remove.isPending}
                  onClick={() => { if (confirm("Anahtar silinsin mi? AI koç kapanır.")) remove.mutate(); }}
                  className="flex min-h-11 items-center gap-2 text-gain hover:bg-gain/10"
                >
                  <Trash2 size={15} /> Sil
                </Button>
              </div>
            </div>
          ) : (
            <form onSubmit={onSubmit} className="space-y-3" noValidate>
              <p className="text-sm leading-6 text-neutral-400">
                Koç, kendi Anthropic API anahtarınla çalışır; kullanım ücreti senin hesabına yansır.
                Anahtarı <span className="text-neutral-200">console.anthropic.com</span> → API Keys'ten alabilirsin.
              </p>
              <div>
                <Input
                  label="Anthropic API anahtarı"
                  type="password"
                  placeholder="sk-ant-…"
                  autoComplete="off"
                  spellCheck={false}
                  autoCapitalize="off"
                  {...register("key")}
                />
                <FieldError message={formState.errors.key?.message} />
              </div>
              <FormError message={error} />
              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={save.isPending} className="flex min-h-11 items-center gap-2">
                  <ShieldCheck size={16} /> {save.isPending ? "Doğrulanıyor…" : "Doğrula ve kaydet"}
                </Button>
                {replacing && (
                  <Button type="button" variant="ghost" onClick={() => { setReplacing(false); reset({ key: "" }); setError(""); }} className="min-h-11">
                    Vazgeç
                  </Button>
                )}
              </div>
              <ul className="space-y-1 text-xs leading-5 text-neutral-500">
                <li>· Kaydetmeden önce Anthropic'e sorulup doğrulanır.</li>
                <li>· Sunucuda AES-256-GCM ile şifreli durur; şifre çözme anahtarı veritabanında değildir.</li>
                <li>· Hiçbir ekranda ya da istekte geri gösterilmez — yalnız son 4 karakteri.</li>
              </ul>
            </form>
          )}
        </>
      )}
    </Card>
  );
}

/* ---------------- Telegram ---------------- */

function TelegramCard() {
  const qc = useQueryClient();
  const toast = useToast();
  const status = useQuery({ queryKey: ["account", "telegram"], queryFn: accountApi.telegram });
  const code = useMutation({ mutationFn: accountApi.telegramCode, onError: (e) => toast.error(apiError(e, "Kod alınamadı")) });
  const unlink = useMutation({
    mutationFn: accountApi.unlinkTelegram,
    onSuccess: () => { code.reset(); qc.invalidateQueries({ queryKey: ["account", "telegram"] }); toast.success("Telegram bağlantısı kaldırıldı"); },
    onError: (e) => toast.error(apiError(e, "Bağlantı kaldırılamadı")),
  });

  if (status.isPending) return <Card><CardTitle icon={MessageCircle}>Telegram</CardTitle><ListSkeleton rows={1} /></Card>;
  if (status.isError || !status.data?.botEnabled) return null; // bot kapalıysa bölüm hiç görünmez

  return (
    <Card>
      <CardTitle icon={MessageCircle}>Telegram</CardTitle>
      {status.data.linked ? (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-neutral-400">Bir Telegram sohbeti bu hesaba bağlı.</p>
          <Button variant="ghost" disabled={unlink.isPending} onClick={() => unlink.mutate()} className="min-h-11 text-gain hover:bg-gain/10">
            Bağlantıyı kaldır
          </Button>
        </div>
      ) : code.data ? (
        <div className="space-y-2 text-sm text-neutral-400">
          <p>Bota şunu yaz (10 dakika geçerli, tek kullanımlık):</p>
          <p className="select-all rounded-xl bg-card2 px-3 py-2.5 font-mono text-base tracking-wider text-neutral-100">/link {code.data.code}</p>
          <button onClick={() => void status.refetch()} className="min-h-10 text-accent hover:underline">Yazdım, kontrol et</button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-neutral-400">Koçla Telegram'dan da konuş.</p>
          <Button variant="soft" disabled={code.isPending} onClick={() => code.mutate()} className="min-h-11">Bağlama kodu al</Button>
        </div>
      )}
    </Card>
  );
}

/* ---------------- Eski veri ---------------- */

function LegacyCard() {
  const qc = useQueryClient();
  const toast = useToast();
  const legacy = useQuery({ queryKey: ["account", "legacy"], queryFn: accountApi.legacy });
  const [pass, setPass] = useState("");
  const claim = useMutation({
    mutationFn: () => accountApi.claimLegacy(pass),
    onSuccess: () => {
      setPass("");
      // Tüm veri artık bu hesaba ait: oturum dışındaki her şeyi yeniden çek.
      qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== SESSION_KEY[0] });
      toast.success("Eski veriler hesabına taşındı");
    },
    onError: (e) => toast.error(apiError(e, "Taşınamadı")),
  });

  if (!legacy.data?.available) return null;
  return (
    <Card>
      <CardTitle icon={Archive}>Eski verileri sahiplen</CardTitle>
      <form onSubmit={(e) => { e.preventDefault(); if (pass) claim.mutate(); }} className="space-y-3">
        <p className="text-sm leading-6 text-neutral-400">
          Tek kullanıcılı dönemden kalan kayıtlar var. Eski uygulama parolasını girersen hepsi bu hesaba taşınır.
        </p>
        <Input label="Eski parola" type="password" autoComplete="off" value={pass} onChange={(e) => setPass(e.target.value)} />
        <Button type="submit" disabled={!pass || claim.isPending} className="min-h-11">{claim.isPending ? "Taşınıyor…" : "Hesabıma taşı"}</Button>
      </form>
    </Card>
  );
}

/* ---------------- Parola ---------------- */

const passwordSchema = z
  .object({
    current: z.string().min(1, "Mevcut parolanı gir."),
    next: z.string().min(10, "En az 10 karakter.").max(128),
    confirm: z.string(),
  })
  .refine((v) => v.next === v.confirm, { path: ["confirm"], message: "Parolalar eşleşmiyor." });

function PasswordCard() {
  const toast = useToast();
  const [error, setError] = useState("");
  const { register, handleSubmit, reset, formState } = useForm<z.infer<typeof passwordSchema>>({ resolver: zodResolver(passwordSchema) });

  const onSubmit = handleSubmit(async (v) => {
    setError("");
    try {
      await accountApi.changePassword(v.current, v.next);
      reset();
      toast.success("Parola değişti. Diğer cihazlardaki oturumlar kapatıldı.");
    } catch (err) {
      setError(apiError(err, "Parola değiştirilemedi."));
    }
  });

  return (
    <Card>
      <CardTitle icon={ShieldCheck}>Parola</CardTitle>
      <form onSubmit={onSubmit} className="space-y-3" noValidate>
        <div>
          <Input label="Mevcut parola" type="password" autoComplete="current-password" {...register("current")} />
          <FieldError message={formState.errors.current?.message} />
        </div>
        <div>
          <Input label="Yeni parola" type="password" autoComplete="new-password" {...register("next")} />
          <FieldError message={formState.errors.next?.message} />
        </div>
        <div>
          <Input label="Yeni parola (tekrar)" type="password" autoComplete="new-password" {...register("confirm")} />
          <FieldError message={formState.errors.confirm?.message} />
        </div>
        <FormError message={error} />
        <Button type="submit" disabled={formState.isSubmitting} className="min-h-11">
          {formState.isSubmitting ? "Kaydediliyor…" : "Parolayı değiştir"}
        </Button>
      </form>
    </Card>
  );
}

/* ---------------- Tehlikeli bölge ---------------- */

function DangerCard() {
  const resetSession = useResetSession();
  const toast = useToast();
  const [confirming, setConfirming] = useState(false);
  const [pass, setPass] = useState("");
  const [error, setError] = useState("");

  const logoutAll = useMutation({
    mutationFn: accountApi.logoutAll,
    onSuccess: () => resetSession(null),
    onError: (e) => toast.error(apiError(e, "İşlem başarısız")),
  });

  const del = useMutation({
    mutationFn: () => accountApi.deleteAccount(pass),
    onSuccess: () => resetSession(null),
    onError: (e) => setError(apiError(e, "Hesap silinemedi.")),
  });

  return (
    <Card>
      <CardTitle icon={LogOut}>Oturumlar ve hesap</CardTitle>
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-neutral-400">Bu dahil tüm cihazlardan çık.</p>
          <Button variant="soft" disabled={logoutAll.isPending} onClick={() => logoutAll.mutate()} className="min-h-11">Her yerden çık</Button>
        </div>

        <div className="border-t border-hair/70 pt-4">
          {!confirming ? (
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-neutral-400">Hesabı ve tüm verini kalıcı olarak sil.</p>
              <Button variant="ghost" onClick={() => setConfirming(true)} className="min-h-11 text-gain hover:bg-gain/10">Hesabı sil</Button>
            </div>
          ) : (
            <form onSubmit={(e) => { e.preventDefault(); setError(""); if (pass) del.mutate(); }} className="space-y-3">
              <p className="text-sm leading-6 text-gain">
                Öğünler, antrenmanlar, kilo, koç notları ve AI anahtarın silinir. Geri alınamaz.
              </p>
              <Input label="Onaylamak için parolan" type="password" autoComplete="current-password" value={pass} onChange={(e) => setPass(e.target.value)} />
              <FormError message={error} />
              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={!pass || del.isPending} className="min-h-11 bg-gain text-white hover:brightness-110">
                  {del.isPending ? "Siliniyor…" : "Kalıcı olarak sil"}
                </Button>
                <Button type="button" variant="ghost" onClick={() => { setConfirming(false); setPass(""); setError(""); }} className="min-h-11">Vazgeç</Button>
              </div>
            </form>
          )}
        </div>
      </div>
    </Card>
  );
}
