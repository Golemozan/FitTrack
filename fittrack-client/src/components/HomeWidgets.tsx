import { useMemo, useState, type ReactNode } from "react";
import {
  DndContext, KeyboardSensor, PointerSensor, TouchSensor, closestCenter, useSensor, useSensors, type DragEndEvent,
} from "@dnd-kit/core";
import { SortableContext, arrayMove, rectSortingStrategy, sortableKeyboardCoordinates, useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { CheckCheck, ChevronRight, ExternalLink, GripVertical, HeartPulse, LayoutGrid, Lightbulb, Minus, PencilLine, Plus, Sparkles, StickyNote } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import Overlay from "./Overlay";
import { Skeleton } from "./ui";
import { TIPS, TIP_TOPICS, type TipTopic } from "../data/tips";
import { useHomeLayout, useSaveHomeLayout } from "../hooks/useHabits";
import { useToast } from "../hooks/useToast";
import type { HomeWidget, HomeWidgetType } from "../types";

/* ────────────────────────────────────────────────────────────────
   Ana sayfanın özelleştirilebilir kısmı ("Kartlarım"). Seri ve bugünkü beslenme sabit; bunlar değil.
   Düzenle → kart köşesinde "−", tutup sürükleyerek sıralama (dnd-kit: fare, dokunmatik ve klavye),
   "Kart ekle" → galeri. Düzen sunucuda — telefon ve bilgisayar aynı ana sayfayı gösterir.
   ──────────────────────────────────────────────────────────────── */

export const WIDGET_META: Record<HomeWidgetType, { title: string; desc: string; icon: LucideIcon; tone: string; repeatable?: boolean }> = {
  mood: { title: "Bugün nasılsın?", desc: "Tek dokunuşla ruh hali, enerji, açlık", icon: HeartPulse, tone: "bg-lav text-navy" },
  habits: { title: "Alışkanlıklar", desc: "Su, takviye, adım… günlük tikler", icon: CheckCheck, tone: "bg-flow text-navy" },
  tiles: { title: "Alan kutuları", desc: "Beslenme, antrenman, kilo, durum kısayolları", icon: LayoutGrid, tone: "bg-lift text-snow" },
  tip: { title: "Biliyor muydun?", desc: "Kaynaklı kısa bilgi: mikroplastik, beslenme, uyku…", icon: Lightbulb, tone: "bg-sun text-navy" },
  note: { title: "Kendi notun", desc: "Kendine hatırlatma: \"şekerli içecek yok\"", icon: StickyNote, tone: "bg-sun text-navy", repeatable: true },
  coach: { title: "Koçuna sor", desc: "AI koça hızlı erişim", icon: Sparkles, tone: "bg-navy text-snow" },
};

const MAX_NOTES = 5;

export function CustomCards({ render }: { render: Partial<Record<HomeWidgetType, () => ReactNode>> }) {
  const layout = useHomeLayout();
  const save = useSaveHomeLayout();
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [gallery, setGallery] = useState(false);
  const widgets = layout.data ?? [];

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    // Dokunmatikte kısa basılı tutunca sürükleme başlar — kaydırma ile karışmasın.
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const commit = (next: HomeWidget[]) =>
    save.mutate(next, { onError: (e) => toast.error((e as { response?: { data?: { error?: string } } }).response?.data?.error ?? "Düzen kaydedilemedi") });

  const onDragEnd = (e: DragEndEvent) => {
    if (!e.over || e.active.id === e.over.id) return;
    const from = widgets.findIndex((w) => w.id === e.active.id);
    const to = widgets.findIndex((w) => w.id === e.over!.id);
    commit(arrayMove(widgets, from, to));
  };

  const add = (type: HomeWidgetType) => {
    const id = type === "note" ? `note-${Date.now().toString(36)}` : type;
    commit([...widgets, { id, type, text: type === "note" ? "" : undefined }]);
    setGallery(false);
  };

  const update = (id: string, patch: Partial<HomeWidget>) => commit(widgets.map((w) => (w.id === id ? { ...w, ...patch } : w)));

  return (
    <section aria-label="Kartlarım" className="min-w-0">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-display text-lg font-semibold">Kartlarım</h2>
        <div className="flex items-center gap-2">
          {editing && (
            <button type="button" onClick={() => setGallery(true)}
              className="flex min-h-10 items-center gap-1.5 rounded-full border border-hair bg-panel px-4 text-sm font-semibold shadow-card hover:bg-card2">
              <Plus size={16} /> Kart ekle
            </button>
          )}
          <button
            type="button"
            onClick={() => setEditing((v) => !v)}
            aria-pressed={editing}
            className={`min-h-10 rounded-full px-4 text-sm font-semibold transition-colors ${editing ? "bg-navy text-snow" : "text-neutral-400 hover:bg-card2 hover:text-neutral-100"}`}
          >
            {editing ? "Bitti" : "Düzenle"}
          </button>
        </div>
      </div>

      {layout.isPending ? (
        <div className="grid gap-4 lg:grid-cols-2"><Skeleton className="h-40" /><Skeleton className="h-40" /></div>
      ) : widgets.length === 0 ? (
        <button type="button" onClick={() => { setEditing(true); setGallery(true); }}
          className="flex w-full flex-col items-center gap-2 rounded-[var(--radius-panel)] border-2 border-dashed border-hair p-8 text-center hover:bg-card2">
          <Plus size={22} className="text-neutral-400" />
          <span className="text-[15px] font-semibold">Ana sayfana kart ekle</span>
          <span className="text-sm text-neutral-500">Bilgi kartları, notların, alışkanlıklar…</span>
        </button>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
          <SortableContext items={widgets.map((w) => w.id)} strategy={rectSortingStrategy}>
            <ul className="grid grid-cols-[minmax(0,1fr)] gap-4 lg:grid-cols-2 lg:gap-5">
              {widgets.map((w) => (
                <SortableCard key={w.id} widget={w} editing={editing} onRemove={() => commit(widgets.filter((x) => x.id !== w.id))}>
                  {w.type === "tip" ? (
                    <TipCard widget={w} editing={editing} onTopics={(topics) => update(w.id, { topics })} />
                  ) : w.type === "note" ? (
                    <NoteCard widget={w} onSave={(text) => update(w.id, { text })} />
                  ) : (
                    render[w.type]?.() ?? null
                  )}
                </SortableCard>
              ))}
            </ul>
          </SortableContext>
        </DndContext>
      )}

      <Overlay open={gallery} onClose={() => setGallery(false)}>
        <Gallery widgets={widgets} onAdd={add} />
      </Overlay>
    </section>
  );
}

function SortableCard({ widget, editing, onRemove, children }: { widget: HomeWidget; editing: boolean; onRemove: () => void; children: ReactNode }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: widget.id, disabled: !editing });
  const meta = WIDGET_META[widget.type];
  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`relative min-w-0 ${isDragging ? "z-20 opacity-90" : ""}`}
    >
      {editing ? (
        <div className={`rounded-[calc(var(--radius-panel)+6px)] border-2 border-dashed p-1.5 ${isDragging ? "border-neutral-100" : "border-hair"}`}>
          <div className="mb-1.5 flex items-center gap-2 px-1.5">
            <button
              type="button"
              {...attributes}
              {...listeners}
              aria-label={`${meta.title} kartını taşı`}
              className="flex h-10 flex-1 cursor-grab touch-none items-center gap-2 rounded-xl px-1 text-sm font-semibold text-neutral-300 active:cursor-grabbing"
            >
              <GripVertical size={18} className="text-neutral-500" /> {meta.title}
            </button>
            <button type="button" onClick={onRemove} aria-label={`${meta.title} kartını kaldır`}
              className="flex h-9 w-9 items-center justify-center rounded-full bg-danger text-snow shadow-card transition-transform active:scale-95">
              <Minus size={18} strokeWidth={3} />
            </button>
          </div>
          {/* Düzenlerken kartın içindeki düğmeler yanlışlıkla çalışmasın. Not ve bilgi kartı düzenlemede de etkileşimli. */}
          <div className={widget.type === "note" || widget.type === "tip" ? "" : "pointer-events-none select-none"}>{children}</div>
        </div>
      ) : (
        children
      )}
    </li>
  );
}

