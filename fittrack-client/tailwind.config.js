/** @type {import('tailwindcss').Config} */
export default {
  darkMode: "class",
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        // Space Grotesk (geometric display) + Inter (workhorse body).
        sans: ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
        cond: ['"Space Grotesk"', "Inter", "system-ui", "sans-serif"],
      },
      colors: {
        // Deep-indigo "night" surfaces. Cards are translucent glass over these.
        ink: "#0B0E1A", // app background
        panel: "#0F1322", // raised chrome
        card: "#141A2E", // solid card fallback
        card2: "#1B2236", // inset / hover surface
        hair: "rgba(255,255,255,0.08)", // hairline border

        // Twin accent — a cyan→violet gradient. `accent` is the solid cyan for
        // text/icons; `accent2` the violet gradient terminus.
        accent: "#22D3EE", // cyan
        accent2: "#A855F7", // violet
        accentink: "#06121A", // text/icon on a bright accent fill

        // Macro data hues.
        pro: "#FBBF24", // protein — amber
        carb: "#34D399", // carbs — emerald
        fat: "#818CF8", // fat — indigo

        // Weight-change semantics.
        loss: "#34D399", // down / cut
        gain: "#FB7185", // up / bulk
      },
      boxShadow: {
        glow: "0 10px 40px -12px rgba(34,211,238,0.45)",
        glowv: "0 10px 40px -12px rgba(168,85,247,0.45)",
      },
      backgroundImage: {
        "accent-grad": "linear-gradient(135deg,#22D3EE 0%,#A855F7 100%)",
      },
    },
  },
  plugins: [],
};
