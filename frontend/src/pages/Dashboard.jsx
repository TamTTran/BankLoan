import { useState, useEffect } from 'react';
import {
  AreaChart, Area,
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
  PieChart, Pie, Cell, ResponsiveContainer,
} from 'recharts';
import { dashboardApi } from '../features/dashboard/api/dashboardApi';
import { mapDashboardSummary } from '../features/dashboard/mappers/dashboardMapper';
import { formatCurrency, formatNumber, STATUS_LABELS } from '../utils/format';
import { DpdBadge } from '../components/Badge';
import { Card, CardHeader, CardBody } from '../components/ui/Card';

/* ── Design tokens ─────────────────────────────────────────────────────── */
const PALETTE = {
  blue:   '#0b57d0',
  green:  '#1e9b69',
  amber:  '#c98a1b',
  red:    '#c34f4f',
  purple: '#5d7ce2',
  teal:   '#1d8f97',
  gold:   '#d9a441',
};

const BUCKET_COLORS = {
  Current:   PALETTE.green,
  DPD1_7:   PALETTE.amber,
  DPD8_30:  PALETTE.gold,
  DPD31Plus: PALETTE.red,
};

const STATUS_COLORS = {
  Active:               PALETTE.blue,
  Overdue:              PALETTE.red,
  PendingDisbursement:  PALETTE.amber,
  Closed:               '#8a9bae',
};

const KPI_CARDS = [
  { key: 'totalOutstanding',      icon: 'VND', label: 'Total outstanding',    cls: 'kpi-blue',   fmt: formatCurrency },
  { key: 'totalActiveLoans',      icon: 'LN',  label: 'Active loans',         cls: 'kpi-green',  fmt: formatNumber },
  { key: 'pendingApprovals',      icon: 'AP',  label: 'Pending approvals',    cls: 'kpi-amber',  fmt: formatNumber },
  { key: 'carRatio',              icon: 'CAR', label: 'CAR',                  cls: 'kpi-purple', fmt: v => `${Number(v || 0).toFixed(2)}%` },
  { key: 'nplRatio',              icon: 'NPL', label: 'NPL ratio',            cls: 'kpi-red',    fmt: v => `${Number(v || 0).toFixed(2)}%` },
  { key: 'collectedThisMonth',    icon: 'COL', label: 'Collected this month', cls: 'kpi-teal',   fmt: formatCurrency },
];

/* ── Custom tooltip shared across charts ───────────────────────────────── */
function ChartTooltip({ active, payload, label, currencyKeys = [] }) {
  if (!active || !payload?.length) return null;
  return (
    <div style={{
      background: 'rgba(255,255,255,0.97)',
      border: '1px solid var(--border)',
      borderRadius: 12,
      padding: '0.7rem 1rem',
      boxShadow: 'var(--shadow)',
      fontSize: '0.82rem',
      lineHeight: 1.6,
    }}>
      <p style={{ fontWeight: 800, color: 'var(--navy)', marginBottom: '0.3rem' }}>{label}</p>
      {payload.map(p => (
        <p key={p.dataKey} style={{ color: p.color }}>
          <span style={{ fontWeight: 700 }}>{p.name}:</span>{' '}
          {currencyKeys.includes(p.dataKey)
            ? formatCurrency(p.value)
            : formatNumber(p.value)}
        </p>
      ))}
    </div>
  );
}

/* ── Pie legend ────────────────────────────────────────────────────────── */
function PieLegend({ data, colors }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.45rem', justifyContent: 'center' }}>
      {data.map((d, i) => (
        <div key={d.name} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8rem' }}>
          <span style={{ width: 10, height: 10, borderRadius: 3, background: colors[i % colors.length], flexShrink: 0 }} />
          <span style={{ color: 'var(--text-secondary)', flex: 1 }}>{d.name}</span>
          <span style={{ fontWeight: 800, color: 'var(--navy)' }}>{d.value}</span>
        </div>
      ))}
    </div>
  );
}

