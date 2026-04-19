import { useState, useEffect } from 'react';
import { reportsApi } from '../features/reports/api/reportsApi';

const TABS = [
  { key: 'portfolio', label: '📊 Danh mục', icon: '📊' },
  { key: 'overdue', label: '📈 Biến động quá hạn', icon: '📈' },
  { key: 'productivity', label: '👥 Hiệu suất thu hồi', icon: '👥' },
  { key: 'approval', label: '✅ Hàng đợi duyệt', icon: '✅' },
];

const fmt = (n) => Number(n || 0).toLocaleString('vi-VN');
const pct = (n) => `${Number(n || 0).toFixed(1)}%`;

export default function Reports() {
  const [tab, setTab] = useState('portfolio');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [dateRange, setDateRange] = useState({
    from: new Date(Date.now() - 30 * 86400000).toISOString().split('T')[0],
    to: new Date().toISOString().split('T')[0]
  });

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      try {
        switch (tab) {
          case 'portfolio': setData(await reportsApi.getPortfolio()); break;
          case 'overdue': setData(await reportsApi.getOverdueMovement(dateRange.from, dateRange.to)); break;
          case 'productivity': setData(await reportsApi.getCollectionProductivity(dateRange.from, dateRange.to)); break;
          case 'approval': setData(await reportsApi.getApprovalQueue()); break;
        }
      } catch (e) { console.error(e); }
      setLoading(false);
    };
    load();
  }, [tab, dateRange.from, dateRange.to]);

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>📈 Báo cáo vận hành</h1>
      </div>

      {/* Tab Bar */}
      <div className="tab-bar" style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', borderBottom: '2px solid var(--border-color)', paddingBottom: '0.5rem' }}>
        {TABS.map(t => (
          <button
            key={t.key}
            className={`btn ${tab === t.key ? 'btn-primary' : 'btn-ghost'}`}
            onClick={() => { setTab(t.key); setData(null); }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Date range for overdue and productivity */}
      {(tab === 'overdue' || tab === 'productivity') && (
        <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem', alignItems: 'center' }}>
          <label>Từ:</label>
          <input type="date" value={dateRange.from} onChange={e => setDateRange(d => ({ ...d, from: e.target.value }))} />
          <label>Đến:</label>
          <input type="date" value={dateRange.to} onChange={e => setDateRange(d => ({ ...d, to: e.target.value }))} />
        </div>
      )}

      {loading && <p>Đang tải báo cáo...</p>}

      {/* Portfolio Report */}
      {tab === 'portfolio' && data && (
        <div>
          <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.totalLoans}</div><div className="kpi-label">Tổng khoản vay</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{fmt(data.totalOutstanding)}</div><div className="kpi-label">Tổng dư nợ</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.avgTenorMonths}</div><div className="kpi-label">Kỳ hạn TB (tháng)</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.avgInterestRate}%</div><div className="kpi-label">Lãi suất TB</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.disbursementsThisMonth}</div><div className="kpi-label">Giải ngân tháng này</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{fmt(data.disbursementVolumeThisMonth)}</div><div className="kpi-label">Giá trị giải ngân</div></div>
          </div>

          <h3 style={{ marginBottom: '1rem' }}>Phân bổ theo trạng thái</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead><tr><th>Trạng thái</th><th className="text-right">Số lượng</th><th className="text-right">Dư nợ</th></tr></thead>
              <tbody>
                {data.statusBreakdown?.map(s => (
                  <tr key={s.status}><td>{s.status}</td><td className="text-right">{s.count}</td><td className="text-right">{fmt(s.totalOutstanding)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>

          <h3 style={{ margin: '1.5rem 0 1rem' }}>Phân bổ theo loại sản phẩm</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead><tr><th>Loại sản phẩm</th><th className="text-right">Số lượng</th><th className="text-right">Dư nợ</th></tr></thead>
              <tbody>
                {data.productBreakdown?.map(p => (
                  <tr key={p.productType}><td>{p.productType}</td><td className="text-right">{p.count}</td><td className="text-right">{fmt(p.totalOutstanding)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Overdue Movement Report */}
      {tab === 'overdue' && data && (
        <div>
          <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.newOverdueCount}</div><div className="kpi-label">Mới quá hạn</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value" style={{ color: '#22c55e' }}>{data.curedCount}</div><div className="kpi-label">Đã khắc phục</div></div>
          </div>
          <div className="table-shell">
            <table className="data-table">
              <thead><tr><th>Ngày</th><th className="text-right">Số khoản QH</th><th className="text-right">Số tiền QH</th><th className="text-right">DPD TB</th></tr></thead>
              <tbody>
                {data.days?.map(d => (
                  <tr key={d.date}><td>{d.date}</td><td className="text-right">{d.totalOverdueLoans}</td><td className="text-right">{fmt(d.totalOverdueAmount)}</td><td className="text-right">{d.avgDpd}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Collection Productivity Report */}
      {tab === 'productivity' && data && (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Collector</th>
                <th className="text-right">Phân công</th>
                <th className="text-right">Liên hệ</th>
                <th className="text-right">Tiếp cận</th>
                <th className="text-right">Tỉ lệ tiếp cận</th>
                <th className="text-right">Hẹn trả</th>
                <th className="text-right">Giữ lời</th>
                <th className="text-right">Tỉ lệ giữ lời</th>
              </tr>
            </thead>
            <tbody>
              {data.collectors?.map(c => (
                <tr key={c.collectorUserId}>
                  <td><strong>{c.collectorName}</strong></td>
                  <td className="text-right">{c.totalAssigned}</td>
                  <td className="text-right">{c.totalActions}</td>
                  <td className="text-right">{c.contacted}</td>
                  <td className="text-right">{pct(c.contactRate)}</td>
                  <td className="text-right">{c.promisesMade}</td>
                  <td className="text-right">{c.promisesKept}</td>
                  <td className="text-right">{pct(c.promiseKeepRate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Approval Queue Report */}
      {tab === 'approval' && data && (
        <div>
          <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.totalPending}</div><div className="kpi-label">Đang chờ duyệt</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.avgDaysPending}</div><div className="kpi-label">Chờ TB (ngày)</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.approvedThisMonth}</div><div className="kpi-label">Đã duyệt tháng này</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{data.rejectedThisMonth}</div><div className="kpi-label">Đã từ chối</div></div>
            <div className="kpi-card kpi-blue"><div className="kpi-value">{pct(data.approvalRate)}</div><div className="kpi-label">Tỉ lệ duyệt</div></div>
          </div>
          {data.byApprover?.length > 0 && (
            <div className="table-shell">
              <table className="data-table">
                <thead><tr><th>Người duyệt</th><th className="text-right">Đã duyệt</th><th className="text-right">Đã từ chối</th></tr></thead>
                <tbody>
                  {data.byApprover.map(a => (
                    <tr key={a.approverName}><td>{a.approverName}</td><td className="text-right">{a.approved}</td><td className="text-right">{a.rejected}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