function Gallery({ widgets, onAdd }: { widgets: HomeWidget[]; onAdd: (t: HomeWidgetType) => void }) {
  const notes = widgets.filter((w) => w.type === "note").length;
  const types = Object.keys(WIDGET_META) as HomeWidgetType[];
  return (
    <div className="mx-auto max-w-xl">
      <h2 className="pr-12 font-display text-2xl font-semibold">Kart ekle</h2>
      <p className="mt-1 text-sm text-neutral-400">Seri ve bugünkü beslenme her zaman en üstte; bunlar altına eklenir.</p>
      <ul className="mt-5 space-y-2.5">
        {types.map((t) => {
          const meta = WIDGET_META[t];
          const added = !meta.repeatable && widgets.some((w) => w.type === t);
          const full = t === "note" && notes >= MAX_NOTES;
          return (
            <li key={t} className="flex items-center gap-3.5 rounded-[var(--radius-card)] border border-hair p-3.5">
              <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${meta.tone}`}><meta.icon size={20} /></span>
              <span className="min-w-0 flex-1">
                <span className="block text-[15px] font-semibold">{meta.title}</span>
                <span className="block text-xs text-neutral-500">{meta.desc}</span>
              </span>
              <button
                type="button"
                disabled={added || full}
                onClick={() => onAdd(t)}
                className="min-h-10 shrink-0 rounded-full bg-accent px-4 text-sm font-semibold text-accentink disabled:bg-card2 disabled:text-neutral-500"
              >
                {added ? "Ekli" : full ? "Dolu" : "Ekle"}
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/* ---------- Biliyor muydun? ---------- */

function dayIndex() {
  const now = new Date();
  return Math.floor((now.getTime() - new Date(now.getFullYear(), 0, 0).getTime()) / 86400_000);
}

/**
 * Konuya göre kimlik: her konunun kendi zemini, deseni ve yazı rengi var — kartlar birbirinin kopyası
 * gibi durmasın, konu ilk bakışta tanınsın. Desenler kodda çizilmiş SVG: indirilen görsel yok
 * (lisans takibi, sayfa ağırlığı ve küçük kartta okunurluk sorunu olmasın). Renkler temadan bağımsız
 * sabit — kart bir "afiş" gibi davranır, açık ve koyu temada aynı görünür.
 */
const TIP_STYLE: Record<TipTopic, {
  card: string; text: string; muted: string; chip: string; action: string; button: string; stat: string;
  Decor: () => ReactNode;
}> = {
  microplastic: {
    card: "bg-[oklch(24%_0.06_250)]",
    text: "text-snow", muted: "text-snow/70", chip: "bg-snow/10 text-[oklch(85%_0.12_215)]",
    action: "bg-snow/10 text-snow", button: "bg-[oklch(85%_0.12_215)] text-navy", stat: "text-[oklch(85%_0.12_215)]",
    Decor: ParticlesDecor,
  },
  nutrition: {
    card: "bg-[oklch(90%_0.19_124)]",
    text: "text-navy", muted: "text-navy/75", chip: "bg-navy/10 text-navy",
    action: "bg-snow/55 text-navy", button: "bg-navy text-snow", stat: "text-[oklch(38%_0.12_140)]",
    Decor: LeavesDecor,
  },
  sleep: {
    card: "bg-[oklch(32%_0.12_285)]",
    text: "text-snow", muted: "text-snow/70", chip: "bg-snow/10 text-[oklch(88%_0.09_85)]",
    action: "bg-snow/10 text-snow", button: "bg-[oklch(88%_0.09_85)] text-navy", stat: "text-[oklch(88%_0.09_85)]",
    Decor: NightDecor,
  },
  training: {
    card: "bg-[oklch(70%_0.17_38)]",
    text: "text-navy", muted: "text-navy/75", chip: "bg-navy/10 text-navy",
    action: "bg-snow/40 text-navy", button: "bg-navy text-snow", stat: "text-navy",
    Decor: EnergyDecor,
  },
};

function TipCard({ widget, editing, onTopics }: { widget: HomeWidget; editing: boolean; onTopics: (t: string[]) => void }) {
  const topics = (widget.topics?.length ? widget.topics : Object.keys(TIP_TOPICS)) as TipTopic[];
  const pool = useMemo(() => TIPS.filter((t) => topics.includes(t.topic)), [topics]);
  // Her gün farklı kart; "Sonraki" yalnız bu oturumda ilerletir.
  const [offset, setOffset] = useState(0);
  const tip = pool.length ? pool[(dayIndex() + offset) % pool.length] : null;
  const st = TIP_STYLE[tip?.topic ?? "nutrition"];

  const toggle = (t: TipTopic) => {
    const next = topics.includes(t) ? topics.filter((x) => x !== t) : [...topics, t];
    if (next.length > 0) onTopics(next.length === Object.keys(TIP_TOPICS).length ? [] : next);
  };

  return (
    <article key={tip?.id} className={`tip-in relative overflow-hidden rounded-[var(--radius-panel)] p-5 sm:p-6 ${st.card} ${st.text}`} aria-label="Biliyor muydun?">
      <st.Decor />
      <div className="relative flex items-center justify-between gap-3">
        <span className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.14em]">
          <Lightbulb size={15} /> Biliyor muydun?
        </span>
        {tip && <span className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${st.chip}`}>{TIP_TOPICS[tip.topic]}</span>}
      </div>

      {editing && (
        <div className="relative mt-3 flex flex-wrap gap-1.5" role="group" aria-label="Gösterilecek konular">
          {(Object.keys(TIP_TOPICS) as TipTopic[]).map((t) => (
            <button key={t} type="button" aria-pressed={topics.includes(t)} onClick={() => toggle(t)}
              className={`min-h-8 rounded-full px-3 text-xs font-semibold ${topics.includes(t) ? st.button : st.chip}`}>
              {TIP_TOPICS[t]}
            </button>
          ))}
        </div>
      )}

      {tip && (
        <>
          {tip.stat && (
            <p className="relative mt-4">
              <span className={`num block text-[40px] leading-none ${st.stat}`}>{tip.stat.value}</span>
              <span className={`mt-1 block text-xs font-semibold ${st.muted}`}>{tip.stat.label}</span>
            </p>
          )}
          <h3 className={`relative font-display text-lg font-semibold leading-snug ${tip.stat ? "mt-3" : "mt-4"}`}>{tip.title}</h3>
          <p className={`relative mt-2 text-sm leading-relaxed ${st.muted}`}>{tip.body}</p>
          <p className={`relative mt-3 rounded-2xl px-3.5 py-2.5 text-sm font-semibold ${st.action}`}>→ {tip.action}</p>
          <div className="relative mt-3 flex items-center justify-between gap-3">
            <a href={tip.source.url} target="_blank" rel="noopener noreferrer"
              className={`flex min-h-9 min-w-0 items-center gap-1.5 text-xs font-semibold underline-offset-2 hover:underline ${st.muted}`}>
              <ExternalLink size={13} className="shrink-0" /> <span className="truncate">{tip.source.label}</span>
            </a>
            <button type="button" onClick={() => setOffset((o) => o + 1)}
              className={`flex min-h-9 shrink-0 items-center gap-1 rounded-full px-3.5 text-xs font-semibold ${st.button}`}>
              Sonraki <ChevronRight size={14} />
            </button>
          </div>
        </>
      )}
    </article>
  );
}

/* ---------- Konu desenleri (dekor; bilgi taşımaz, ekran okuyucudan gizli) ---------- */

/** Mikroplastik: yüzen parçacık kümesi — büyük sayının sağındaki boşlukta; metnin üstüne düşmez. */
function ParticlesDecor() {
  const dots: Array<[number, number, number, number]> = [
    [112, 40, 9, 0.55], [84, 26, 3.5, 0.75], [140, 74, 5, 0.45], [74, 62, 2.5, 0.65], [150, 30, 2.5, 0.8],
    [104, 88, 2.5, 0.55], [130, 104, 5, 0.25], [118, 12, 2, 0.6], [92, 52, 1.6, 0.7],
  ];
  return (
    <svg aria-hidden="true" className="pointer-events-none absolute right-2 top-12 h-40 w-40" viewBox="0 0 160 160">
      <circle cx="112" cy="60" r="52" fill="oklch(85% 0.12 215)" opacity="0.08" />
      {dots.map(([x, y, r, o], i) => (
        <circle key={i} cx={x} cy={y} r={r} fill={i % 3 ? "oklch(85% 0.12 215)" : "white"} opacity={o} />
      ))}
      <rect x="146" y="100" width="9" height="4" rx="2" fill="white" opacity="0.3" transform="rotate(25 150 102)" />
    </svg>
  );
}

/** Beslenme: iki organik yaprak, sağ alttan taşan. */
function LeavesDecor() {
  return (
    <svg aria-hidden="true" className="pointer-events-none absolute -bottom-6 -right-6 h-48 w-48" viewBox="0 0 200 200">
      <path d="M180 20 C 80 30, 30 90, 40 190 C 140 180, 190 120, 180 20 Z" fill="oklch(38% 0.12 140)" opacity="0.14" />
      <path d="M180 20 C 120 70, 80 120, 40 190" stroke="oklch(38% 0.12 140)" strokeWidth="3" fill="none" opacity="0.2" />
      <path d="M200 110 C 150 110, 120 140, 120 200 L 200 200 Z" fill="white" opacity="0.28" />
    </svg>
  );
}

/** Uyku: yıldızlı gece ve hilal. */
function NightDecor() {
  const stars: Array<[number, number, number]> = [[40, 30, 1.6], [120, 18, 1.2], [210, 40, 1.8], [250, 12, 1.1], [175, 75, 1.3], [90, 60, 1], [230, 118, 1.4], [205, 92, 1]];
  return (
    <svg aria-hidden="true" className="pointer-events-none absolute inset-x-0 top-0 h-36 w-full" viewBox="0 0 360 140" preserveAspectRatio="xMaxYMin slice">
      {stars.map(([x, y, r], i) => <circle key={i} cx={x + 20} cy={y} r={r} fill="white" opacity={0.5 + (i % 3) * 0.15} />)}
      {/* Hilal konu etiketinin altında, büyük sayının sağında — etiketle çakışmaz. */}
      <circle cx="310" cy="96" r="26" fill="oklch(88% 0.09 85)" opacity="0.95" />
      <circle cx="323" cy="88" r="24" fill="oklch(32% 0.12 285)" />
      <circle cx="310" cy="96" r="46" fill="oklch(88% 0.09 85)" opacity="0.06" />
    </svg>
  );
}

/** Antrenman: köşeden geçen kalın enerji çizgileri ve şimşek. */
function EnergyDecor() {
  return (
    <svg aria-hidden="true" className="pointer-events-none absolute -right-4 top-0 h-full w-40" viewBox="0 0 160 260" preserveAspectRatio="xMaxYMin slice">
      {[0, 1, 2, 3].map((i) => (
        <path key={i} d={`M${170 - i * 28} -10 L${70 - i * 28} 270`} stroke="oklch(22% 0.032 282)" strokeWidth={i === 1 ? 14 : 6} opacity={i === 1 ? 0.1 : 0.07} strokeLinecap="round" />
      ))}
      <path d="M118 22 L98 58 L116 58 L100 96" stroke="white" strokeWidth="5" fill="none" strokeLinecap="round" strokeLinejoin="round" opacity="0.5" />
    </svg>
  );
}

/* ---------- Kendi notun ---------- */

function NoteCard({ widget, onSave }: { widget: HomeWidget; onSave: (text: string) => void }) {
  const [editing, setEditing] = useState(!widget.text);
  const [text, setText] = useState(widget.text ?? "");
  const save = () => {
    onSave(text.trim());
    setEditing(false);
  };
  return (
    <article className="relative rounded-[var(--radius-panel)] border border-hair bg-panel p-5 shadow-card sm:p-6" aria-label="Notum">
      <div className="flex items-center justify-between gap-3">
        <span className="flex items-center gap-2 text-sm font-bold"><StickyNote size={16} className="text-neutral-400" /> Notum</span>
        {!editing && (
          <button type="button" onClick={() => setEditing(true)} aria-label="Notu düzenle"
            className="flex h-9 w-9 items-center justify-center rounded-full text-neutral-400 hover:bg-card2 hover:text-neutral-100">
            <PencilLine size={16} />
          </button>
        )}
      </div>
      {editing ? (
        <div className="mt-3">
          <textarea
            autoFocus
            value={text}
            onChange={(e) => setText(e.target.value)}
            maxLength={280}
            rows={3}
            placeholder="Kendine bir not yaz: “Akşam 9'dan sonra atıştırma yok”"
            aria-label="Not"
            className="w-full resize-none rounded-[var(--radius-input)] border border-hair bg-card2 px-3.5 py-3 text-[15px] leading-relaxed outline-none focus:border-accentfg"
          />
          <div className="mt-2 flex items-center justify-between gap-3">
            <span className="tnum text-xs text-neutral-500">{text.length} / 280</span>
            <button type="button" onClick={save} className="min-h-10 rounded-full bg-accent px-5 text-sm font-semibold text-accentink">Kaydet</button>
          </div>
        </div>
      ) : (
        <p className="mt-3 whitespace-pre-line font-display text-[17px] font-medium leading-snug">{widget.text}</p>
      )}
    </article>
  );
}
