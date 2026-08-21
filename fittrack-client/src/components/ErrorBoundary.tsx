import { Component } from "react";
import type { ErrorInfo, ReactNode } from "react";
import { AlertTriangle, RotateCw } from "lucide-react";

type Props = {
  /** Hangi kartın çöktüğü — fallback başlığında geçer ("Antrenman açılamadı"). */
  label: string;
  /** Grid yerleşimi korunsun diye dışarıdan gelen span sınıfları. */
  className?: string;
  children: ReactNode;
};

type State = { error: Error | null };

/**
 * Tek bir kartın hatasını o kartın içinde tutar.
 *
 * Bu sınıf olmadan bir bileşenin fırlattığı hata React ağacının tamamını söküyor ve
 * geriye boş bir gövde — pratikte siyah ekran — kalıyordu (bkz. README GOTCHAS,
 * 2026-08-20 `Ok(null)` → 204 vakası). Kart bazında sınır, hatayı 200x150 piksellik
 * bir alana hapsediyor: diğer kartlar, sekmeler ve koç çalışmaya devam ediyor.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Tek kullanıcılı yerel uygulama — hata toplama servisi yok, konsol yeterli.
    console.error(`[${this.props.label}] kart hatası:`, error, info.componentStack);
  }

  private retry = () => this.setState({ error: null });

  render() {
    const { error } = this.state;
    if (!error) return this.props.children;

    return (
      <div
        role="alert"
        className={`min-w-0 rounded-[var(--radius-card)] border border-hair/70 bg-panel p-5 shadow-[var(--shadow-card)] ${this.props.className ?? ""}`}
      >
        <div className="mb-5 flex items-center gap-2 text-[15px] font-semibold text-neutral-300">
          <AlertTriangle size={18} className="text-gain" />
          {this.props.label} açılamadı
        </div>

        <p className="text-sm text-neutral-500">
          Bu kart yüklenirken bir şey ters gitti. Diğer bölümler çalışmaya devam ediyor.
        </p>

        {import.meta.env.DEV && (
          <p className="mt-2 break-words font-mono text-xs text-neutral-600">{error.message}</p>
        )}

        <button
          onClick={this.retry}
          className="mt-4 flex min-h-11 items-center gap-2 rounded-xl bg-card2 px-4 text-sm font-medium text-neutral-300 transition-colors hover:bg-panel hover:text-neutral-100"
        >
          <RotateCw size={16} />
          Tekrar dene
        </button>
      </div>
    );
  }
}
