import { useState, useEffect, useCallback } from 'react';
import { api } from '../api';
import { formatCurrency, formatDate, today } from '../utils/format';
import { DpdBadge } from '../components/Badge';
import Modal from '../components/Modal';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

function CollectionNoteModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [noteText, setNoteText] = useState('');
  const [noteDate, setNoteDate] = useState(today());
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState('');

  const submit = async () => {
    if (!noteText.trim()) { setError('Vui lòng nhập nội dung ghi chú'); return; }
    setError(''); setLoading(true);
    try { await api.loans.addNote(loanId, { noteText, noteDate }); toast('Đã thêm ghi chú thu hồi!'); onDone(); }
    catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal title="📝 Ghi chú thu hồi" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-primary" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'📝 Lưu'}</button></>}>
      <div className="form-group"><label>Ngày ghi chú <span className="required">*</span></label><input type="date" value={noteDate} onChange={e=>setNoteDate(e.target.value)}/></div>
      <div className="form-group">
        <label>Nội dung <span className="required">*</span></label>
        <textarea value={noteText} onChange={e=>setNoteText(e.target.value)} rows={4} placeholder="Khách hàng hứa thanh toán vào... / Đã liên hệ qua sđt... / Gặp trực tiếp tại..."/>
      </div>
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

export default function Overdue() {
  const { can } = useAuth();
  const [items, setItems]       = useState([]);
  const [loading, setLoading]   = useState(true);
  const [noteId, setNoteId]     = useState(null);
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.loans.overdue();
      setItems(Array.isArray(res) ? res : (res?.items || []));
    } catch(e) { toast(e.message,'error'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, []);

  // Bucket summary
  const bucketCounts = items.reduce((acc, i) => { acc[i.bucket] = (acc[i.bucket]||0)+1; return acc; }, {});

  return (
    <div className="page">
      <div className="page-header"><h2>⚠️ Quản lý quá hạn</h2></div>

      {/* Bucket summary tags */}
      {Object.keys(bucketCounts).length > 0 && (
        <div className="overdue-summary">
          {Object.entries(bucketCounts).map(([k,v])=>(
            <div key={k} className="summary-tag">
              <DpdBadge bucket={k}/> <strong>{v} khoản</strong>
            </div>
          ))}
        </div>
      )}

      <div className="card">
        <table className="data-table">
          <thead><tr><th>Mã KV</th><th>Khách hàng</th><th>Dư nợ</th><th>Số tiền QH</th><th>Số ngày QH</th><th>DPD</th><th>Ghi chú gần nhất</th><th>Thao tác</th></tr></thead>
          <tbody>
            {loading ? <tr><td colSpan={8} className="empty-row"><div className="loading-shimmer"/></td></tr>
            : !items.length ? <tr><td colSpan={8} className="empty-row"><div className="empty-state"><span className="empty-icon">🎉</span><p>Không có khoản vay quá hạn</p></div></td></tr>
            : items.map(item => (
              <tr key={item.loanId||item.id}>
                <td><span className="code-badge">{item.loanAccountNo}</span></td>
                <td><div className="cell-main">{item.customerName||item.customer?.fullName||'—'}</div><div className="cell-sub">{item.customerPhone||item.customer?.phone}</div></td>
                <td>{formatCurrency(item.outstandingAmount??item.outstandingBalance)}</td>
                <td style={{color:'var(--red)',fontWeight:600}}>{formatCurrency(item.overdueAmount)}</td>
                <td style={{color:'var(--red)',fontWeight:700}}>{item.daysOverdue??'—'} ngày</td>
                <td><DpdBadge bucket={item.bucket}/></td>
                <td><span className="cell-sub">{item.lastCollectionNote||'—'}</span></td>
                <td>
                  <div className="action-btns">
                    {can.addCollectionNote && <button className="btn btn-sm btn-ghost" onClick={()=>setNoteId(item.loanId||item.id)}>📝 Ghi chú</button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {noteId && <CollectionNoteModal loanId={noteId} onClose={()=>setNoteId(null)} onDone={()=>{setNoteId(null);load();}}/>}
    </div>
  );
}
