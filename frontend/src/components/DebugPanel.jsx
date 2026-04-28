/**
 * DebugPanel — Floating action-log viewer
 * ─────────────────────────────────────────────────────────────
 * Chỉ render trong DEV (import.meta.env.DEV).
 * Phím tắt: Ctrl+Shift+D để toggle mở/đóng.
 */
import { useState, useEffect, useRef, useCallback } from 'react';
import { logger, LOG_TYPES } from '../shared/utils/logger';

const IS_DEV = import.meta.env.DEV;

/* ── Màu theo loại event ─────────────────────────────────── */
const TYPE_META = {
  [LOG_TYPES.API_REQ] : { color: '#60a5fa', bg: 'rgba(96,165,250,0.12)',  label: 'REQ'  },
  [LOG_TYPES.API_OK]  : { color: '#34d399', bg: 'rgba(52,211,153,0.12)',  label: 'OK'   },
  [LOG_TYPES.API_ERR] : { color: '#f87171', bg: 'rgba(248,113,113,0.15)', label: 'ERR'  },
  [LOG_TYPES.NAV]     : { color: '#a78bfa', bg: 'rgba(167,139,250,0.12)', label: 'NAV'  },
  [LOG_TYPES.ACTION]  : { color: '#fbbf24', bg: 'rgba(251,191,36,0.12)',  label: 'ACT'  },
  [LOG_TYPES.AUTH]    : { color: '#22d3ee', bg: 'rgba(34,211,238,0.12)',  label: 'AUTH' },
  [LOG_TYPES.ERROR]   : { color: '#f87171', bg: 'rgba(248,113,113,0.2)',  label: 'FAIL' },
  [LOG_TYPES.INFO]    : { color: '#9ca3af', bg: 'rgba(156,163,175,0.1)',  label: 'INFO' },
};

const ALL_TYPES = Object.keys(LOG_TYPES);

function TypeBadge({ type }) {
  const m = TYPE_META[type] || { color: '#9ca3af', bg: 'transparent', label: type };
  return (
    <span style={{
      display: 'inline-block',
      padding: '1px 6px',
      borderRadius: 4,
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.05em',
      background: m.bg,
      color: m.color,
      border: `1px solid ${m.color}40`,
      flexShrink: 0,
    }}>{m.label}</span>
  );
}

function DetailView({ detail }) {
  const [open, setOpen] = useState(false);
  if (!detail || Object.keys(detail).length === 0) return null;
  return (
    <span>
      <button
        onClick={e => { e.stopPropagation(); setOpen(v => !v); }}
        style={{
          background: 'none', border: 'none', cursor: 'pointer',
          color: '#60a5fa', fontSize: 10, padding: '0 4px',
        }}
      >{open ? '▲' : '▶'} detail</button>
      {open && (
        <pre style={{
          margin: '4px 0 0',
          padding: '6px 8px',
          background: 'rgba(0,0,0,0.3)',
          borderRadius: 6,
          fontSize: 10,
          color: '#e2e8f0',
          overflowX: 'auto',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-all',
          maxHeight: 160,
          overflowY: 'auto',
        }}>
          {JSON.stringify(detail, null, 2)}
        </pre>
      )}
    </span>
  );
}

