import { useState, useEffect, useCallback } from 'react';
import { riskApi } from '../features/risk/api/riskApi';
import { useToast } from '../context/ToastContext';

const fmt = (n) => Number(n || 0).toLocaleString('vi-VN');
const pct = (n) => `${Number(n || 0).toFixed(2)}%`;

function Gauge({ value, max = 100, thresholds = { warning: 60, danger: 80 }, label, unit = '%' }) {
  const pctValue = Math.min((value / max) * 100, 100);
  const color = pctValue >= thresholds.danger ? 'var(--red)' : pctValue >= thresholds.warning ? 'var(--amber)' : 'var(--green)';
  return (
    <div style={{ padding: '1.25rem', border: '1px solid var(--border)', borderRadius: 18, background: 'rgba(255,255,255,0.9)' }}>
      <p style={{ fontSize: '0.72rem', fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '0.75rem' }}>{label}</p>
      <div style={{ position: 'relative', height: 10, background: 'var(--bg-muted)', borderRadius: 999, overflow: 'hidden' }}>
        <div style={{ position: 'absolute', inset: '0 auto 0 0', width: `${pctValue}%`, background: color, borderRadius: 999, transition: 'width 0.6s ease' }} />
      </div>
      <p style={{ marginTop: '0.6rem', fontSize: '1.5rem', fontWeight: 800, letterSpacing: '-0.04em', color }}>{value}{unit}</p>
    </div>
  );
}

function SectionTitle({ children }) {
  return <h3 style={{ fontSize: '1rem', fontWeight: 800, color: 'var(--navy)', margin: '1.5rem 0 0.75rem' }}>{children}</h3>;
}

