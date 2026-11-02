import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AppShell from "./shell/AppShell";
import HomePage from "./pages/HomePage";
import NutritionPage from "./pages/NutritionPage";
import WorkoutPage from "./pages/WorkoutPage";
import ProfilePage from "./pages/ProfilePage";

/** Dört gerçek sayfa. Sunucu bilinmeyen yolları index.html'e düşürür (MapFallbackToFile), derin link çalışır. */
export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<HomePage />} />
          <Route path="beslenme" element={<NutritionPage />} />
          <Route path="antrenman" element={<WorkoutPage />} />
          <Route path="profil" element={<ProfilePage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
