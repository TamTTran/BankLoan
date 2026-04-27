import { useState, useEffect, useCallback } from 'react';
import { amlApi } from '../features/aml/api/amlApi';
import { useToast } from '../context/ToastContext';

const RISK_LABELS = { Low: 'Thấp', Medium: 'Trung bình', High: 'Cao', VeryHigh: 'Rất cao' };
const RISK_COLORS = { Low: 'var(--green)', Medium: 'var(--amber)', High: 'var(--red)', VeryHigh: '#7b0000' };

function RiskBadge({ level }) {
  return (
    <span style={{
      display: 'inline-block',
      padding: '0.2rem 0.6rem',
      borderRadius: 999,
      fontSize: '0.72rem',
      fontWeight: 800,
      background: level === 'Low' ? 'var(--green-bg)' : level === 'Medium' ? 'var(--amber-bg)' : 'var(--red-bg)',
      color: RISK_COLORS[level] || 'var(--text-secondary)',
    }}>
      {RISK_LABELS[level] || level}
    </span>
  );
}

function StatCard({ value, label, cls = 'kpi-blue', icon }) {
  return (
    <div className={`kpi-card ${cls}`}>
      {icon && <div className="kpi-icon kpi-icon-text">{icon}</div>}
      <div>
        <p className="kpi-value">{value}</p>
        <p className="kpi-label">{label}</p>
      </div>
    </div>
  );
}

