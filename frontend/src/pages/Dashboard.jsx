import { useState, useEffect } from 'react';
import { dashboardApi } from '../features/dashboard/api/dashboardApi';
import { mapDashboardSummary } from '../features/dashboard/mappers/dashboardMapper';
import { formatCurrency, formatNumber, STATUS_LABELS } from '../utils/format';
import { DpdBadge } from '../components/Badge';
import { Card, CardHeader, CardBody } from '../components/ui/Card';

const KPI_CARDS = [
  { key: 'totalOutstanding', icon: 'VND', label: 'Total outstanding', cls: 'kpi-blue', fmt: formatCurrency },
  { key: 'totalActiveLoans', icon: 'LN', label: 'Active loans', cls: 'kpi-green', fmt: formatNumber },
  { key: 'pendingApprovals', icon: 'AP', label: 'Pending approvals', cls: 'kpi-amber', fmt: formatNumber },
  { key: 'carRatio', icon: 'CAR', label: 'CAR', cls: 'kpi-purple', fmt: (v) => `${Number(v || 0).toFixed(2)}%` },
  { key: 'nplRatio', icon: 'NPL', label: 'NPL Ratio', cls: 'kpi-red', fmt: (v) => `${Number(v || 0).toFixed(2)}%` },
  { key: 'collectedThisMonth', icon: 'COL', label: 'Collected this month', cls: 'kpi-teal', fmt: formatCurrency },
];

const BUCKET_COLORS = {
  Current: '#1e9b69',
  DPD1_7: '#c98a1b',
  DPD8_30: '#d9a441',
  DPD31Plus: '#c34f4f'
};

export default function Dashboard({ onPendingCount }) {
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      dashboardApi.summary().then(mapDashboardSummary),
      dashboardApi.capitalAdequacy()
    ])
    .then(([dashData, capData]) => {
      const merged = { ...dashData, ...capData };
      setSummary(merged);
      onPendingCount?.(merged?.pendingApprovals || 0);
    })
    .catch(console.error)
    .finally(() => setLoading(false));
  }, []);

  const overdueBuckets = summary?.overdueBuckets || [];
  const loanBreakdown = summary?.loanStatusBreakdown || [];
  const topBucket = overdueBuckets.reduce((max, item) => (item.count > (max?.count || 0) ? item : max), null);
  const topLoanStatus = loanBreakdown.reduce((max, item) => (item.count > (max?.count || 0) ? item : max), null);
  const coverageRate = summary?.totalOutstanding
    ? Math.round(((summary?.collectedThisMonth || 0) / summary.totalOutstanding) * 100)
    : 0;

  const dateStr = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });

  return (
    <div className="page">
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

      <section className="dashboard-brief-grid">
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Priority watch</span>
          <strong>{loading ? 'Loading...' : (topBucket?.bucket || 'Current')}</strong>
          <p>{loading ? 'Preparing portfolio signal.' : `Highest pressure bucket by volume: ${topBucket?.count || 0} account(s).`}</p>
        </div>
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Portfolio mix</span>
          <strong>{loading ? 'Loading...' : (STATUS_LABELS[topLoanStatus?.status] || topLoanStatus?.status || 'N/A')}</strong>
          <p>{loading ? 'Preparing loan mix.' : `Largest status segment with ${formatNumber(topLoanStatus?.count || 0)} loan(s).`}</p>
        </div>
        <div className="dashboard-brief-card">
          <span className="dashboard-brief-label">Approvals queue</span>
          <strong>{loading ? '—' : formatNumber(summary?.pendingApprovals || 0)}</strong>
          <p>Use this count to prioritize officer and approver daily workload.</p>
        </div>
      </section>

      <div className="kpi-grid">
        {KPI_CARDS.map((card) => (
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
            title="Overdue pressure by bucket"
            subtitle="Shows where collection attention is building across the current portfolio."
          />
          <CardBody>
            {loading ? (
              <div className="loading-shimmer" />
            ) : overdueBuckets.length ? (
              overdueBuckets.map((bucket) => {
                const total = overdueBuckets.reduce((sum, item) => sum + item.count, 0) || 1;
                return (
                  <div key={bucket.bucket} className="bucket-row">
                    <span className="bucket-label"><DpdBadge bucket={bucket.bucket} /></span>
                    <div className="bucket-bar-wrap">
                      <div
                        className="bucket-bar"
                        style={{
                          width: `${Math.round((bucket.count / total) * 100)}%`,
                          background: BUCKET_COLORS[bucket.bucket] || '#1f5fbf'
                        }}
                      />
                    </div>
                    <span className="bucket-count">{bucket.count}</span>
                    <span className="bucket-amount">{formatCurrency(bucket.totalAmount)}</span>
                  </div>
                );
              })
            ) : (
              <p className="no-data">No overdue data available.</p>
            )}
          </CardBody>
        </Card>

        <Card hoverable>
          <CardHeader
            title="Loan status distribution"
            subtitle="Highlights how the book is currently split across lifecycle stages."
          />
          <CardBody>
            {loading ? (
              <div className="loading-shimmer" />
            ) : loanBreakdown.length ? (
              loanBreakdown.map((item) => (
                <div key={item.status} className="bucket-row">
                  <span className="bucket-label">{STATUS_LABELS[item.status] || item.status}</span>
                  <span className="bucket-count">{item.count}</span>
                  <span className="bucket-amount">{formatCurrency(item.totalAmount)}</span>
                </div>
              ))
            ) : (
              <p className="no-data">No loan status data available.</p>
            )}
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