/* ── Main Component ────────────────────────────────────────────────────── */
export default function Dashboard({ onBadgeCounts }) {
  const [summary, setSummary] = useState(null);
  const [trend, setTrend] = useState([]);
  const [loading, setLoading] = useState(true);
  const [trendLoading, setTrendLoading] = useState(true);

  /* Fetch KPI + bucket data */
  useEffect(() => {
    Promise.all([
      dashboardApi.summary().then(mapDashboardSummary),
      dashboardApi.capitalAdequacy(),
      dashboardApi.approvalsPending().catch(() => ({ items: [], totalCount: 0 })),
    ])
    .then(([dashData, capData, approvalData]) => {
      const merged = { ...dashData, ...capData };
      setSummary(merged);
      const approvalsCount =
        approvalData?.totalCount ??
        (Array.isArray(approvalData?.items)
          ? approvalData.items.length
          : Array.isArray(approvalData?.Items)
            ? approvalData.Items.length
            : 0);
      onBadgeCounts?.({ applications: merged?.pendingApprovals || 0, approvals: approvalsCount });
    })
    .catch(console.error)
    .finally(() => setLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* Fetch monthly trend separately so charts load independently */
  useEffect(() => {
    dashboardApi.monthlyTrend(6)
      .then(res => setTrend(res?.points || res?.Points || []))
      .catch(() => setTrend([]))
      .finally(() => setTrendLoading(false));
  }, []);

  /* Derived values */
  const overdueBuckets   = summary?.overdueBuckets || [];
  const loanBreakdown    = summary?.loanStatusBreakdown || [];
  const coverageRate     = summary?.totalOutstanding
    ? Math.round(((summary?.collectedThisMonth || 0) / summary.totalOutstanding) * 100)
    : 0;

  const pieData = loanBreakdown.map(d => ({
    name: STATUS_LABELS[d.status] || d.status,
    value: d.count,
  }));
  const pieColors = loanBreakdown.map(d => STATUS_COLORS[d.status] || PALETTE.blue);

  const bucketBarData = overdueBuckets.map(b => ({
    name: b.bucket === 'DPD31Plus' ? '31+ DPD'
        : b.bucket === 'DPD8_30'  ? '8-30 DPD'
        : b.bucket === 'DPD1_7'   ? '1-7 DPD'
        : b.bucket,
    'Accounts':    b.count,
    'Amount (VND)': Number(b.totalAmount),
    _color: BUCKET_COLORS[b.bucket] || PALETTE.blue,
  }));

  const dateStr = new Date().toLocaleDateString('en-GB', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });

  return (
    <div className="page">

      {/* ── Hero ─────────────────────────────────────────────────── */}
      <section className="dashboard-hero">
        <div>
          <p className="dashboard-eyebrow">Portfolio command view</p>
          <h2>Daily lending snapshot</h2>
          <p className="dashboard-lead">
            Track approvals, disbursement movement, collection coverage and early overdue pressure from one screen.
          </p>
        </div>
        <div className="dashboard-hero-meta">
          <span className="dashboard-date-chip">{dateStr}</span>
          <div className="dashboard-hero-metric">
            <strong>{loading ? '—' : `${coverageRate}%`}</strong>
            <span>Monthly collection coverage</span>
          </div>
        </div>
      </section>

      {/* ── Brief cards ──────────────────────────────────────────── */}
      <section className="dashboard-brief-grid">
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Due today</span>
          <strong>{loading ? '—' : formatCurrency(summary?.dueTodayAmount || 0)}</strong>
          <p>Total installment principal + interest falling due today.</p>
        </div>
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Overdue accounts</span>
          <strong>{loading ? '—' : formatNumber(summary?.overdueAccounts || 0)}</strong>
          <p>Loans currently past their due date across all DPD buckets.</p>
        </div>
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Approvals queue</span>
          <strong>{loading ? '—' : formatNumber(summary?.pendingApprovals || 0)}</strong>
          <p>Use this count to prioritize officer and approver daily workload.</p>
        </div>
      </section>

      {/* ── KPI grid ─────────────────────────────────────────────── */}
      <div className="kpi-grid">
        {KPI_CARDS.map(card => (
          <div key={card.key} className={`kpi-card ${card.cls}`}>
            <div className="kpi-icon kpi-icon-text">{card.icon}</div>
            <div>
              <p className="kpi-label">{card.label}</p>
              <p className="kpi-value">{loading ? '—' : card.fmt(summary?.[card.key])}</p>
            </div>
          </div>
        ))}
      </div>

      {/* ── Row 1: Monthly trend (full width) ────────────────────── */}
      <Card hoverable style={{ marginBottom: '1rem' }}>
        <CardHeader
          title="Disbursement vs Collection — Last 6 months"
          subtitle="Monthly comparison of new loan disbursements against principal and interest collected."
        />
        <CardBody>
          {trendLoading ? (
            <div className="loading-shimmer" style={{ height: 220 }} />
          ) : trend.length === 0 ? (
            <p className="no-data">No trend data available.</p>
          ) : (
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={trend} margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                <defs>
                  <linearGradient id="gradDis" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor={PALETTE.blue}  stopOpacity={0.18} />
                    <stop offset="95%" stopColor={PALETTE.blue}  stopOpacity={0.01} />
                  </linearGradient>
                  <linearGradient id="gradCol" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor={PALETTE.green} stopOpacity={0.18} />
                    <stop offset="95%" stopColor={PALETTE.green} stopOpacity={0.01} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(16,35,63,0.06)" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} />
                <YAxis tickFormatter={v => v === 0 ? '0' : `${(v/1e9).toFixed(1)}B`} tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} width={52} />
                <Tooltip content={<ChartTooltip currencyKeys={['Disbursed','Collected']} />} />
                <Legend wrapperStyle={{ fontSize: 12, paddingTop: 8 }} />
                <Area type="monotone" dataKey="Disbursed"  name="Disbursed"  stroke={PALETTE.blue}  strokeWidth={2} fill="url(#gradDis)" dot={{ r: 3, fill: PALETTE.blue }} />
                <Area type="monotone" dataKey="Collected"  name="Collected"  stroke={PALETTE.green} strokeWidth={2} fill="url(#gradCol)" dot={{ r: 3, fill: PALETTE.green }} />
              </AreaChart>
            </ResponsiveContainer>
          )}
        </CardBody>
      </Card>

      {/* ── Row 2: Overdue bar + Status pie ──────────────────────── */}
      <div className="dashboard-grid">

        {/* Overdue pressure — horizontal bar */}
        <Card hoverable>
          <CardHeader
            title="Overdue pressure by DPD bucket"
            subtitle="Number of accounts and outstanding balance per delinquency bucket."
          />
          <CardBody>
            {loading ? (
              <div className="loading-shimmer" style={{ height: 200 }} />
            ) : overdueBuckets.length === 0 ? (
              <p className="no-data">No overdue data available.</p>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={bucketBarData} layout="vertical" margin={{ top: 0, right: 20, left: 10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="rgba(16,35,63,0.06)" />
                  <XAxis type="number" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} tickFormatter={v => formatNumber(v)} />
                  <YAxis type="category" dataKey="name" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} width={58} />
                  <Tooltip content={<ChartTooltip />} />
                  <Bar dataKey="Accounts" radius={[0, 6, 6, 0]}>
                    {bucketBarData.map((entry, idx) => (
                      <Cell key={idx} fill={entry._color} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardBody>
        </Card>

        {/* Loan status — donut */}
        <Card hoverable>
          <CardHeader
            title="Loan book distribution"
            subtitle="How the portfolio is currently split across lifecycle stages."
          />
          <CardBody>
            {loading ? (
              <div className="loading-shimmer" style={{ height: 200 }} />
            ) : pieData.length === 0 ? (
              <p className="no-data">No loan status data available.</p>
            ) : (
              <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem', height: 220 }}>
                <ResponsiveContainer width="55%" height="100%">
                  <PieChart>
                    <Pie
                      data={pieData}
                      cx="50%" cy="50%"
                      innerRadius={58} outerRadius={88}
                      paddingAngle={3}
                      dataKey="value"
                      stroke="none"
                    >
                      {pieData.map((_, i) => (
                        <Cell key={i} fill={pieColors[i]} />
                      ))}
                    </Pie>
                    <Tooltip content={<ChartTooltip />} />
                  </PieChart>
                </ResponsiveContainer>
                <div style={{ flex: 1 }}>
                  <PieLegend data={pieData} colors={pieColors} />
                </div>
              </div>
            )}
          </CardBody>
        </Card>
      </div>

      {/* ── Row 3: New loans per month bar ───────────────────────── */}
      <Card hoverable style={{ marginTop: '1rem' }}>
        <CardHeader
          title="New loan volume — Last 6 months"
          subtitle="Number of new loans disbursed each month. Highlights origination trend."
        />
        <CardBody>
          {trendLoading ? (
            <div className="loading-shimmer" style={{ height: 160 }} />
          ) : trend.length === 0 ? (
            <p className="no-data">No trend data available.</p>
          ) : (
            <ResponsiveContainer width="100%" height={180}>
              <BarChart data={trend} margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(16,35,63,0.06)" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} allowDecimals={false} width={32} />
                <Tooltip content={<ChartTooltip />} />
                <Bar dataKey="NewLoans" name="New Loans" fill={PALETTE.teal} radius={[6, 6, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </CardBody>
      </Card>

    </div>
  );
}
