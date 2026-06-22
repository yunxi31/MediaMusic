/** @type {import('tailwindcss').Config} */
module.exports = {
  // Scan all Razor components, HTML entry points and JS interop files.
  // Frontend lives in a subfolder, so globs reach up one level with "../".
  content: [
    "../**/*.razor",
    "../**/*.html",
    "../wwwroot/js/**/*.js"
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Semantic theme tokens resolved at runtime from CSS variables,
        // enabling instant light/dark switching by toggling <html class="dark">.
        background: 'rgb(var(--background) / <alpha-value>)',
        surface: 'rgb(var(--surface) / <alpha-value>)',
        'on-surface': 'rgb(var(--on-surface) / <alpha-value>)',
        'on-surface-muted': 'rgb(var(--on-surface-muted) / <alpha-value>)',
        'surface-variant': 'rgb(var(--surface-variant) / <alpha-value>)',
        accent: 'rgb(var(--accent) / <alpha-value>)',
        'on-accent': 'rgb(var(--on-accent) / <alpha-value>)'
      },
      fontFamily: {
        sans: ['"Inter"', '"Microsoft YaHei"', '"Segoe UI"', 'system-ui', 'sans-serif']
      }
    }
  },
  plugins: []
};
