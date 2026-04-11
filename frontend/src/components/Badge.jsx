import { STATUS_LABELS, DPD_LABELS } from '../utils/format';

const STATUS_CLS = {
  Draft:'draft', Submitted:'submitted', Approved:'approved', Rejected:'rejected',
  PendingDisbursement:'pending', Active:'active', Overdue:'overdue', Closed:'closed',
  Unpaid:'pending', Partial:'partial', Paid:'approved', Inactive:'closed',
};
const DPD_CLS = { Current:'dpd-current', DPD1_7:'dpd-1-7', DPD8_30:'dpd-8-30', DPD31Plus:'dpd-31plus' };

export function StatusBadge({ status }) {
  const label = STATUS_LABELS[status] || status;
  const cls   = STATUS_CLS[status] || 'draft';
  return <span className={`badge badge-${cls}`}>{label}</span>;
}

export function DpdBadge({ bucket }) {
  const label = DPD_LABELS[bucket] || bucket;
  const cls   = DPD_CLS[bucket] || 'dpd-current';
  return <span className={`badge ${cls}`}>{label}</span>;
}

// Inject badge CSS once
const badgeCSS = `
.badge { display:inline-flex;align-items:center;padding:.22rem .65rem;border-radius:999px;font-size:.68rem;font-weight:700;letter-spacing:.02em;white-space:nowrap; }
.badge-draft     { background:var(--bg-elevated);color:var(--text-muted); }
.badge-submitted { background:var(--blue-bg);color:var(--blue);border:1px solid rgba(59,130,246,.3); }
.badge-pending   { background:var(--amber-bg);color:var(--amber);border:1px solid rgba(245,158,11,.3); }
.badge-approved  { background:var(--green-bg);color:var(--green);border:1px solid rgba(16,185,129,.3); }
.badge-rejected  { background:var(--red-bg);color:var(--red);border:1px solid rgba(239,68,68,.3); }
.badge-active    { background:var(--green-bg);color:var(--green);border:1px solid rgba(16,185,129,.3); }
.badge-overdue   { background:var(--red-bg);color:var(--red);border:1px solid rgba(239,68,68,.3); }
.badge-closed    { background:var(--bg-elevated);color:var(--text-muted); }
.badge-partial   { background:var(--amber-bg);color:var(--amber);border:1px solid rgba(245,158,11,.3); }
.dpd-current  { background:var(--green-bg);color:var(--green); }
.dpd-1-7      { background:var(--amber-bg);color:var(--amber); }
.dpd-8-30     { background:rgba(249,115,22,.12);color:var(--orange); }
.dpd-31plus   { background:var(--red-bg);color:var(--red); }
`;
if (!document.getElementById('badge-styles')) {
  const s = document.createElement('style');
  s.id = 'badge-styles';
  s.textContent = badgeCSS;
  document.head.appendChild(s);
}
