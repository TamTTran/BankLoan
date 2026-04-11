import { useState, useEffect } from 'react';
import { api } from '../api';
import { formatCurrency, formatNumber, DPD_LABELS, STATUS_LABELS } from '../utils/format';
import { DpdBadge } from '../components/Badge';

const KPI_CARDS = [
  { key: 'totalOutstanding',      icon: '💰', label: 'Tổng dư nợ',          cls: 'kpi-blue',   fmt: formatCurrency },
  { key: 'totalActiveLoans',      icon: '✅', label: 'Khoản vay HĐ',        cls: 'kpi-green',  fmt: formatNumber },
  { key: 'pendingApprovals',      icon: '⏳', label: 'Chờ phê duyệt',        cls: 'kpi-amber',  fmt: formatNumber },
  { key: 'overdueAccounts',       icon: '🚨', label: 'Tài khoản quá hạn',   cls: 'kpi-red',    fmt: formatNumber },
  { key: 'totalDisbursedThisMonth', icon: '📈', label: 'Giải ngân tháng này', cls: 'kpi-purple', fmt: formatCurrency },
  { key: 'collectedThisMonth',    icon: '💵', label: 'Thu nợ tháng này',     cls: 'kpi-teal',   fmt: formatCurrency },
];
const BUCKET_COLORS = { Current:'#10B981', DPD1_7:'#F59E0B', DPD8_30:'#F97316', DPD31Plus:'#EF4444' };

export default function Dashboard({ onPendingCount }) {
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.dashboard.summary().then(d => {
      setSummary(d);
      onPendingCount?.(d?.pendingApprovals || 0);
    }).catch(console.error).finally(() => setLoading(false));
  }, []);

  const dateStr = new Date().toLocaleDateString('vi-VN', { weekday:'long', year:'numeric', month:'long', day:'numeric' });

  return (
    <div className="page">
      <div className="page-header">
        <h2>📊 Dashboard</h2>
        <span className="subtitle">{dateStr}</span>
      </div>

      {/* KPI Grid */}
      <div className="kpi-grid">
        {KPI_CARDS.map(c => (
          <div key={c.key} className={`kpi-card ${c.cls}`}>
            <div className="kpi-icon">{c.icon}</div>
            <div>
              <p className="kpi-label">{c.label}</p>
              <p className="kpi-value">
                {loading ? '—' : c.fmt(summary?.[c.key])}
              </p>
            </div>
          </div>
        ))}
      </div>

      {/* Charts */}
      <div className="dashboard-grid">
        <div className="card">
          <div className="card-header"><h3>📉 Phân tích quá hạn (DPD)</h3></div>
          <div className="card-body">
            {loading ? <div className="loading-shimmer" /> :
              (summary?.overdueBuckets?.length ? (() => {
                const total = summary.overdueBuckets.reduce((s, b) => s + b.count, 0) || 1;
                return summary.overdueBuckets.map(b => (
                  <div key={b.bucket} className="bucket-row">
                    <span className="bucket-label"><DpdBadge bucket={b.bucket} /></span>
                    <div className="bucket-bar-wrap">
                      <div className="bucket-bar" style={{ width: `${Math.round(b.count/total*100)}%`, background: BUCKET_COLORS[b.bucket] || '#3B82F6' }} />
                    </div>
                    <span className="bucket-count">{b.count}</span>
                    <span className="bucket-amount">{formatCurrency(b.totalAmount)}</span>
                  </div>
                ));
              })() : <p className="no-data">Không có dữ liệu quá hạn 🎉</p>)
            }
          </div>
        </div>

        <div className="card">
          <div className="card-header"><h3>📊 Trạng thái khoản vay</h3></div>
          <div className="card-body">
            {loading ? <div className="loading-shimmer" /> :
              (summary?.loanStatusBreakdown?.length ?
                summary.loanStatusBreakdown.map(s => (
                  <div key={s.status} className="bucket-row">
                    <span className="bucket-label">{STATUS_LABELS[s.status] || s.status}</span>
                    <span className="bucket-count">{s.count}</span>
                    <span className="bucket-amount">{formatCurrency(s.totalAmount)}</span>
                  </div>
                )) : <p className="no-data">Không có dữ liệu</p>)
            }
          </div>
        </div>
      </div>
    </div>
  );
}
