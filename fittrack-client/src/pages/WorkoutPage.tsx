import ErrorBoundary from "../components/ErrorBoundary";
import TemplatesBar from "../components/Templates";
import WorkoutSection from "../sections/WorkoutSection";

/** Antrenman sayfası: üstte şablonlar, altında bugünün seansı. Set tamamlanınca dinlenme sayacı başlar. */
export default function WorkoutPage() {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)] gap-4 lg:gap-5">
      <ErrorBoundary label="Şablonlar"><TemplatesBar /></ErrorBoundary>
      <div className="rounded-[var(--radius-panel)] border border-hair bg-panel p-4 shadow-card sm:p-6">
        <ErrorBoundary label="Antrenman"><WorkoutSection /></ErrorBoundary>
      </div>
    </div>
  );
}
