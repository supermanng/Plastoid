import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The API runs on http://localhost:5080 in development (`make run` or the plastoids-api launch config).
// Everything under /api and /health is proxied so the browser sees one origin and no CORS is needed.
const apiTarget = process.env.VITE_API_PROXY ?? 'http://localhost:5080';

/*
 * Interface modules (ADR 0020): one package, three HTML entries, each with its own router mounted at its base path.
 * `VITE_MODULES=learner,sponsor` builds a subset (default: all three).
 */
const MODULES = {
  learner: { base: '/', html: 'index.html' },
  sponsor: { base: '/sponsor', html: 'sponsor/index.html' },
  admin: { base: '/admin', html: 'admin/index.html' },
};

function selectedModules() {
  const wanted = (process.env.VITE_MODULES || '').split(',').map((s) => s.trim()).filter(Boolean);
  if (!wanted.length) return Object.keys(MODULES);
  const unknown = wanted.filter((m) => !MODULES[m]);
  if (unknown.length) throw new Error(`VITE_MODULES: unknown module(s) ${unknown.join(', ')}; known: ${Object.keys(MODULES).join(', ')}`);
  return wanted;
}

/** Which module's HTML serves a navigation request: the longest matching base wins; `/sponsorship-x` is the learner module. */
function htmlFor(pathname) {
  let best = MODULES.learner.html;
  let bestLen = 0;
  for (const { base, html } of Object.values(MODULES)) {
    if (base === '/') continue;
    if ((pathname === base || pathname.startsWith(base + '/')) && base.length > bestLen) {
      best = html;
      bestLen = base.length;
    }
  }
  return '/' + best;
}

/** History fallback per module for the dev and preview servers (appType 'mpa' turns Vite's single-page fallback off). */
function moduleHistoryFallback() {
  const skip = ['/api/', '/health', '/@', '/src/', '/node_modules/'];
  const rewrite = (req, res, next) => {
    const method = req.method || 'GET';
    const pathname = (req.url || '/').split('?')[0];
    const accept = req.headers.accept || '';
    const lastSegment = pathname.slice(pathname.lastIndexOf('/') + 1);
    const looksLikeFile = lastSegment.includes('.');
    const isNavigation = (method === 'GET' || method === 'HEAD') && accept.includes('text/html') && !looksLikeFile;
    if (isNavigation && !skip.some((p) => pathname === p || pathname.startsWith(p))) req.url = htmlFor(pathname);
    next();
  };
  return {
    name: 'plastoids-module-history-fallback',
    configureServer(server) {
      server.middlewares.use(rewrite);
    },
    configurePreviewServer(server) {
      server.middlewares.use(rewrite);
    },
  };
}

export default defineConfig({
  appType: 'mpa',
  plugins: [react(), tailwindcss(), moduleHistoryFallback()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      '/health': { target: apiTarget, changeOrigin: true },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
    rollupOptions: {
      input: Object.fromEntries(selectedModules().map((m) => [m, fileURLToPath(new URL(MODULES[m].html, import.meta.url))])),
    },
  },
});
