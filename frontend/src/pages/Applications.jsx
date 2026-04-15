import { useState } from 'react';
import { applicationApi } from '../features/applications/api/applicationApi';
import { loanApi } from '../features/loans/api/loanApi';
import { formatCurrency, formatDate } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { ApplicationFormModal } from '../features/applications/components/ApplicationFormModal';
import { RejectApplicationModal } from '../features/applications/components/RejectApplicationModal';
import { useApplications } from '../features/applications/hooks/useApplications';

export default function Applications() {
  const { can } = useAuth();
  const { toast } = useToast();
  
  const { data, page, setPage, search, setSearch, status, setStatus, loading, doSearch, refresh } = useApplications();
  
  const [showCreate, setShowCreate] = useState(false);
  const [confirm, setConfirm] = useState(null);
  const [rejectId, setRejectId] = useState(null);

  const submitApp = (id) => setConfirm({
    msg: 'Nộp hồ sơ để gửi phê duyệt?',
    action: async () => { await applicationApi.submit(id); toast('Hồ sơ đã được nộp!'); refresh(); }
  });
  
  const approveApp = (id) => setConfirm({
    msg: 'Xác nhận phê duyệt hồ sơ vay này?',
    action: async () => { await applicationApi.approve(id); toast('Hồ sơ đã được phê duyệt!'); refresh(); }
  });
  
  const createLoan = (id) => setConfirm({
    msg: 'Tạo khoản vay từ hồ sơ đã duyệt?',
    action: async () => { await loanApi.createFromApp(id); toast('Khoản vay đã được tạo! Tiến hành giải ngân.'); refresh(); }
  });

  const runConfirm = async () => {
    try { await confirm.action(); } catch(e) { toast(e.message,'error'); }
    setConfirm(null);
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>📋 Hồ sơ vay</h2>
        {can.createApplication && <Button variant="primary" onClick={() => setShowCreate(true)}>+ Tạo hồ sơ</Button>}
      </div>
      <div className="filter-bar">
        <div style={{ flex: 1, minWidth: '200px' }}>
          <Input placeholder="🔍 Tìm theo khách hàng..." value={search} onChange={e=>setSearch(e.target.value)} onKeyDown={e=>e.key==='Enter'&&doSearch()} />
        </div>
        <div style={{ width: '180px' }}>
          <Input type="select" value={status} onChange={e=>{setStatus(e.target.value);setPage(1); refresh();}}>
            <option value="">Tất cả trạng thái</option>
            {['Draft','Submitted','Approved','Rejected'].map(s=><option key={s} value={s}>{s}</option>)}
          </Input>
        </div>
        <Button variant="outline" onClick={doSearch}>Tìm kiếm</Button>
      </div>
      <Card>
        <table className="data-table">
          <thead><tr><th>Số HĐ</th><th>Khách hàng</th><th>Sản phẩm</th><th>Số tiền</th><th>Kỳ hạn</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th></tr></thead>
          <tbody>
            {loading ? <tr><td colSpan={8} className="empty-row"><div className="loading-shimmer"/></td></tr>
            : !data?.items?.length ? <tr><td colSpan={8} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Chưa có hồ sơ vay</p></div></td></tr>
            : data.items.map(a => (
              <tr key={a.id}>
                <td><span className="code-badge">APP-{String(a.id).padStart(4,'0')}</span></td>
                <td><div className="cell-main">{a.customer?.fullName||'—'}</div><div className="cell-sub">{a.customer?.phone}</div></td>
                <td><span className="cell-sub">{a.product?.productName||'—'}</span></td>
                <td><strong>{formatCurrency(a.requestedAmount)}</strong></td>
                <td>{a.requestedTenorMonths} tháng</td>
                <td><StatusBadge status={a.status}/></td>
                <td><span className="cell-sub">{formatDate(a.createdAt)}</span></td>
                <td>
                  <div className="action-btns">
                    {a.status==='Draft'      && can.submitApplication  && <Button size="sm" variant="outline" onClick={()=>submitApp(a.id)}>📤 Nộp</Button>}
                    {a.status==='Submitted'  && can.approveApplication && <Button size="sm" variant="primary" onClick={()=>approveApp(a.id)}>✅ Duyệt</Button>}
                    {a.status==='Submitted'  && can.approveApplication && <Button size="sm" variant="danger"  onClick={()=>setRejectId(a.id)}>❌ Từ chối</Button>}
                    {a.status==='Approved'   && can.createLoan          && <Button size="sm" variant="primary" onClick={()=>createLoan(a.id)}>💰 Tạo KV</Button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Pagination paged={data} onPage={p=>{setPage(p); refresh();}} />
      </Card>
      {showCreate && <ApplicationFormModal onClose={()=>setShowCreate(false)} onSaved={()=>{setShowCreate(false); refresh();}} />}
      {confirm    && <ConfirmDialog message={confirm.msg} onConfirm={runConfirm} onCancel={()=>setConfirm(null)} />}
      {rejectId   && <RejectApplicationModal id={rejectId} onClose={()=>setRejectId(null)} onDone={()=>{setRejectId(null); refresh();}} />}
    </div>
  );
}
