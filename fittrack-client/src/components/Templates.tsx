import { useState } from "react";
import { BookmarkPlus, Download, Trash2 } from "lucide-react";
import { useApplyTemplate, useDeleteTemplate, useSaveTemplate, useTemplates } from "../hooks/useHabits";
import { useTodaySession } from "../hooks/useWorkout";
import { useToast } from "../hooks/useToast";
import { Skeleton } from "./ui";

const norm = (s: string) => s.trim().toLocaleLowerCase("tr");

function errorText(e: unknown, fallback: string) {
  return (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? fallback;
}

/**
 * Kayıtlı antrenmanlar: bir kez girilen gün isimle kaydedilir, başka gün "Yükle" ile bugüne gelir.
 * Yüklenince hareketler hazır, ağırlıklar o hareketin son yapıldığı seanstan dolu; kullanıcı yalnız
 * değişen kiloyu düzeltip "Güne kaydet" der. Aynı isimle tekrar kaydetmek kaydı günceller.
 */
export default function TemplatesBar() {
  const templates = useTemplates();
  const today = useTodaySession();
  const apply = useApplyTemplate();
  const remove = useDeleteTemplate();
  const save = useSaveTemplate();
  const toast = useToast();
  const [naming, setNaming] = useState(false);
  const [name, setName] = useState("");

  const session = today.data;
  const canSave = !!session && session.exercises.length > 0;
  const list = templates.data ?? [];
  const updating = list.some((t) => norm(t.name) === norm(name));

  // Bugün kayıtlı bir antrenman yüklendiyse adı önerilir: tekrar kaydetmek kopya değil güncelleme olur.
  const suggestName = () => {
    if (!session) return "";
    if (list.some((t) => norm(t.name) === norm(session.name))) return session.name;
    const today = new Set(session.exercises.map((e) => norm(e.name)));
    const best = list
      .map((t) => ({ t, hit: t.exercises.filter((e) => today.has(norm(e.name))).length }))
      .sort((a, b) => b.hit - a.hit)[0];
    if (best && best.hit > 0 && best.hit >= Math.ceil(best.t.exercises.length / 2)) return best.t.name;
    return session.name.startsWith("Antrenman -") ? "" : session.name;
  };

  const onApply = (id: string, tname: string) =>
    apply.mutate(id, {
      onSuccess: () => toast.success(`${tname} yüklendi — kiloları düzelt, sonra Güne kaydet`),
      onError: (e) => toast.error(errorText(e, "Antrenman yüklenemedi")),
    });

  const onSave = () => {
    if (!session) return;
    save.mutate(
      { name: name.trim(), sessionId: session.id },
      {
        onSuccess: (t) => {
          toast.success(updating ? `"${t.name}" güncellendi` : `"${t.name}" kaydedildi`);
          setNaming(false);
          setName("");
        },
        onError: (e) => toast.error(errorText(e, "Kaydedilemedi")),
      },
    );
  };

  return (
    <section className="rounded-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-card sm:p-6" aria-label="Kayıtlı antrenmanlar">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-[17px] font-bold">Kayıtlı antrenmanlar</h2>
          <p className="text-xs text-neutral-500">Bir kez gir, kaydet; sonra tek dokunuşla yükle</p>
        </div>
        {canSave && !naming && (
          <button
            type="button"
            onClick={() => {
              setName(suggestName());
              setNaming(true);
            }}
            className="flex min-h-10 shrink-0 items-center gap-1.5 rounded-full border border-hair px-3.5 text-sm font-semibold transition-colors hover:bg-card2"
          >
            <BookmarkPlus size={16} /> Kaydet
          </button>
        )}
      </div>

      {naming && (
        <form
          className="mt-4 flex flex-wrap gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (name.trim()) onSave();
          }}
        >
          <input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={40}
            placeholder="Antrenman adı (ör. Push günü)"
            aria-label="Antrenman adı"
            className="min-h-11 min-w-0 flex-1 rounded-full border border-hair bg-card2 px-4 text-[15px] outline-none focus:border-accentfg"
          />
          <button type="submit" disabled={!name.trim() || save.isPending} className="min-h-11 rounded-full bg-accent px-5 text-sm font-semibold text-accentink disabled:opacity-40">
            {updating ? "Güncelle" : "Kaydet"}
          </button>
          <button type="button" onClick={() => setNaming(false)} className="min-h-11 rounded-full px-3 text-sm font-semibold text-neutral-400 hover:bg-card2">
            Vazgeç
          </button>
          {updating && (
            <p className="w-full px-4 text-xs text-neutral-500">Bu isimde kayıt var — bugünkü hareketlerle güncellenecek.</p>
          )}
        </form>
      )}

      {templates.isPending ? (
        <div className="mt-4 flex gap-3"><Skeleton className="h-28 w-44" /><Skeleton className="h-28 w-44" /></div>
      ) : list.length === 0 ? (
        <p className="mt-4 rounded-[var(--radius-card)] border border-dashed border-hair p-4 text-sm text-neutral-400">
          Henüz kayıtlı antrenman yok. Hareketlerini bir kez gir, <span className="font-semibold text-neutral-200">Kaydet</span>'e basıp isim ver; bir dahaki sefere tek dokunuşla yüklensin.
        </p>
      ) : (
        <ul className="-mx-1 mt-4 flex snap-x gap-3 overflow-x-auto px-1 pb-1 no-scrollbar">
          {list.map((t) => {
            const sets = t.exercises.reduce((n, e) => n + e.setCount, 0);
            return (
              <li key={t.id} className="relative w-48 shrink-0 snap-start overflow-hidden rounded-[var(--radius-card)] bg-lift p-4 text-snow">
                <span aria-hidden="true" className="pointer-events-none absolute -right-6 -top-6 h-20 w-20 rounded-full bg-snow/15" />
                <p className="relative truncate pr-8 text-[15px] font-bold">{t.name}</p>
                <p className="relative mt-0.5 text-xs text-snow/75">{t.exercises.length} hareket · {sets} set</p>
                <p className="relative mt-2 line-clamp-2 min-h-8 text-[11px] leading-4 text-snow/70">{t.exercises.map((e) => e.name).join(" · ")}</p>
                <button
                  type="button"
                  onClick={() => onApply(t.id, t.name)}
                  disabled={apply.isPending}
                  className="relative mt-3 flex min-h-10 w-full items-center justify-center gap-1.5 rounded-full bg-snow text-sm font-semibold text-navy transition-[filter] hover:brightness-95 disabled:opacity-50"
                >
                  <Download size={15} /> Yükle
                </button>
                <button
                  type="button"
                  onClick={() => remove.mutate(t.id)}
                  aria-label={`${t.name} kaydını sil`}
                  className="absolute right-2 top-2 flex h-9 w-9 items-center justify-center rounded-full text-snow/70 hover:bg-snow/15 hover:text-snow"
                >
                  <Trash2 size={15} />
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
