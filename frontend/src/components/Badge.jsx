export function StatusBadge({ status }) {
  const mapping = {
    Draft: { label: 'Nhap', tone: 'neutral' },
    Submitted: { label: 'Cho duyet', tone: 'amber' },
    Pending: { label: 'Cho duyet', tone: 'amber' },
    Approved: { label: 'Da duyet', tone: 'blue' },
    Rejected: { label: 'Tu choi', tone: 'red' },
    PendingDisbursement: { label: 'Cho giai ngan', tone: 'teal' },
    Active: { label: 'Dang vay', tone: 'green' },
    Overdue: { label: 'Qua han', tone: 'red' },
    Closed: { label: 'Da dong', tone: 'neutral' },
    Paid: { label: 'Da tat toan', tone: 'teal' },
    BadDebt: { label: 'No xau', tone: 'purple' },
    Unpaid: { label: 'Chua tra', tone: 'amber' },
    Partial: { label: 'Tra mot phan', tone: 'gold' },
    Inactive: { label: 'Khong HD', tone: 'neutral' },
  };

  const current = mapping[status] || { label: status, tone: 'neutral' };

  return <span className={`status-badge status-badge-${current.tone}`}>{current.label}</span>;
}

export function DpdBadge({ bucket }) {
  const mapping = {
    B0: 'green',
    B1: 'amber',
    B2: 'gold',
    B3: 'red',
    B4: 'purple',
    Current: 'green',
    DPD1_7: 'amber',
    DPD8_30: 'gold',
    DPD31Plus: 'red',
  };

  const tone = mapping[bucket] || 'neutral';

  return <span className={`dpd-badge dpd-badge-${tone}`}>{bucket}</span>;
}