export default function AmlDashboard() {
  const { toast } = useToast();
  const [tab, setTab] = useState('alerts');
  const [alerts, setAlerts] = useState(null);
  const [strList, setStrList] = useState(null);
  const [loading, setLoading] = useState(false);
  const [screening, setScreening] = useState(false);
  const [screenId, setScreenId] = useState('');
  const [showStrForm, setShowStrForm] = useState(false);
  const [strForm, setStrForm] = useState({ customerId: '', transactionId: '', reason: '', reportType: 'STR' });
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      if (tab === 'alerts') {
        setAlerts(await amlApi.getAlerts());
      } else if (tab === 'str') {
        setStrList(await amlApi.getStrList());
      }
    } catch (e) {
      toast('Lỗi tải dữ liệu AML: ' + (e.message || ''), 'error');
    } finally {
      setLoading(false);
    }
  }, [tab]);

  useEffect(() => { load(); }, [load]);

  const handleScreen = async () => {
    if (!screenId.trim()) { toast('Nhập Customer ID', 'error'); return; }
    setScreening(true);
    try {
      const res = await amlApi.screenCustomer(parseInt(screenId));
      toast(`✅ Screening xong — Rủi ro: ${res?.riskLevel || 'N/A'}`, 'success');
      load();
    } catch (e) {
      toast('Lỗi screening: ' + (e.message || ''), 'error');
    } finally {
      setScreening(false);
    }
  };

  const handleSubmitStr = async () => {
    if (!strForm.reason.trim()) { toast('Nhập mô tả nghi vấn', 'error'); return; }
    setSubmitting(true);
    try {
      await amlApi.submitStr({
        customerId: parseInt(strForm.customerId),
        suspectedTransactionId: strForm.transactionId ? parseInt(strForm.transactionId) : null,
        reportType: strForm.reportType,
        narrativeDescription: strForm.reason,
      });
      toast('✅ Đã nộp báo cáo STR thành công!', 'success');
      setShowStrForm(false);
      setStrForm({ customerId: '', transactionId: '', reason: '', reportType: 'STR' });
      load();
    } catch (e) {
      toast('Lỗi nộp STR: ' + (e.message || ''), 'error');
    } finally {
      setSubmitting(false);
    }
  };

  const TABS = [
    { key: 'alerts', label: '🔔 Cảnh báo giao dịch' },
    { key: 'str', label: '📋 Báo cáo STR' },
  ];

  const alertItems = alerts?.items || alerts || [];
  const strItems = strList?.items || strList || [];
  const highRiskCount = alertItems.filter?.(a => a.riskLevel === 'High' || a.riskLevel === 'VeryHigh').length || 0;

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>🛡️ AML / KYC Dashboard</h2>
          <p className="subtitle">Giám sát tuân thủ phòng chống rửa tiền theo TT09/2023/TT-NHNN</p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <input
            className="search-input"
            placeholder="Customer ID để screening..."
            value={screenId}
            onChange={e => setScreenId(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleScreen()}
            style={{ width: 220 }}
          />
          <button className="btn btn-primary" onClick={handleScreen} disabled={screening}>
            {screening ? '⏳' : '🔍'} Screening
          </button>
          <button className="btn btn-outline" onClick={() => setShowStrForm(true)}>
            + Nộp STR
          </button>
        </div>
      </div>

      {/* KPI bar */}
      <div className="kpi-grid" style={{ marginBottom: '1.5rem' }}>
        <StatCard icon="🔔" value={alertItems.length} label="Tổng cảnh báo" cls="kpi-amber" />
        <StatCard icon="🔴" value={highRiskCount} label="Rủi ro Cao/Rất Cao" cls="kpi-red" />
        <StatCard icon="📋" value={strItems.length} label="Báo cáo STR" cls="kpi-blue" />
        <StatCard icon="✅" value={strItems.filter?.(s => s.status === 'Submitted').length || 0} label="STR đã nộp NHNN" cls="kpi-green" />
      </div>

      {/* Tab bar */}
      <div style={{ display: 'flex', gap: '0.4rem', marginBottom: '1.5rem', borderBottom: '2px solid var(--border)', paddingBottom: 0, flexWrap: 'wrap' }}>
        {TABS.map(t => (
          <button key={t.key} onClick={() => setTab(t.key)}
            style={{
              padding: '0.6rem 1rem', border: 'none', background: 'transparent',
              fontFamily: 'inherit', fontSize: '0.85rem', fontWeight: 700, cursor: 'pointer',
              color: tab === t.key ? 'var(--blue)' : 'var(--text-muted)',
              borderBottom: tab === t.key ? '2px solid var(--blue)' : '2px solid transparent',
              marginBottom: '-2px', transition: 'color 0.18s',
            }}>
            {t.label}
          </button>
        ))}
      </div>

      {loading && <p style={{ color: 'var(--text-muted)', padding: '2rem 0' }}>Đang tải dữ liệu…</p>}

      {/* Alerts tab */}
      {tab === 'alerts' && !loading && (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Khách hàng</th>
                <th>Mức rủi ro</th>
                <th>PEP</th>
                <th>Sanction</th>
                <th>Ghi chú</th>
                <th>Ngày đánh giá</th>
              </tr>
            </thead>
            <tbody>
              {alertItems.length === 0 ? (
                <tr><td colSpan={6} className="empty-row"><div className="empty-state">✅ Không có cảnh báo nào</div></td></tr>
              ) : alertItems.map((a, i) => (
                <tr key={a.id || i}>
                  <td><strong>#{a.customerId}</strong></td>
                  <td><RiskBadge level={a.riskLevel} /></td>
                  <td>{a.isPep ? <span style={{ color: 'var(--red)', fontWeight: 700 }}>⚠️ PEP</span> : <span style={{ color: 'var(--green)' }}>✓</span>}</td>
                  <td>{a.isSanctioned ? <span style={{ color: 'var(--red)', fontWeight: 700 }}>🚫 Danh sách đen</span> : <span style={{ color: 'var(--green)' }}>✓ Sạch</span>}</td>
                  <td><span className="cell-sub">{a.notes || '—'}</span></td>
                  <td><span className="cell-sub">{a.assessedAt ? new Date(a.assessedAt).toLocaleDateString('vi-VN') : '—'}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* STR tab */}
      {tab === 'str' && !loading && (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã báo cáo</th>
                <th>Khách hàng</th>
                <th>Loại báo cáo</th>
                <th>Trạng thái</th>
                <th>Mô tả</th>
                <th>Ngày nộp</th>
              </tr>
            </thead>
            <tbody>
              {strItems.length === 0 ? (
                <tr><td colSpan={6} className="empty-row"><div className="empty-state">📋 Chưa có báo cáo STR nào</div></td></tr>
              ) : strItems.map((s, i) => (
                <tr key={s.id || i}>
                  <td><span className="code-badge">STR-{s.id}</span></td>
                  <td>#{s.customerId}</td>
                  <td><span className="code-badge">{s.reportType || 'STR'}</span></td>
                  <td>
                    {s.status === 'Submitted'
                      ? <span style={{ color: 'var(--green)', fontWeight: 600 }}>✅ Đã nộp</span>
                      : <span style={{ color: 'var(--amber)', fontWeight: 600 }}>⏳ Dự thảo</span>}
                  </td>
                  <td><span className="cell-sub">{s.narrativeDescription?.substring(0, 80)}…</span></td>
                  <td><span className="cell-sub">{s.createdAt ? new Date(s.createdAt).toLocaleDateString('vi-VN') : '—'}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* STR Form modal */}
      {showStrForm && (
        <div className="modal-overlay" onClick={() => setShowStrForm(false)}>
          <div className="modal-panel" style={{ maxWidth: 520 }} onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h3>📋 Nộp Báo cáo Giao dịch Nghi ngờ (STR)</h3>
              <button className="modal-close" onClick={() => setShowStrForm(false)}>✕</button>
            </div>
            <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <div className="form-row">
                <div className="form-group">
                  <label>Customer ID <span className="required">*</span></label>
                  <input type="number" className="form-input" value={strForm.customerId}
                    onChange={e => setStrForm(f => ({ ...f, customerId: e.target.value }))} placeholder="VD: 101" />
                </div>
                <div className="form-group">
                  <label>Transaction ID</label>
                  <input type="number" className="form-input" value={strForm.transactionId}
                    onChange={e => setStrForm(f => ({ ...f, transactionId: e.target.value }))} placeholder="Tuỳ chọn" />
                </div>
              </div>
              <div className="form-group">
                <label>Loại báo cáo</label>
                <select className="form-input" value={strForm.reportType}
                  onChange={e => setStrForm(f => ({ ...f, reportType: e.target.value }))}>
                  <option value="STR">STR — Báo cáo giao dịch đáng ngờ</option>
                  <option value="CTR">CTR — Báo cáo giao dịch tiền mặt lớn</option>
                </select>
              </div>
              <div className="form-group">
                <label>Mô tả nghi vấn <span className="required">*</span></label>
                <textarea className="form-input" rows={4} value={strForm.reason}
                  onChange={e => setStrForm(f => ({ ...f, reason: e.target.value }))}
                  placeholder="Mô tả chi tiết về hành vi giao dịch đáng ngờ..." />
              </div>
              <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                <button className="btn btn-ghost" onClick={() => setShowStrForm(false)} disabled={submitting}>Huỷ</button>
                <button className="btn btn-primary" onClick={handleSubmitStr} disabled={submitting}>
                  {submitting ? '⏳ Đang nộp…' : '📤 Nộp STR'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
