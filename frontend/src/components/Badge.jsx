export function StatusBadge({ status }) {
  const mapping = {
    Draft: { label: 'Nhap', color: 'var(--text-muted)', bg: 'rgba(122,141,165,0.12)', glow: 'transparent' },
    Submitted: { label: 'Cho duyet', color: 'var(--amber)', bg: 'var(--amber-bg)', glow: 'rgba(201,138,27,0.14)' },
    Pending: { label: 'Cho duyet', color: 'var(--amber)', bg: 'var(--amber-bg)', glow: 'rgba(201,138,27,0.14)' },
    Approved: { label: 'Da duyet', color: 'var(--blue)', bg: 'var(--blue-bg)', glow: 'rgba(31,95,191,0.14)' },
    Rejected: { label: 'Tu choi', color: 'var(--red)', bg: 'var(--red-bg)', glow: 'rgba(195,79,79,0.14)' },

    PendingDisbursement: { label: 'Cho giai ngan', color: 'var(--teal)', bg: 'rgba(29,143,151,0.12)', glow: 'rgba(29,143,151,0.14)' },
    Active: { label: 'Dang vay', color: 'var(--green)', bg: 'var(--green-bg)', glow: 'rgba(30,155,105,0.14)' },
    Overdue: { label: 'Qua han', color: 'var(--red)', bg: 'var(--red-bg)', glow: 'rgba(195,79,79,0.16)' },
    Closed: { label: 'Da dong', color: 'var(--text-muted)', bg: 'rgba(122,141,165,0.12)', glow: 'transparent' },
    Paid: { label: 'Da tat toan', color: 'var(--teal)', bg: 'rgba(29,143,151,0.12)', glow: 'rgba(29,143,151,0.14)' },
    BadDebt: { label: 'No xau', color: 'var(--purple)', bg: 'rgba(93,124,226,0.12)', glow: 'rgba(93,124,226,0.14)' },

    Unpaid: { label: 'Chua tra', color: 'var(--amber)', bg: 'var(--amber-bg)', glow: 'rgba(201,138,27,0.14)' },
    Partial: { label: 'Tra mot phan', color: 'var(--gold)', bg: 'var(--gold-bg)', glow: 'rgba(217,164,65,0.14)' },
    Inactive: { label: 'Khong HD', color: 'var(--text-muted)', bg: 'rgba(122,141,165,0.1)', glow: 'transparent' },
  };

  const current = mapping[status] || {
    label: status,
    color: 'var(--text-secondary)',
    bg: 'rgba(255,255,255,0.84)',
    glow: 'transparent'
  };

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        whiteSpace: 'nowrap',
        padding: '0.32rem 0.7rem',
        borderRadius: '999px',
        backgroundColor: current.bg,
        color: current.color,
        border: `1px solid ${current.color}22`,
        boxShadow: `0 8px 20px ${current.glow}`,
        fontSize: '0.75rem',
        fontWeight: '800',
        backdropFilter: 'blur(8px)'
      }}
    >
      {current.label}
    </span>
  );
}

export function DpdBadge({ bucket }) {
  const mapping = {
    B0: { color: 'var(--green)', bg: 'var(--green-bg)', shadow: 'rgba(30,155,105,0.15)' },
    B1: { color: 'var(--amber)', bg: 'var(--amber-bg)', shadow: 'rgba(201,138,27,0.15)' },
    B2: { color: 'var(--gold)', bg: 'var(--gold-bg)', shadow: 'rgba(217,164,65,0.15)' },
    B3: { color: 'var(--red)', bg: 'var(--red-bg)', shadow: 'rgba(195,79,79,0.15)' },
    B4: { color: 'var(--purple)', bg: 'rgba(93,124,226,0.12)', shadow: 'rgba(93,124,226,0.15)' }
  };

  const current = mapping[bucket] || {
    color: 'var(--text-secondary)',
    bg: 'rgba(255,255,255,0.84)',
    shadow: 'transparent'
  };

  return (
    <span
      style={{
        backgroundColor: current.bg,
        color: current.color,
        border: `1px solid ${current.color}24`,
        boxShadow: `0 8px 18px ${current.shadow}`,
        padding: '0.25rem 0.65rem',
        borderRadius: '999px',
        fontSize: '0.75rem',
        fontWeight: '800',
        fontFamily: "'Courier New', monospace",
        backdropFilter: 'blur(8px)'
      }}
    >
      {bucket}
    </span>
  );
}
