import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './i18n/index.js';    // must be before App
import { App } from './app/App';
import './index.css';
import { logger } from './shared/utils/logger';

/* ── Global error capture ───────────────────────────────────────────────
   Catches any unhandled JS exception or rejected Promise and logs it
   so the DebugPanel shows what went wrong even without DevTools open. */
window.addEventListener('error', (e) => {
  logger.error(`Unhandled error: ${e.message}`, e.error, {
    source: e.filename,
    line: e.lineno,
    col: e.colno,
  });
});

window.addEventListener('unhandledrejection', (e) => {
  logger.error(`Unhandled rejection`, e.reason, {
    promise: String(e.promise),
  });
});

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>
);
