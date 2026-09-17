import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { LogIn, UserPlus } from "lucide-react";
import { apiError, authApi } from "../api/auth";
import { useResetSession } from "../hooks/useSession";
import { AuthShell } from "./AuthShell";
import { Button, Input, Segmented } from "./ui";

type Mode = "login" | "register";

const email = z.string().trim().min(1, "E-posta gerekli.").email("Geçerli bir e-posta gir.").max(254);

const loginSchema = z.object({
  email,
  password: z.string().min(1, "Parola gerekli.").max(128),
});

const registerSchema = z
  .object({
    displayName: z.string().trim().min(1, "İsmini yaz.").max(40, "En fazla 40 karakter."),
    email,
    password: z.string().min(10, "En az 10 karakter.").max(128, "En fazla 128 karakter."),
    confirm: z.string(),
  })
  .refine((v) => v.password === v.confirm, { path: ["confirm"], message: "Parolalar eşleşmiyor." });

type LoginValues = z.infer<typeof loginSchema>;
type RegisterValues = z.infer<typeof registerSchema>;

export default function AuthScreen() {
  const [mode, setMode] = useState<Mode>("login");
  return (
    <AuthShell subtitle={mode === "login" ? "Hesabına giriş yap" : "Yeni hesap oluştur"}>
      <div className="flex justify-center">
        <Segmented<Mode>
          value={mode}
          onChange={setMode}
          options={[
            { value: "login", label: "Giriş" },
            { value: "register", label: "Kayıt ol" },
          ]}
        />
      </div>
      <div className="mt-5">{mode === "login" ? <LoginForm /> : <RegisterForm />}</div>
    </AuthShell>
  );
}

export function FieldError({ message }: { message?: string }) {
  return message ? <p className="mt-1 text-xs font-medium text-gain">{message}</p> : null;
}

export function FormError({ message }: { message: string }) {
  return message ? <p role="alert" className="rounded-xl bg-gain/10 px-3 py-2.5 text-sm font-medium text-gain">{message}</p> : null;
}

function LoginForm() {
  const reset = useResetSession();
  const [error, setError] = useState("");
  const { register, handleSubmit, formState } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  const onSubmit = handleSubmit(async (v) => {
    setError("");
    try { reset(await authApi.login(v.email, v.password)); }
    catch (err) { setError(apiError(err, "Giriş yapılamadı.")); }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-4" noValidate>
      <div>
        <Input label="E-posta" type="email" autoComplete="email" autoFocus {...register("email")} />
        <FieldError message={formState.errors.email?.message} />
      </div>
      <div>
        <Input label="Parola" type="password" autoComplete="current-password" {...register("password")} />
        <FieldError message={formState.errors.password?.message} />
      </div>
      <FormError message={error} />
      <Button type="submit" disabled={formState.isSubmitting} className="flex min-h-11 w-full items-center justify-center gap-2">
        <LogIn size={16} />{formState.isSubmitting ? "Kontrol ediliyor…" : "Giriş yap"}
      </Button>
    </form>
  );
}

function RegisterForm() {
  const reset = useResetSession();
  const [error, setError] = useState("");
  const { register, handleSubmit, formState } = useForm<RegisterValues>({ resolver: zodResolver(registerSchema) });

  const onSubmit = handleSubmit(async (v) => {
    setError("");
    try { reset(await authApi.register(v.email, v.password, v.displayName)); }
    catch (err) { setError(apiError(err, "Hesap oluşturulamadı.")); }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-4" noValidate>
      <div>
        <Input label="İsim" autoComplete="given-name" autoFocus {...register("displayName")} />
        <FieldError message={formState.errors.displayName?.message} />
      </div>
      <div>
        <Input label="E-posta" type="email" autoComplete="email" {...register("email")} />
        <FieldError message={formState.errors.email?.message} />
      </div>
      <div>
        <Input label="Parola" type="password" autoComplete="new-password" {...register("password")} />
        {formState.errors.password
          ? <FieldError message={formState.errors.password.message} />
          : <p className="mt-1 text-xs text-neutral-500">En az 10 karakter.</p>}
      </div>
      <div>
        <Input label="Parola (tekrar)" type="password" autoComplete="new-password" {...register("confirm")} />
        <FieldError message={formState.errors.confirm?.message} />
      </div>
      <FormError message={error} />
      <Button type="submit" disabled={formState.isSubmitting} className="flex min-h-11 w-full items-center justify-center gap-2">
        <UserPlus size={16} />{formState.isSubmitting ? "Oluşturuluyor…" : "Hesap oluştur"}
      </Button>
      <p className="text-center text-xs leading-5 text-neutral-500">
        AI koç için kayıttan sonra kendi Anthropic API anahtarını eklersin. Anahtar sunucuda şifreli saklanır, sana bile geri gösterilmez.
      </p>
    </form>
  );
}
