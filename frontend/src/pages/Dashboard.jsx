import { useState, useEffect } from 'react';
import {
  AreaChart, Area,
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
  PieChart, Pie, Cell, ResponsiveContainer,
} from 'recharts';
import { dashboardApi } from '../features/dashboard/api/dashboardApi';
import { mapDashboardSummary } from '../features/dashboard/mappers/dashboardMapper';
import { formatCurrency, formatNumber, STATUS_LABELS } from '../utils/format';
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
  DPD1_7:    PALETTE.amber,
  DPD8_30:   PALETTE.gold,
  DPD31Plus: PALETTE.red,
};

const STATUS_COLORS = {
  Active:               PALETTE.blue,
  Overdue:              PALETTE.red,
  PendingDisbursement:  PALETTE.amber,
  Closed:               '#8a9bae',
};

const APPLICATION_STATUS_ORDER = ['Draft', 'Submitted', 'Approved', 'Rejected'];

const APPLICATION_STATUS_COLORS = {
  Draft:     '#8a9bae',
  Submitted: PALETTE.amber,
  Approved:  PALETTE.green,
  Rejected:  PALETTE.red,
};

const KPI_CARDS = [
  { key: 'totalOutstanding',   icon: 'VND', label: 'Total outstanding',    cls: 'kpi-blue',   fmt: formatCurrency },
  { key: 'totalActiveLoans',   icon: 'LN',  label: 'Active loans',         cls: 'kpi-green',  fmt: formatNumber },
  { key: 'totalApplicationsToday', icon: 'NEW', label: 'New applications today', cls: 'kpi-purple', fmt: formatNumber },
  { key: 'pendingApprovals',   icon: 'AP',  label: 'Pending approvals',    cls: 'kpi-amber',  fmt: formatNumber },
  { key: 'totalDisbursedThisMonth', icon: 'DIS', label: 'Disbursed this month', cls: 'kpi-blue', fmt: formatCurrency },
  { key: 'disbursedLoansThisMonth', icon: 'VOL', label: 'Disbursed loans this month', cls: 'kpi-green', fmt: formatNumber },
  { key: 'carRatio',           icon: 'CAR', label: 'CAR',                  cls: 'kpi-purple', fmt: v => `${Number(v || 0).toFixed(2)}%` },
  { key: 'nplRatio',           icon: 'NPL', label: 'NPL ratio',            cls: 'kpi-red',    fmt: v => `${Number(v || 0).toFixed(2)}%` },
  { key: 'collectedThisMonth', icon: 'COL', label: 'Collected this month', cls: 'kpi-teal',   fmt: formatCurrency },
];

/* ── Shared tooltip ────────────────────────────────────────────────────── */
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
          {currencyKeys.includes(p.dataKey) ? formatCurrency(p.value) : formatNumber(p.value)}
        </p>
      ))}
    </div>
  );
}

