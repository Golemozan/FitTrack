import ErrorBoundary from "../components/ErrorBoundary";
import NutritionSection from "../sections/NutritionSection";

/** Beslenme sayfası. İlk aşamada mevcut bölüm yeni kabukta; sayfa düzeni sonraki adımda elden geçecek. */
export default function NutritionPage() {
  return (
    <div className="rounded-[var(--radius-panel)] border border-hair bg-panel p-4 shadow-card sm:p-6">
      <ErrorBoundary label="Beslenme"><NutritionSection /></ErrorBoundary>
    </div>
  );
}
