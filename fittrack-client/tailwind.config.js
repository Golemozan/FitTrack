/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        sans: ["var(--font-body)"],
        cond: ["var(--font-display)"],
        mono: ["var(--font-outlier)"],
      },
      borderRadius: {
        lg: "var(--radius-input)",
        xl: "1rem",
        "2xl": "var(--radius-card)",
        "3xl": "var(--radius-panel)",
      },
      colors: {
        ink: "oklch(var(--paper-channels) / <alpha-value>)",
        panel: "oklch(var(--paper-2-channels) / <alpha-value>)",
        card: "oklch(var(--paper-2-channels) / <alpha-value>)",
        card2: "oklch(var(--paper-3-channels) / <alpha-value>)",
        hair: "oklch(var(--rule-channels) / <alpha-value>)",
        white: "oklch(var(--ink-channels) / <alpha-value>)",
        black: "oklch(var(--paper-channels) / <alpha-value>)",
        accent: "oklch(var(--accent-channels) / <alpha-value>)",
        accent2: "oklch(var(--accent-channels) / <alpha-value>)",
        accentink: "oklch(var(--accent-ink-channels) / <alpha-value>)",
        pro: "oklch(var(--protein-channels) / <alpha-value>)",
        carb: "oklch(var(--carb-channels) / <alpha-value>)",
        fat: "oklch(var(--fat-channels) / <alpha-value>)",
        move: "oklch(var(--move-channels) / <alpha-value>)",
        lift: "oklch(var(--lift-channels) / <alpha-value>)",
        flow: "oklch(var(--flow-channels) / <alpha-value>)",
        lav: "oklch(var(--lav-channels) / <alpha-value>)",
        track: "oklch(23% 0.010 264 / <alpha-value>)",
        loss: "oklch(var(--success-channels) / <alpha-value>)",
        gain: "oklch(var(--danger-channels) / <alpha-value>)",
        neutral: {
          100: "oklch(var(--ink-channels) / <alpha-value>)",
          200: "oklch(var(--ink-channels) / <alpha-value>)",
          300: "oklch(var(--ink-2-channels) / <alpha-value>)",
          400: "oklch(var(--neutral-channels) / <alpha-value>)",
          500: "oklch(var(--muted-channels) / <alpha-value>)",
          600: "oklch(var(--muted-channels) / <alpha-value>)",
        },
      },
      boxShadow: {
        glow: "var(--shadow-card)",
        glowv: "var(--shadow-card)",
      },
    },
  },
  plugins: [],
};