/* ── Donut legend ──────────────────────────────────────────────────────── */
function PieLegend({ data, colors }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', justifyContent: 'center' }}>
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

/* ── Loading shimmer placeholder ───────────────────────────────────────── */
function ChartSkeleton({ height = 220 }) {
  return <div className="loading-shimmer" style={{ height, borderRadius: 12 }} />;
}

/* ══════════════════════════════════════════════════════════════════════════
   Main component
   ══════════════════════════════════════════════════════════════════════════ */
export default function Dashboard({ onBadgeCounts }) {
  const [summary,        setSummary]        = useState(null);
  const [overdueBuckets, setOverdueBuckets] = useState([]);
  const [trend,          setTrend]          = useState([]);
  const [loading,        setLoading]        = useState(true);
  const [bucketsLoading, setBucketsLoading] = useState(true);
  const [trendLoading,   setTrendLoading]   = useState(true);

  /* Fetch 1: KPI summary. Keep old dashboard charts independent. */
  useEffect(() => {
    let cancelled = false;

    dashboardApi.summary()
      .then(mapDashboardSummary)
      .then(dashData => {
        if (cancelled) return;
        setSummary(prev => ({ ...(prev || {}), ...dashData }));
        onBadgeCounts?.(prev => ({ ...(prev || {}), applications: dashData?.pendingApprovals || 0 }));
      })
      .catch(console.error)
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [onBadgeCounts]);

  useEffect(() => {
    let cancelled = false;

    dashboardApi.capitalAdequacy()
      .then(capData => {
        if (!cancelled) setSummary(prev => ({ ...(prev || {}), ...capData }));
      })
      .catch(console.error);

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;

    dashboardApi.approvalsPending()
      .catch(() => ({ items: [], totalCount: 0 }))
      .then(approvalData => {
        if (cancelled) return;
        const approvalsCount =
          approvalData?.totalCount ??
          (Array.isArray(approvalData?.items) ? approvalData.items.length
            : Array.isArray(approvalData?.Items) ? approvalData.Items.length
            : 0);
        onBadgeCounts?.(prev => ({ ...(prev || {}), approvals: approvalsCount }));
      });

    return () => { cancelled = true; };
  }, [onBadgeCounts]);

  /* ── Fetch 2: Overdue bucket breakdown ─────────────────────────────── */
  useEffect(() => {
    dashboardApi.overdueBuckets()
      .then(res => {
        // Backend returns { buckets: [...] } or { Buckets: [...] }
        const arr = res?.buckets || res?.Buckets || [];
        setOverdueBuckets(arr);
      })
      .catch(() => setOverdueBuckets([]))
      .finally(() => setBucketsLoading(false));
  }, []);

  /* Fetch 3: monthly trend. Fallback stays as placeholder if API has no data. */
  useEffect(() => {
    let cancelled = false;

    dashboardApi.monthlyTrend(6)
      .then(res => {
        if (cancelled) return;
        const points = res?.points || res?.Points || [];
        setTrend(points.map(p => ({
          label: p.label || p.Label || p.month || p.Month,
          disbursed: Number(p.disbursed ?? p.Disbursed ?? 0),
          collected: Number(p.collected ?? p.Collected ?? 0),
          newLoans: Number(p.newLoans ?? p.NewLoans ?? 0),
        })));
      })
      .catch(() => {
        if (!cancelled) setTrend([]);
      })
      .finally(() => {
        if (!cancelled) setTrendLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  /* ── Derived ────────────────────────────────────────────────────────── */
  // loanStatusBreakdown from backend is camelCase
  const loanBreakdown = summary?.loanStatusBreakdown || [];
  const applicationBreakdown = summary?.applicationStatusBreakdown || [];
  const coverageRate  = summary?.totalOutstanding
    ? Math.round(((summary?.collectedThisMonth || 0) / summary.totalOutstanding) * 100)
    : 0;

  // Pie chart — loan status donut
  const pieData   = loanBreakdown.map(d => ({ name: STATUS_LABELS[d.status] || d.status, value: d.count }));
  const pieColors = loanBreakdown.map(d => STATUS_COLORS[d.status] || PALETTE.blue);

  const pipelineData = APPLICATION_STATUS_ORDER
    .map(status => {
      const item = applicationBreakdown.find(d => d.status === status);
      return {
        status,
        name: STATUS_LABELS[status] || status,
        count: Number(item?.count || 0),
        amount: Number(item?.totalRequestedAmount || 0),
        _color: APPLICATION_STATUS_COLORS[status] || PALETTE.blue,
      };
    })
    .filter(d => d.count > 0 || applicationBreakdown.length > 0);

  // Horizontal bar — overdue buckets
  // Backend fields (camelCase): bucket, label, count, totalAmount
  const bucketBarData = overdueBuckets.map(b => ({
    name:      b.label || b.bucket,
    accounts:  b.count,
    amount:    Number(b.totalAmount),
    _color:    BUCKET_COLORS[b.bucket] || PALETTE.blue,
  }));

  // Trend dataKeys are camelCase from backend: disbursed, collected, newLoans, label
  const hasTrend = trend.length > 0;

  const dateStr = new Date().toLocaleDateString('en-GB', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });

  return (
    <div className="page">

      {/* ── Hero ─────────────────────────────────────────────────────── */}
      <section className="dashboard-hero">
        <div>
          <p className="dashboard-eyebrow">Portfolio command view</p>
          <h2>Daily lending snapshot</h2>
          <p className="dashboard-lead">
            Track approvals, disbursement movement, collection coverage and overdue pressure from one screen.
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

      {/* ── Brief cards ──────────────────────────────────────────────── */}
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
          <p>Prioritize officer and approver workload from this count.</p>
        </div>
      </section>

      {/* ── KPI grid ─────────────────────────────────────────────────── */}
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

      <div className="dashboard-grid">
        <Card hoverable>
          <CardHeader
            title="Application pipeline"
            subtitle="Current application volume by workflow status."
          />
          <CardBody>
            {loading ? <ChartSkeleton height={220} /> : pipelineData.length === 0 ? (
              <p className="no-data">No application data available.</p>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={pipelineData} margin={{ top: 8, right: 20, left: 8, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(16,35,63,0.06)" />
                  <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} />
                  <YAxis tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} allowDecimals={false} width={36} />
                  <Tooltip content={<ChartTooltip currencyKeys={['amount']} />} />
                  <Bar dataKey="count" name="Applications" radius={[6, 6, 0, 0]} maxBarSize={54}>
                    {pipelineData.map((entry, idx) => (
                      <Cell key={idx} fill={entry._color} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardBody>
        </Card>

        <Card hoverable>
          <CardHeader
            title="Monthly decision mix"
            subtitle="Approval and disbursement movement for the current month."
          />
          <CardBody>
            {loading ? <ChartSkeleton height={220} /> : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '1rem' }}>
                <div>
                  <span className="dashboard-brief-label">Approved</span>
                  <strong style={{ display: 'block', fontSize: '1.6rem', color: 'var(--navy)', marginTop: '0.35rem' }}>
                    {formatNumber(summary?.approvedApplicationsThisMonth || 0)}
                  </strong>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.82rem', marginTop: '0.4rem' }}>Applications approved this month.</p>
                </div>
                <div>
                  <span className="dashboard-brief-label">Rejected</span>
                  <strong style={{ display: 'block', fontSize: '1.6rem', color: 'var(--navy)', marginTop: '0.35rem' }}>
                    {formatNumber(summary?.rejectedApplicationsThisMonth || 0)}
                  </strong>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.82rem', marginTop: '0.4rem' }}>Applications rejected this month.</p>
                </div>
                <div>
                  <span className="dashboard-brief-label">Disbursed</span>
                  <strong style={{ display: 'block', fontSize: '1.6rem', color: 'var(--navy)', marginTop: '0.35rem' }}>
                    {formatNumber(summary?.disbursedLoansThisMonth || 0)}
                  </strong>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.82rem', marginTop: '0.4rem' }}>Loans disbursed this month.</p>
                </div>
              </div>
            )}
          </CardBody>
        </Card>
      </div>

      {/* ── Chart row 1: Overdue bar + Status donut ──────────────────── */}
      <div className="dashboard-grid">

        {/* Horizontal bar — overdue DPD */}
        <Card hoverable>
          <CardHeader
            title="Overdue pressure by DPD bucket"
            subtitle="Number of accounts per delinquency bucket."
          />
          <CardBody>
            {bucketsLoading ? <ChartSkeleton height={220} /> : bucketBarData.length === 0 ? (
              <div className="empty-state" style={{ padding: '3rem 0' }}>
                <span className="empty-icon">✅</span>
                <p>No overdue loans — portfolio is healthy.</p>
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={bucketBarData} layout="vertical" margin={{ top: 0, right: 24, left: 8, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="rgba(16,35,63,0.06)" />
                  <XAxis type="number" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false}
                    tickFormatter={v => formatNumber(v)} />
                  <YAxis type="category" dataKey="name" tick={{ fontSize: 11, fill: '#7a8da5' }}
                    axisLine={false} tickLine={false} width={70} />
                  <Tooltip content={<ChartTooltip currencyKeys={['amount']} />} />
                  <Bar dataKey="accounts" name="Accounts" radius={[0, 6, 6, 0]}>
                    {bucketBarData.map((entry, idx) => (
                      <Cell key={idx} fill={entry._color} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardBody>
        </Card>

        {/* Donut — loan status */}
        <Card hoverable>
          <CardHeader
            title="Loan book distribution"
            subtitle="Portfolio split across lifecycle stages."
          />
          <CardBody>
            {loading ? <ChartSkeleton height={220} /> : pieData.length === 0 ? (
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
                      {pieData.map((_, i) => <Cell key={i} fill={pieColors[i]} />)}
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

      {/* ── Chart row 2: Area trend (full width) ─────────────────────── */}
      <Card hoverable style={{ marginTop: '1rem' }}>
        <CardHeader
          title="Disbursement vs Collection — Last 6 months"
          subtitle="Monthly comparison of new loan disbursements against principal and interest collected."
        />
        <CardBody>
          {trendLoading ? <ChartSkeleton height={240} /> : !hasTrend ? (
            <p className="no-data" style={{ padding: '3rem 0', textAlign: 'center' }}>
              Trend data will appear after the backend deployment is complete.
            </p>
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
                <YAxis
                  tickFormatter={v => v === 0 ? '0' : `${(v / 1e9).toFixed(1)}B`}
                  tick={{ fontSize: 11, fill: '#7a8da5' }}
                  axisLine={false} tickLine={false} width={52}
                />
                <Tooltip content={<ChartTooltip currencyKeys={['disbursed', 'collected']} />} />
                <Legend wrapperStyle={{ fontSize: 12, paddingTop: 8 }} />
                <Area type="monotone" dataKey="disbursed" name="Disbursed"
                  stroke={PALETTE.blue}  strokeWidth={2} fill="url(#gradDis)"
                  dot={{ r: 3, fill: PALETTE.blue }} />
                <Area type="monotone" dataKey="collected" name="Collected"
                  stroke={PALETTE.green} strokeWidth={2} fill="url(#gradCol)"
                  dot={{ r: 3, fill: PALETTE.green }} />
              </AreaChart>
            </ResponsiveContainer>
          )}
        </CardBody>
      </Card>

      {/* ── Chart row 3: New loan volume bar ─────────────────────────── */}
      {hasTrend && (
        <Card hoverable style={{ marginTop: '1rem' }}>
          <CardHeader
            title="New loan volume — Last 6 months"
            subtitle="Number of new loans disbursed each month. Highlights origination trend."
          />
          <CardBody>
            <ResponsiveContainer width="100%" height={180}>
              <BarChart data={trend} margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(16,35,63,0.06)" />
                {/* dataKey="label" matches camelCase backend field */}
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#7a8da5' }} axisLine={false} tickLine={false}
                  allowDecimals={false} width={32} />
                <Tooltip content={<ChartTooltip />} />
                {/* dataKey="newLoans" matches camelCase backend field NewLoans → newLoans */}
                <Bar dataKey="newLoans" name="New Loans" fill={PALETTE.teal} radius={[6, 6, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          </CardBody>
        </Card>
      )}

    </div>
  );
}
