import { useState, useEffect } from 'react';
import { reportsApi } from '../features/reports/api/reportsApi';
import { systemApi }  from '../features/system/api/systemApi';
import { Modal } from '../components/ui/Modal';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { useToast } from '../context/ToastContext';
import { useT } from '../i18n/useT';

const fmt  = (n)  => Number(n || 0).toLocaleString('vi-VN');
const pct  = (n)  => `${Number(n || 0).toFixed(2)}%`;


export default function Reports() {
  const { toast } = useToast();
  const { t } = useT();

  const TABS = [
    { key: 'portfolio',      label: `📊 ${t('reports.tabs.portfolio')}` },
    { key: 'overdue',        label: `📈 ${t('reports.tabs.overdue')}` },
    { key: 'productivity',   label: `👥 ${t('reports.tabs.productivity')}` },
    { key: 'approval',       label: `✅ ${t('reports.tabs.approval')}` },
    { key: 'npl',            label: `🏦 ${t('reports.tabs.npl')}` },
    { key: 'reconciliation', label: `🔄 ${t('reports.tabs.reconciliation')}` },
  ];
  const [tab, setTab]         = useState('portfolio');
  const [data, setData]       = useState(null);
  const [loading, setLoading] = useState(false);
  const [dateRange, setDateRange] = useState({
    from: new Date(Date.now() - 30 * 86400000).toISOString().split('T')[0],
    to:   new Date().toISOString().split('T')[0],
  });

  // Reconciliation resolve modal
  const [resolveTarget, setResolveTarget] = useState(null); // { id, loanAccountNo }
  const [resolveReason, setResolveReason] = useState('');
  const [resolving, setResolving]         = useState(false);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setData(null);
      try {
        switch (tab) {
          case 'portfolio':
            setData(await reportsApi.getPortfolio()); break;
          case 'overdue':
            setData(await reportsApi.getOverdueMovement(dateRange.from, dateRange.to)); break;
          case 'productivity':
            setData(await reportsApi.getCollectionProductivity(dateRange.from, dateRange.to)); break;
          case 'approval':
            setData(await reportsApi.getApprovalQueue()); break;
          case 'npl':
            const [summary, byProduct] = await Promise.all([
              systemApi.getNplSummary(),
              systemApi.getNplByProduct(),
            ]);
            setData({ summary, byProduct }); break;
          case 'reconciliation':
            setData(await reportsApi.getReconciliationExceptions('')); break;
        }
      } catch (e) { console.error(e); toast('Lỗi tải báo cáo.', 'error'); }
      setLoading(false);
    };
    load();
  }, [tab, dateRange.from, dateRange.to]);

  const handleResolve = async () => {
    if (!resolveReason.trim()) { toast('Vui lòng nhập lý do xử lý.', 'error'); return; }
    setResolving(true);
    try {
      await reportsApi.resolveReconciliationException(resolveTarget.id, {
        resolutionMethod: 'ManualAdjust',
        remarks: resolveReason.trim(),
      });
      toast('Đã xử lý thành công!', 'success');
      setResolveTarget(null);
      setResolveReason('');
      // refresh
      const fresh = await reportsApi.getReconciliationExceptions('');
      setData(fresh);
    } catch (e) {
      toast('Lỗi: ' + (e.message || 'Có lỗi xảy ra'), 'error');
    } finally { setResolving(false); }
  };

  return (
    <>
      {/* Resolve modal */}
      {resolveTarget && (
        <Modal title={`🔄 Xử lý lệch sổ — ${resolveTarget.loanAccountNo || resolveTarget.id}`}
          onClose={() => setResolveTarget(null)} width="440px">
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <Input type="textarea" label="Lý do / hướng xử lý" value={resolveReason}
              onChange={e => setResolveReason(e.target.value)} />
            <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
              <Button variant="ghost" onClick={() => setResolveTarget(null)} disabled={resolving}>Huỷ</Button>
              <Button variant="primary" onClick={handleResolve} disabled={resolving}>
                {resolving ? 'Đang xử lý…' : 'Xác nhận'}
              </Button>
            </div>
          </div>
        </Modal>
      )}

      <div className="page">
        <div className="page-header">
          <h2>📈 {t('reports.title')}</h2>
        </div>

        {/* Tab Bar */}
        <div style={{ display: 'flex', gap: '0.4rem', marginBottom: '1.5rem', borderBottom: '2px solid var(--border)', paddingBottom: '0', flexWrap: 'wrap' }}>
          {TABS.map(t => (
            <button key={t.key} onClick={() => { setTab(t.key); setData(null); }}
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

        {/* Date range filter */}
        {(tab === 'overdue' || tab === 'productivity') && (
          <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
            <Input type="date" label="Từ ngày" value={dateRange.from}
              onChange={e => setDateRange(d => ({ ...d, from: e.target.value }))} style={{ width: '180px' }} />
            <Input type="date" label="Đến ngày" value={dateRange.to}
              onChange={e => setDateRange(d => ({ ...d, to: e.target.value }))} style={{ width: '180px' }} />
          </div>
        )}

        {loading && <p style={{ color: 'var(--text-muted)', padding: '2rem 0' }}>Đang tải báo cáo…</p>}

        {/* ── Portfolio ── */}
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
            <h3 style={{ margin: '1.5rem 0 1rem' }}>Phân bổ theo sản phẩm</h3>
            <div className="table-shell">
              <table className="data-table">
                <thead><tr><th>Sản phẩm</th><th className="text-right">Số lượng</th><th className="text-right">Dư nợ</th></tr></thead>
                <tbody>
                  {data.productBreakdown?.map(p => (
                    <tr key={p.productType}><td>{p.productType}</td><td className="text-right">{p.count}</td><td className="text-right">{fmt(p.totalOutstanding)}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* ── Overdue Movement ── */}
        {tab === 'overdue' && data && (
          <div>
            <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
              <div className="kpi-card kpi-red"><div className="kpi-value">{data.newOverdueCount}</div><div className="kpi-label">Mới quá hạn</div></div>
              <div className="kpi-card kpi-green"><div className="kpi-value">{data.curedCount}</div><div className="kpi-label">Đã khắc phục</div></div>
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

        {/* ── Collection Productivity ── */}
        {tab === 'productivity' && data && (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Collector</th>
                  <th className="text-right">Phân công</th>
                  <th className="text-right">Liên hệ</th>
                  <th className="text-right">Tiếp cận</th>
                  <th className="text-right">Tỉ lệ TC</th>
                  <th className="text-right">Hẹn trả</th>
                  <th className="text-right">Giữ lời</th>
                  <th className="text-right">Tỉ lệ GL</th>
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

        {/* ── Approval Queue ── */}
        {tab === 'approval' && data && (
          <div>
            <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
              <div className="kpi-card kpi-amber"><div className="kpi-value">{data.totalPending}</div><div className="kpi-label">Đang chờ duyệt</div></div>
              <div className="kpi-card kpi-blue"><div className="kpi-value">{data.avgDaysPending}</div><div className="kpi-label">Chờ TB (ngày)</div></div>
              <div className="kpi-card kpi-green"><div className="kpi-value">{data.approvedThisMonth}</div><div className="kpi-label">Đã duyệt tháng này</div></div>
              <div className="kpi-card kpi-red"><div className="kpi-value">{data.rejectedThisMonth}</div><div className="kpi-label">Đã từ chối</div></div>
              <div className="kpi-card kpi-teal"><div className="kpi-value">{pct(data.approvalRate)}</div><div className="kpi-label">Tỉ lệ duyệt</div></div>
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

        {/* ── NPL Reports ── */}
        {tab === 'npl' && data && (
          <div>
            {data.summary && (
              <>
                <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
                  <div className="kpi-card kpi-red"><div className="kpi-value">{pct(data.summary.nplRatio)}</div><div className="kpi-label">Tỉ lệ NPL</div></div>
                  <div className="kpi-card kpi-amber"><div className="kpi-value">{fmt(data.summary.totalNplOutstanding)}</div><div className="kpi-label">Dư nợ NPL</div></div>
                  <div className="kpi-card kpi-blue"><div className="kpi-value">{pct(data.summary.provisionCoverageRatio)}</div><div className="kpi-label">Tỉ lệ dự phòng</div></div>
                  <div className="kpi-card kpi-teal"><div className="kpi-value">{data.summary.totalNplLoans}</div><div className="kpi-label">Số khoản NPL</div></div>
                </div>
                {data.summary.groupBreakdown?.length > 0 && (
                  <>
                    <h3 style={{ marginBottom: '1rem' }}>Phân nhóm nợ</h3>
                    <div className="table-shell">
                      <table className="data-table">
                        <thead><tr><th>Nhóm nợ</th><th className="text-right">Số khoản</th><th className="text-right">Dư nợ</th><th className="text-right">Tỉ lệ</th></tr></thead>
                        <tbody>
                          {data.summary.groupBreakdown.map(g => (
                            <tr key={g.group}>
                              <td><strong>Nhóm {g.group}</strong></td>
                              <td className="text-right">{g.loanCount}</td>
                              <td className="text-right">{fmt(g.outstandingAmount)}</td>
                              <td className="text-right">{pct(g.ratio)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}
              </>
            )}
            {data.byProduct?.length > 0 && (
              <>
                <h3 style={{ margin: '1.5rem 0 1rem' }}>NPL theo sản phẩm</h3>
                <div className="table-shell">
                  <table className="data-table">
                    <thead><tr><th>Sản phẩm</th><th className="text-right">Số khoản NPL</th><th className="text-right">Dư nợ NPL</th><th className="text-right">Tỉ lệ NPL</th></tr></thead>
                    <tbody>
                      {data.byProduct.map(p => (
                        <tr key={p.productName}>
                          <td>{p.productName}</td>
                          <td className="text-right">{p.nplCount}</td>
                          <td className="text-right">{fmt(p.nplOutstanding)}</td>
                          <td className="text-right">{pct(p.nplRatio)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </div>
        )}

        {/* ── Reconciliation ── */}
        {tab === 'reconciliation' && data && (
          <div>
            <div className="kpi-grid" style={{ marginBottom: '2rem' }}>
              <div className="kpi-card kpi-red"><div className="kpi-value">{data.totalCount || 0}</div><div className="kpi-label">Tổng lỗi lệch sổ</div></div>
            </div>
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Mã khoản vay</th>
                    <th>Loại lỗi</th>
                    <th className="text-right">Khoản lệch</th>
                    <th>Chi tiết</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {!data.items || data.items.length === 0 ? (
                    <tr><td colSpan={6} className="empty-row"><div className="empty-state">✅ Không có lỗi lệch sổ nào</div></td></tr>
                  ) : (
                    data.items.map(ex => (
                      <tr key={ex.id}>
                        <td>{ex.loanAccountNo || ex.loanId}</td>
                        <td><span className="code-badge">{ex.exceptionType}</span></td>
                        <td className="text-right" style={{ color: 'var(--red)', fontWeight: 700 }}>{fmt(ex.discrepancyAmount)}</td>
                        <td><span className="cell-sub">{ex.description}</span></td>
                        <td>
                          {ex.status === 'Resolved'
                            ? <span style={{ color: 'var(--green)', fontWeight: 600 }}>✅ Đã xử lý</span>
                            : <span style={{ color: 'var(--amber)', fontWeight: 600 }}>⏳ Đang chờ</span>}
                        </td>
                        <td>
                          {ex.status !== 'Resolved' && (
                            <Button variant="primary" onClick={() => setResolveTarget(ex)}
                              style={{ fontSize: '0.78rem', padding: '0.3rem 0.65rem' }}>
                              Xử lý
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
