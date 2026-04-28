/**
 * UI Action Logger
 * ──────────────────────────────────────────────────────────────
 * Ghi lại mọi action của UI: API calls, navigation, user clicks,
 * errors. Lưu vào memory (circular buffer 300 entries) và
 * localStorage để persist qua reload.
 *
 * Usage:
 *   import { logger } from './logger';
 *   logger.api('GET', '/loans', { status: 200, ms: 120 });
 *   logger.nav('/loans', '/dashboard');
 *   logger.action('CLICK', 'approve-btn', { loanId: 5 });
 *   logger.error('Disburse failed', err);
 */

const MAX_ENTRIES    = 300;
const LS_KEY         = '__lf_action_log__';
const IS_DEV         = import.meta.env.DEV;

// ── Log Levels ───────────────────────────────────────────────
export const LOG_TYPES = {
  API_REQ  : 'API_REQ',   // HTTP request sent
  API_OK   : 'API_OK',    // HTTP success response
  API_ERR  : 'API_ERR',   // HTTP error / network failure
  NAV      : 'NAV',       // Route navigation
  ACTION   : 'ACTION',    // User UI interaction (click, submit…)
  AUTH     : 'AUTH',      // Login / logout / token refresh
  ERROR    : 'ERROR',     // Unhandled JS error
  INFO     : 'INFO',      // Generic informational
};

// ── Internal state ───────────────────────────────────────────
let _entries = [];
let _listeners = [];

/** Load from localStorage on boot */
function _hydrate() {
  try {
    const raw = localStorage.getItem(LS_KEY);
    if (raw) {
      _entries = JSON.parse(raw).slice(-MAX_ENTRIES);
    }
  } catch {
    _entries = [];
  }
}

/** Persist latest N entries to localStorage */
function _persist() {
  try {
    // Only keep last 100 in localStorage to avoid quota issues
    localStorage.setItem(LS_KEY, JSON.stringify(_entries.slice(-100)));
  } catch { /* storage full — ignore */ }
}

/** Notify all subscribed listeners */
function _notify() {
  _listeners.forEach(fn => fn([..._entries]));
}

/** Core append function */
function _append(type, label, detail = {}) {
  const entry = {
    id      : Date.now() + Math.random(),
    ts      : new Date().toISOString(),
    type,
    label,
    detail,
  };

  _entries.push(entry);

  // Trim to max
  if (_entries.length > MAX_ENTRIES) {
    _entries = _entries.slice(-MAX_ENTRIES);
  }

  _persist();
  _notify();

  // Console echo in dev mode
  if (IS_DEV) {
    const style = TYPE_STYLES[type] || 'color:#888';
    const ms    = detail.ms != null ? ` [${detail.ms}ms]` : '';
    console.log(
      `%c[LF ${type}]%c ${label}${ms}`,
      `${style}; font-weight:700`,
      'color:inherit',
      detail,
    );
  }

  return entry;
}

const TYPE_STYLES = {
  [LOG_TYPES.API_REQ] : 'color:#0b57d0',
  [LOG_TYPES.API_OK]  : 'color:#1e9b69',
  [LOG_TYPES.API_ERR] : 'color:#c34f4f',
  [LOG_TYPES.NAV]     : 'color:#5d7ce2',
  [LOG_TYPES.ACTION]  : 'color:#c98a1b',
  [LOG_TYPES.AUTH]    : 'color:#1d8f97',
  [LOG_TYPES.ERROR]   : 'color:#c34f4f; background:#fff0f0',
  [LOG_TYPES.INFO]    : 'color:#888',
};

// ── Public API ───────────────────────────────────────────────
export const logger = {
  /** Log outgoing API request */
  apiReq(method, path, body) {
    return _append(LOG_TYPES.API_REQ, `${method} ${path}`, { method, path, body });
  },

  /** Log successful API response */
  apiOk(method, path, status, ms, responsePreview) {
    return _append(LOG_TYPES.API_OK, `${method} ${path} → ${status}`, { method, path, status, ms, responsePreview });
  },

  /** Log failed API response */
  apiErr(method, path, status, ms, error) {
    return _append(LOG_TYPES.API_ERR, `${method} ${path} → ${status ?? 'ERR'}`, { method, path, status, ms, error: error?.message || String(error) });
  },

  /** Log route navigation */
  nav(from, to) {
    return _append(LOG_TYPES.NAV, `${from} → ${to}`, { from, to });
  },

  /** Log user UI interaction */
  action(verb, target, meta = {}) {
    return _append(LOG_TYPES.ACTION, `${verb}: ${target}`, { verb, target, ...meta });
  },

  /** Log auth events */
  auth(event, meta = {}) {
    return _append(LOG_TYPES.AUTH, event, meta);
  },

  /** Log an error */
  error(label, err, meta = {}) {
    return _append(LOG_TYPES.ERROR, label, {
      message: err?.message || String(err),
      stack  : err?.stack,
      ...meta,
    });
  },

  /** Generic info */
  info(label, detail = {}) {
    return _append(LOG_TYPES.INFO, label, detail);
  },

  // ── Read ──────────────────────────────────────────────────

  /** Get all entries (newest-first) */
  getEntries(filter = {}) {
    let result = [..._entries].reverse();
    if (filter.type)   result = result.filter(e => e.type === filter.type);
    if (filter.search) {
      const q = filter.search.toLowerCase();
      result = result.filter(e =>
        e.label.toLowerCase().includes(q) ||
        JSON.stringify(e.detail).toLowerCase().includes(q)
      );
    }
    return result;
  },

  /** Subscribe to new entries */
  subscribe(fn) {
    _listeners.push(fn);
    return () => { _listeners = _listeners.filter(l => l !== fn); };
  },

  /** Clear all logs */
  clear() {
    _entries = [];
    localStorage.removeItem(LS_KEY);
    _notify();
  },

  /** Export as JSON string */
  export() {
    return JSON.stringify(_entries, null, 2);
  },

  /** Download log as .json file */
  download() {
    const blob = new Blob([this.export()], { type: 'application/json' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `lf-log-${new Date().toISOString().slice(0,19).replace(/:/g,'-')}.json`;
    a.click();
    URL.revokeObjectURL(url);
  },
};

// Hydrate from localStorage immediately
_hydrate();
