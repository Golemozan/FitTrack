import { createContext, useCallback, useEffect, useState, type ReactNode } from "react";

// Varsayılan açık tema; koyu tema Profil → "Koyu tema" anahtarıyla açılır ve cihazda saklanır.
// İlk boyamada yanlış tema yanıp sönmesin diye index.html'deki satır içi betik aynı anahtarı
// React'ten önce okuyup data-theme'i koyar; burası yalnız sonraki değişiklikleri yönetir.
export type Theme = "light" | "dark";

export const THEME_KEY = "fittrack:theme";

interface ThemeContextValue {
  theme: Theme;
  toggle: () => void;
  setTheme: (t: Theme) => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

function readStored(): Theme {
  try {
    return localStorage.getItem(THEME_KEY) === "dark" ? "dark" : "light";
  } catch {
    return "light"; // gizli sekme / engellenmiş depolama: varsayılan
  }
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readStored);

  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = theme;
    root.classList.toggle("dark", theme === "dark");
    const bar = getComputedStyle(root).getPropertyValue("--color-paper").trim();
    if (bar) document.querySelector('meta[name="theme-color"]')?.setAttribute("content", bar);
    try {
      localStorage.setItem(THEME_KEY, theme);
    } catch {
      /* saklanamazsa bu oturumla sınırlı kalır */
    }
  }, [theme]);

  const setTheme = useCallback((t: Theme) => setThemeState(t), []);
  const toggle = useCallback(() => setThemeState((t) => (t === "dark" ? "light" : "dark")), []);

  return <ThemeContext.Provider value={{ theme, toggle, setTheme }}>{children}</ThemeContext.Provider>;
}