export default function RiskDashboard() {
  const { toast } = useToast();
  const [tab, setTab] = useState('capital');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [bsDate, setBsDate] = useState(new Date().toISOString().split('T')[0]);
  const [pnlFrom, setPnlFrom] = useState(new Date(Date.now() - 30 * 86400000).toISOString().split('T')[0]);
  const [pnlTo, setPnlTo] = useState(new Date().toISOString().split('T')[0]);

  const TABS = [
    { key: 'capital', label: '🏦 Hệ số an toàn vốn (CAR)' },
    { key: 'concentration', label: '📊 Tập trung tín dụng' },
    { key: 'balancesheet', label: '📋 Bảng CĐKT' },
    { key: 'pnl', label: '💹 Kết quả KD' },
    { key: 'ifrs9', label: '🔬 IFRS 9 ECL' },
  ];

  const load = useCallback(async () => {
    setLoading(true);
    setData(null);
    try {
      switch (tab) {
        case 'capital':
          setData(await riskApi.getCapitalAdequacy()); break;
        case 'concentration':
          setData(await riskApi.getConcentrationRisk()); break;
        case 'balancesheet':
          setData(await riskApi.getBalanceSheet(bsDate)); break;
        case 'pnl':
          setData(await riskApi.getPnl(pnlFrom, pnlTo)); break;
        case 'ifrs9':
          setData(await riskApi.getExpectedLossSummary()); break;
        default:
          setData(null);
      }
    } catch (e) {
      toast('Lỗi tải dữ liệu: ' + (e.message || ''), 'error');
    } finally {
      setLoading(false);
    }
  }, [tab, bsDate, pnlFrom, pnlTo]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>⚖️ Risk Dashboard</h2>
          <p className="subtitle">Quản trị rủi ro tập trung — IFRS 9, CAR, ECL, Báo cáo tài chính</p>
        </div>
        <button className="btn btn-outline" onClick={load}>🔄 Làm mới</button>
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

      {/* Date filters for balance sheet & P&L */}
      {tab === 'balancesheet' && (
        <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div className="form-group">
            <label>Ngày báo cáo</label>
            <input type="date" className="form-input" value={bsDate} onChange={e => setBsDate(e.target.value)} style={{ width: 180 }} />
          </div>
          <button className="btn btn-primary" onClick={load}>Xem báo cáo</button>
        </div>
      )}
      {tab === 'pnl' && (
        <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div className="form-group">
            <label>Từ ngày</label>
            <input type="date" className="form-input" value={pnlFrom} onChange={e => setPnlFrom(e.target.value)} style={{ width: 180 }} />
          </div>
          <div className="form-group">
            <label>Đến ngày</label>
            <input type="date" className="form-input" value={pnlTo} onChange={e => setPnlTo(e.target.value)} style={{ width: 180 }} />
          </div>
          <button className="btn btn-primary" onClick={load}>Xem P&amp;L</button>
        </div>
      )}

      {loading && <p style={{ color: 'var(--text-muted)', padding: '2rem 0' }}>Đang tải dữ liệu rủi ro…</p>}

      {/* ── CAR ── */}
      {tab === 'capital' && data && (
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
            <Gauge label="CAR (Hệ số an toàn vốn)" value={Number(data.carRatio || 0).toFixed(2)} unit="%" max={20} thresholds={{ warning: 10, danger: 8 }} />
            <Gauge label="Tier 1 Capital Ratio" value={Number(data.tier1CapitalRatio || 0).toFixed(2)} unit="%" max={20} thresholds={{ warning: 8, danger: 6 }} />
            <Gauge label="NPL Ratio" value={Number(data.nplRatio || 0).toFixed(2)} unit="%" max={10} thresholds={{ warning: 2, danger: 3 }} />
            <Gauge label="Provision Coverage" value={Number(data.provisionCoverageRatio || 0).toFixed(2)} unit="%" max={200} thresholds={{ warning: 60, danger: 30 }} />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem' }}>
            <div className="kpi-card kpi-blue"><div><p className="kpi-value">{fmt(data.totalCapital)}</p><p className="kpi-label">Vốn tự có</p></div></div>
            <div className="kpi-card kpi-amber"><div><p className="kpi-value">{fmt(data.riskWeightedAssets)}</p><p className="kpi-label">Tài sản tính theo rủi ro (RWA)</p></div></div>
            <div className="kpi-card kpi-teal"><div><p className="kpi-value">{fmt(data.totalProvision)}</p><p className="kpi-label">Tổng dự phòng</p></div></div>
          </div>
        </div>
      )}

      {/* ── Concentration Risk ── */}
      {tab === 'concentration' && data && (
        <div>
          <SectionTitle>Giới hạn tín dụng theo khách hàng / nhóm KH</SectionTitle>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Tên / Nhóm</th>
                  <th>Loại</th>
                  <th className="text-right">Dư nợ hiện tại</th>
                  <th className="text-right">Giới hạn</th>
                  <th className="text-right">Tỉ lệ sử dụng</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {(!data.limits || data.limits?.length === 0) ? (
                  <tr><td colSpan={6} className="empty-row"><div className="empty-state">✅ Không có vi phạm giới hạn tín dụng</div></td></tr>
                ) : data.limits?.map((l, i) => {
                  const ratio = Number(l.utilizationRatio ?? (l.limitAmount > 0 ? (l.currentExposure / l.limitAmount) * 100 : 0));
                  const exceeded = l.isBreached ?? ratio > 100;
                  return (
                    <tr key={i}>
                      <td><strong>{l.customerName || l.groupName || `#${l.id}`}</strong></td>
                      <td><span className="code-badge">{l.limitType}</span></td>
                      <td className="text-right">{fmt(l.currentExposure)}</td>
                      <td className="text-right">{fmt(l.limitAmount)}</td>
                      <td className="text-right" style={{ color: exceeded ? 'var(--red)' : 'var(--green)', fontWeight: 700 }}>{pct(ratio)}</td>
                      <td>{exceeded
                        ? <span style={{ color: 'var(--red)', fontWeight: 600 }}>⚠️ Vượt hạn</span>
                        : <span style={{ color: 'var(--green)', fontWeight: 600 }}>✅ Bình thường</span>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Balance Sheet ── */}
      {tab === 'balancesheet' && data && (
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
            <div className="kpi-card kpi-blue"><div><p className="kpi-value">{fmt(data.totalAssets)}</p><p className="kpi-label">Tổng tài sản</p></div></div>
            <div className="kpi-card kpi-red"><div><p className="kpi-value">{fmt(data.totalLiabilities)}</p><p className="kpi-label">Tổng nợ phải trả</p></div></div>
            <div className="kpi-card kpi-green"><div><p className="kpi-value">{fmt(data.totalEquity)}</p><p className="kpi-label">Vốn chủ sở hữu</p></div></div>
            <div className="kpi-card kpi-purple"><div><p className="kpi-value">{fmt(data.netIncome)}</p><p className="kpi-label">Lợi nhuận ròng</p></div></div>
          </div>
          <div className="table-shell">
            <table className="data-table">
              <thead><tr><th>Mã TK</th><th>Tên tài khoản</th><th>Loại</th><th className="text-right">Số dư</th></tr></thead>
              <tbody>
                {data.details?.map((d, i) => (
                  <tr key={i}>
                    <td><span className="code-badge">{d.accountCode}</span></td>
                    <td>{d.accountName}</td>
                    <td><span className="code-badge">{d.accountType}</span></td>
                    <td className="text-right" style={{ fontWeight: 700 }}>{fmt(d.balance)}</td>
                  </tr>
                ))}
                {!data.details?.length && (
                  <tr><td colSpan={4} className="empty-row"><div className="empty-state">Không có dữ liệu tài khoản</div></td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── P&L ── */}
      {tab === 'pnl' && data && (
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
            <div className="kpi-card kpi-green"><div><p className="kpi-value">{fmt(data.totalAssets)}</p><p className="kpi-label">Tổng doanh thu</p></div></div>
            <div className="kpi-card kpi-red"><div><p className="kpi-value">{fmt(data.totalLiabilities)}</p><p className="kpi-label">Tổng chi phí</p></div></div>
            <div className="kpi-card kpi-blue"><div><p className="kpi-value">{fmt(data.netIncome)}</p><p className="kpi-label">Lợi nhuận trước thuế</p></div></div>
          </div>
          <div className="table-shell">
            <table className="data-table">
              <thead><tr><th>Mã TK</th><th>Tên tài khoản</th><th>Loại</th><th className="text-right">Số tiền</th></tr></thead>
              <tbody>
                {data.details?.map((d, i) => (
                  <tr key={i}>
                    <td><span className="code-badge">{d.accountCode}</span></td>
                    <td>{d.accountName}</td>
                    <td><span className="code-badge">{d.accountType}</span></td>
                    <td className="text-right" style={{ fontWeight: 700, color: d.accountType === 'Revenue' ? 'var(--green)' : 'var(--red)' }}>{fmt(d.balance)}</td>
                  </tr>
                ))}
                {!data.details?.length && (
                  <tr><td colSpan={4} className="empty-row"><div className="empty-state">Không có dữ liệu P&L</div></td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── IFRS 9 placeholder ── */}
      {tab === 'ifrs9' && (
        <div style={{ padding: '3rem 2rem', textAlign: 'center', color: 'var(--text-muted)' }}>
          <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>🔬</div>
          <h3 style={{ marginBottom: '0.5rem', color: 'var(--navy)' }}>IFRS 9 ECL Drill-down</h3>
          <p>Xem phân bổ Stage 1 / 2 / 3 và ECL amount theo danh mục vay trong module tiếp theo.</p>
          <div style={{ marginTop: '1.5rem', display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', maxWidth: 480, margin: '1.5rem auto 0' }}>
            <div style={{ padding: '1rem', borderRadius: 14, background: 'var(--green-bg)', border: '1px solid rgba(30,155,105,0.2)' }}>
              <p style={{ fontSize: '1.5rem', fontWeight: 800, color: 'var(--green)' }}>Stage 1</p>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>12-month ECL</p>
            </div>
            <div style={{ padding: '1rem', borderRadius: 14, background: 'var(--amber-bg)', border: '1px solid rgba(201,138,27,0.2)' }}>
              <p style={{ fontSize: '1.5rem', fontWeight: 800, color: 'var(--amber)' }}>Stage 2</p>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Lifetime ECL (SICR)</p>
            </div>
            <div style={{ padding: '1rem', borderRadius: 14, background: 'var(--red-bg)', border: '1px solid rgba(195,79,79,0.2)' }}>
              <p style={{ fontSize: '1.5rem', fontWeight: 800, color: 'var(--red)' }}>Stage 3</p>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Impaired (100% PD)</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
