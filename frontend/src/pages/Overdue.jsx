import { useState, useEffect, useCallback } from 'react';
import { overdueApi } from '../features/overdue/api/overdueApi';
import { loanApi } from '../features/loans/api/loanApi';
import { mapOverdueList } from '../features/overdue/mappers/overdueMapper';
import { formatCurrency, formatDate, today } from '../utils/format';
import { DpdBadge } from '../components/Badge';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import Modal from '../components/Modal';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

import { CollectionNoteModal } from '../features/overdue/components/CollectionNoteModal';

export default function Overdue() {
  const { can } = useAuth();
  const [items, setItems]       = useState([]);
  const [loading, setLoading]   = useState(true);
  const [noteId, setNoteId]     = useState(null);
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await overdueApi.list();
      const mapped = mapOverdueList(res);
      setItems(Array.isArray(mapped) ? mapped : (mapped?.items || []));
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

      <Card>
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
                    {can.addCollectionNote && <Button size="sm" variant="ghost" onClick={()=>setNoteId(item.loanId||item.id)}>📝 Ghi chú</Button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      {noteId && <CollectionNoteModal loanId={noteId} onClose={()=>setNoteId(null)} onDone={()=>{setNoteId(null);load();}}/>}
    </div>
  );
}