export default function DebugPanel() {
  if (!IS_DEV) return null;

  const [open,    setOpen]    = useState(false);
  const [entries, setEntries] = useState(() => logger.getEntries());
  const [filter,  setFilter]  = useState({ type: '', search: '' });
  const [pinned,  setPinned]  = useState(false); // keep panel always on top
  const scrollRef = useRef(null);
  const prevLen   = useRef(entries.length);

  /* Subscribe to live updates */
  useEffect(() => {
    return logger.subscribe(() => {
      setEntries(logger.getEntries(filter));
    });
  }, [filter]);

  /* Re-filter when filter changes */
  useEffect(() => {
    setEntries(logger.getEntries(filter));
  }, [filter]);

  /* Auto-scroll to top (newest entry) */
  useEffect(() => {
    if (entries.length !== prevLen.current && scrollRef.current) {
      scrollRef.current.scrollTop = 0;
    }
    prevLen.current = entries.length;
  }, [entries.length]);

  /* Keyboard shortcut Ctrl+Shift+D */
  useEffect(() => {
    const handler = (e) => {
      if (e.ctrlKey && e.shiftKey && e.key === 'D') {
        e.preventDefault();
        setOpen(v => !v);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  const copyAll = useCallback(() => {
    navigator.clipboard.writeText(logger.export()).catch(() => {});
  }, []);

  const errCount = entries.filter(e =>
    e.type === LOG_TYPES.API_ERR || e.type === LOG_TYPES.ERROR
  ).length;

  /* ── Toggle button (always visible) ─────────────────────── */
  return (
    <>
      {/* FAB trigger */}
      <button
        id="debug-panel-toggle"
        title="Debug Log (Ctrl+Shift+D)"
        onClick={() => setOpen(v => !v)}
        style={{
          position: 'fixed',
          bottom: 20,
          right: 20,
          zIndex: 9999,
          width: 44,
          height: 44,
          borderRadius: '50%',
          border: 'none',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 18,
          background: errCount > 0
            ? 'linear-gradient(135deg,#7f1d1d,#c34f4f)'
            : 'linear-gradient(135deg,#1e293b,#334155)',
          color: '#fff',
          boxShadow: '0 4px 14px rgba(0,0,0,0.4)',
          transition: 'transform 0.15s',
        }}
        onMouseEnter={e => e.currentTarget.style.transform = 'scale(1.1)'}
        onMouseLeave={e => e.currentTarget.style.transform = 'scale(1)'}
      >
        {errCount > 0
          ? <span style={{ fontSize: 12, fontWeight: 700 }}>⚠{errCount}</span>
          : '🪵'}
      </button>

      {/* Panel */}
      {open && (
        <div
          id="debug-panel"
          style={{
            position: 'fixed',
            bottom: 72,
            right: 20,
            width: 520,
            maxWidth: 'calc(100vw - 40px)',
            height: 500,
            zIndex: pinned ? 10000 : 9998,
            display: 'flex',
            flexDirection: 'column',
            background: 'rgba(10,14,22,0.97)',
            backdropFilter: 'blur(12px)',
            border: '1px solid rgba(255,255,255,0.08)',
            borderRadius: 14,
            boxShadow: '0 24px 60px rgba(0,0,0,0.7)',
            fontFamily: '"JetBrains Mono","Fira Code",monospace',
            overflow: 'hidden',
          }}
        >
          {/* ── Header ─────────────────────────────────────── */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            padding: '10px 14px',
            borderBottom: '1px solid rgba(255,255,255,0.07)',
            background: 'rgba(255,255,255,0.03)',
            flexShrink: 0,
          }}>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#e2e8f0', flex: 1 }}>
              🪵 Action Log
              <span style={{ color: '#64748b', fontWeight: 400, marginLeft: 8, fontSize: 11 }}>
                ({entries.length} entries)
              </span>
            </span>

            {/* Pin */}
            <button onClick={() => setPinned(v => !v)} title="Pin on top" style={headerBtn(pinned ? '#60a5fa' : '#64748b')}>
              📌
            </button>

            {/* Copy */}
            <button onClick={copyAll} title="Copy all as JSON" style={headerBtn('#64748b')}>
              📋
            </button>

            {/* Download */}
            <button onClick={() => logger.download()} title="Download JSON" style={headerBtn('#64748b')}>
              ⬇
            </button>

            {/* Clear */}
            <button
              onClick={() => { logger.clear(); setEntries([]); }}
              title="Clear log"
              style={headerBtn('#f87171')}
            >
              🗑
            </button>

            {/* Close */}
            <button onClick={() => setOpen(false)} style={headerBtn('#64748b')}>✕</button>
          </div>

          {/* ── Filters ────────────────────────────────────── */}
          <div style={{
            display: 'flex',
            gap: 6,
            padding: '8px 14px',
            borderBottom: '1px solid rgba(255,255,255,0.05)',
            flexShrink: 0,
            flexWrap: 'wrap',
          }}>
            <input
              placeholder="🔍 search label / detail…"
              value={filter.search}
              onChange={e => setFilter(f => ({ ...f, search: e.target.value }))}
              style={{
                flex: 1,
                minWidth: 140,
                background: 'rgba(255,255,255,0.05)',
                border: '1px solid rgba(255,255,255,0.08)',
                borderRadius: 6,
                color: '#e2e8f0',
                fontSize: 11,
                padding: '4px 8px',
                outline: 'none',
              }}
            />
            <select
              value={filter.type}
              onChange={e => setFilter(f => ({ ...f, type: e.target.value }))}
              style={{
                background: 'rgba(255,255,255,0.05)',
                border: '1px solid rgba(255,255,255,0.08)',
                borderRadius: 6,
                color: '#e2e8f0',
                fontSize: 11,
                padding: '4px 8px',
                outline: 'none',
              }}
            >
              <option value="">All types</option>
              {ALL_TYPES.map(t => (
                <option key={t} value={t}>{TYPE_META[t]?.label || t}</option>
              ))}
            </select>
          </div>

          {/* ── Log list ───────────────────────────────────── */}
          <div
            ref={scrollRef}
            style={{ flex: 1, overflowY: 'auto', padding: '6px 0' }}
          >
            {entries.length === 0 ? (
              <div style={{
                color: '#475569', fontSize: 12, textAlign: 'center',
                padding: '40px 20px',
              }}>
                Chưa có log nào. Hãy thực hiện thao tác trên UI.
              </div>
            ) : entries.map(e => (
              <LogRow key={e.id} entry={e} />
            ))}
          </div>
        </div>
      )}
    </>
  );
}

function LogRow({ entry }) {
  const m = TYPE_META[entry.type] || {};
  const time = entry.ts.slice(11, 23); // HH:MM:SS.mmm
  return (
    <div style={{
      padding: '5px 14px',
      borderBottom: '1px solid rgba(255,255,255,0.03)',
      display: 'flex',
      alignItems: 'flex-start',
      gap: 8,
      fontSize: 11,
    }}>
      <span style={{ color: '#475569', flexShrink: 0, paddingTop: 1 }}>{time}</span>
      <TypeBadge type={entry.type} />
      <span style={{ flex: 1, color: '#cbd5e1', wordBreak: 'break-all', lineHeight: 1.5 }}>
        {entry.label}
        <DetailView detail={entry.detail} />
      </span>
    </div>
  );
}

function headerBtn(color) {
  return {
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    color,
    fontSize: 14,
    padding: '2px 6px',
    borderRadius: 4,
    lineHeight: 1,
  };
}
