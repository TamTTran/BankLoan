import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import { collectionsApi } from '../features/collections/api/collectionsApi';

const ACTION_TYPES = ['Call', 'Visit', 'SMS', 'Letter', 'Legal'];
const OUTCOMES = [
  { value: 'Contacted', label: 'Đã liên hệ' },
  { value: 'NoAnswer', label: 'Không nghe máy' },
  { value: 'PromiseToPay', label: 'Hẹn trả nợ' },
  { value: 'Refused', label: 'Từ chối' },
  { value: 'Escalated', label: 'Chuyển cấp trên' },
];

const PRIORITY_COLORS = { High: '#ef4444', Medium: '#f59e0b', Low: '#22c55e' };
const PRIORITY_LABELS = { High: 'Cao', Medium: 'Trung bình', Low: 'Thấp' };

export default function Collections() {
  const { user, can } = useAuth();
  const [queue, setQueue] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedLoan, setSelectedLoan] = useState(null);
  const [history, setHistory] = useState([]);
  const [showActionModal, setShowActionModal] = useState(false);
  const [showHistoryModal, setShowHistoryModal] = useState(false);

  // Action form
  const [actionForm, setActionForm] = useState({
    actionType: 'Call', actionDate: new Date().toISOString().split('T')[0],
    outcome: 'Contacted', nextFollowUpDate: '', promiseDate: '', promiseAmount: '', notes: ''
  });

  const isManagerView = can.viewAllCollections;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = isManagerView
        ? await collectionsApi.getAllQueue()
        : await collectionsApi.getMyQueue();
      setQueue(data);

      if (isManagerView) {
        const s = await collectionsApi.getSummary();
        setSummary(s);
      }
    } catch (e) { console.error(e); }
    setLoading(false);
  }, [isManagerView]);

  useEffect(() => { load(); }, [load]);

  const openActionModal = (loan) => {
    setSelectedLoan(loan);
    setActionForm({
      actionType: 'Call', actionDate: new Date().toISOString().split('T')[0],
      outcome: 'Contacted', nextFollowUpDate: '', promiseDate: '', promiseAmount: '', notes: ''
    });
    setShowActionModal(true);
  };

  const openHistory = async (loan) => {
    setSelectedLoan(loan);
    setShowHistoryModal(true);
    const data = await collectionsApi.getHistory(loan.loanId);
    setHistory(data);
  };

  const submitAction = async (e) => {
    e.preventDefault();
    try {
      const payload = {
        actionType: actionForm.actionType,
        actionDate: actionForm.actionDate,
        outcome: actionForm.outcome,
        nextFollowUpDate: actionForm.nextFollowUpDate || null,
        notes: actionForm.notes || null,
      };
      if (actionForm.outcome === 'PromiseToPay') {
        payload.promiseDate = actionForm.promiseDate;
        payload.promiseAmount = parseFloat(actionForm.promiseAmount);
      }
      await collectionsApi.addAction(selectedLoan.loanId, payload);
      setShowActionModal(false);
      load();
    } catch (err) { alert(err.message || 'Lỗi'); }
  };

  const fmt = (n) => Number(n || 0).toLocaleString('vi-VN');

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>🔔 Thu hồi nợ</h1>
        <p className="page-subtitle">
          {isManagerView ? 'Quản lý tất cả khoản vay quá hạn' : 'Danh sách khoản vay được giao'}
        </p>
      </div>

      {/* Summary Cards (Manager/Admin only) */}
      {summary && (
        <div className="stats-grid" style={{ marginBottom: '2rem' }}>
          <div className="stat-card"><div className="stat-value">{summary.totalOverdueLoans}</div><div className="stat-label">Tổng quá hạn</div></div>
          <div className="stat-card"><div className="stat-value">{summary.totalAssigned}</div><div className="stat-label">Đã phân công</div></div>
          <div className="stat-card"><div className="stat-value">{summary.totalUnassigned}</div><div className="stat-label">Chưa phân công</div></div>
          <div className="stat-card"><div className="stat-value">{summary.actionsToday}</div><div className="stat-label">Liên hệ hôm nay</div></div>
          <div className="stat-card"><div className="stat-value">{summary.promisesPending}</div><div className="stat-label">Hẹn trả chờ</div></div>
          <div className="stat-card" style={{ borderColor: '#ef4444' }}><div className="stat-value" style={{ color: '#ef4444' }}>{summary.promisesBroken}</div><div className="stat-label">Hẹn trả thất bại</div></div>
        </div>
      )}

      {/* Queue Table */}
      {loading ? <p>Đang tải...</p> : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Mã khoản vay</th>
                <th>Khách hàng</th>
                <th>SĐT</th>
                <th>Dư nợ quá hạn</th>
                <th>DPD</th>
                <th>Ưu tiên</th>
                <th>Collector</th>
                <th>Kết quả cuối</th>
                <th>Hẹn trả</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {queue.map(item => (
                <tr key={item.loanId}>
                  <td><strong>{item.loanAccountNo}</strong></td>
                  <td>{item.customerName}</td>
                  <td>{item.customerPhone}</td>
                  <td className="text-right">{fmt(item.overdueAmount)}</td>
                  <td><span className="badge" style={{ backgroundColor: item.dpd > 30 ? '#ef4444' : item.dpd > 7 ? '#f59e0b' : '#3b82f6' }}>{item.dpd} ngày</span></td>
                  <td><span style={{ color: PRIORITY_COLORS[item.priority], fontWeight: 600 }}>{PRIORITY_LABELS[item.priority]}</span></td>
                  <td>{item.assignedCollector || <em style={{ opacity: 0.5 }}>Chưa phân</em>}</td>
                  <td>{OUTCOMES.find(o => o.value === item.lastActionOutcome)?.label || '—'}</td>
                  <td>
                    {item.lastPromiseDate && (
                      <span className={`badge ${item.lastPromiseStatus === 'Broken' ? 'badge-danger' : item.lastPromiseStatus === 'Kept' ? 'badge-success' : 'badge-warning'}`}>
                        {item.lastPromiseDate} ({item.lastPromiseStatus === 'Broken' ? 'Thất bại' : item.lastPromiseStatus === 'Kept' ? 'Đã trả' : 'Chờ'})
                      </span>
                    )}
                    {!item.lastPromiseDate && '—'}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                      {can.logCollectionAction && (
                        <button className="btn btn-sm btn-primary" onClick={() => openActionModal(item)}>📞 Ghi nhận</button>
                      )}
                      <button className="btn btn-sm btn-ghost" onClick={() => openHistory(item)}>📋 Lịch sử</button>
                    </div>
                  </td>
                </tr>
              ))}
              {queue.length === 0 && <tr><td colSpan="10" style={{ textAlign: 'center', padding: '2rem' }}>Không có khoản vay quá hạn nào.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {/* Action Modal */}
      {showActionModal && (
        <div className="modal-backdrop" onClick={() => setShowActionModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '540px' }}>
            <div className="modal-header">
              <h3>📞 Ghi nhận liên hệ — {selectedLoan.loanAccountNo}</h3>
              <button className="btn-close" onClick={() => setShowActionModal(false)}>×</button>
            </div>
            <form onSubmit={submitAction}>
              <div className="modal-body">
                <div className="form-group">
                  <label>Hình thức liên hệ</label>
                  <select value={actionForm.actionType} onChange={e => setActionForm(f => ({ ...f, actionType: e.target.value }))}>
                    {ACTION_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Ngày liên hệ</label>
                  <input type="date" value={actionForm.actionDate} onChange={e => setActionForm(f => ({ ...f, actionDate: e.target.value }))} required />
                </div>
                <div className="form-group">
                  <label>Kết quả</label>
                  <select value={actionForm.outcome} onChange={e => setActionForm(f => ({ ...f, outcome: e.target.value }))}>
                    {OUTCOMES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                </div>

                {actionForm.outcome === 'PromiseToPay' && (
                  <>
                    <div className="form-group">
                      <label>Ngày hẹn trả</label>
                      <input type="date" value={actionForm.promiseDate} onChange={e => setActionForm(f => ({ ...f, promiseDate: e.target.value }))} required />
                    </div>
                    <div className="form-group">
                      <label>Số tiền hẹn trả</label>
                      <input type="number" value={actionForm.promiseAmount} onChange={e => setActionForm(f => ({ ...f, promiseAmount: e.target.value }))} required />
                    </div>
                  </>
                )}

                <div className="form-group">
                  <label>Ngày follow-up tiếp</label>
                  <input type="date" value={actionForm.nextFollowUpDate} onChange={e => setActionForm(f => ({ ...f, nextFollowUpDate: e.target.value }))} />
                </div>
                <div className="form-group">
                  <label>Ghi chú</label>
                  <textarea rows="3" value={actionForm.notes} onChange={e => setActionForm(f => ({ ...f, notes: e.target.value }))} placeholder="Ghi chú chi tiết..." />
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-ghost" onClick={() => setShowActionModal(false)}>Hủy</button>
                <button type="submit" className="btn btn-primary">Lưu</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* History Modal */}
      {showHistoryModal && (
        <div className="modal-backdrop" onClick={() => setShowHistoryModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '680px' }}>
            <div className="modal-header">
              <h3>📋 Lịch sử thu hồi — {selectedLoan?.loanAccountNo}</h3>
              <button className="btn-close" onClick={() => setShowHistoryModal(false)}>×</button>
            </div>
            <div className="modal-body" style={{ maxHeight: '500px', overflowY: 'auto' }}>
              {history.length === 0 ? <p style={{ textAlign: 'center', opacity: 0.6 }}>Chưa có lịch sử liên hệ.</p> : (
                <div className="timeline">
                  {history.map(h => (
                    <div key={h.id} className="timeline-item" style={{ borderLeft: '3px solid var(--border-color)', paddingLeft: '1rem', marginBottom: '1.5rem' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.25rem' }}>
                        <strong>{h.actionType} — {OUTCOMES.find(o => o.value === h.outcome)?.label || h.outcome}</strong>
                        <small style={{ opacity: 0.6 }}>{h.actionDate}</small>
                      </div>
                      <div style={{ fontSize: '0.85rem', opacity: 0.7 }}>Collector: {h.collectorName}</div>
                      {h.promiseDate && (
                        <div style={{ fontSize: '0.85rem', marginTop: '0.25rem' }}>
                          🤝 Hẹn trả: {h.promiseDate} — {fmt(h.promiseAmount)} —
                          <span style={{ color: h.promiseStatus === 'Broken' ? '#ef4444' : h.promiseStatus === 'Kept' ? '#22c55e' : '#f59e0b', fontWeight: 600 }}>
                            {' '}{h.promiseStatus === 'Broken' ? 'Thất bại' : h.promiseStatus === 'Kept' ? 'Đã trả' : 'Chờ'}
                          </span>
                        </div>
                      )}
                      {h.notes && <div style={{ fontSize: '0.85rem', marginTop: '0.25rem', fontStyle: 'italic' }}>"{h.notes}"</div>}
                      {h.nextFollowUpDate && <div style={{ fontSize: '0.8rem', marginTop: '0.25rem' }}>📅 Follow-up: {h.nextFollowUpDate}</div>}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
