import { createContext, useEffect, type ReactNode } from "react";

// FitTrack is a permanently dark ("iron") app. Theme is fixed; the context
// stays so existing consumers (charts) can still read `theme` without changes.
export type Theme = "dark";

interface ThemeContextValue {
  theme: Theme;
  toggle: () => void;
  setTheme: (t: Theme) => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

export function ThemeProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add("dark");
  }, []);

  const value: ThemeContextValue = { theme: "dark", toggle: () => {}, setTheme: () => {} };
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
