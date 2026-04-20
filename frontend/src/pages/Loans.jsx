import { useState } from 'react';
import { formatCurrency, formatDate, formatPercent } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import Pagination from '../components/Pagination';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { LoanDetailModal } from '../features/loans/components/LoanDetailModal';
import { DisburseModal } from '../features/loans/components/DisburseModal';
import { PaymentModal } from '../features/loans/components/PaymentModal';
import { EContractModal } from '../features/loans/components/EContractModal';
import { useLoans } from '../features/loans/hooks/useLoans';

export default function Loans() {
  const { can } = useAuth();
  
  const { data, page, setPage, search, setSearch, status, setStatus, loading, doSearch, refresh } = useLoans();

  const [detailId, setDetailId]   = useState(null);
  const [disburseId, setDisburseId] = useState(null);
  const [paymentId, setPaymentId]   = useState(null);
  const [contractId, setContractId] = useState(null);

  const handleTestWebhook = async (vaNumber) => {
    try {
      const amount = prompt("Nhập số tiền chuyển khoản giả lập cho " + vaNumber + ":", "1000000");
      if (!amount) return;
      const res = await fetch('/api/webhooks/bank-transfer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          virtualAccountNo: vaNumber,
          amount: parseFloat(amount),
          transactionId: "MOCK-" + Date.now(),
          description: "Khach hang thanh toan"
        })
      });
      const data = await res.json();
      if (data.success) {
        toast('Đã ghi nhận thanh toán qua Webhook!', 'success');
        refresh();
      } else {
        toast('Lỗi: ' + data.message, 'error');
      }
    } catch(e) {
      toast('Lỗi Webhook: ' + e.message, 'error');
    }
  };

  return (
    <div className="page">
      <div className="page-header"><h2>💰 Khoản vay</h2></div>
      <div className="filter-bar">
        <div style={{ flex: 1, minWidth: '200px' }}>
          <Input placeholder="🔍 Tìm theo mã KV, khách hàng..." value={search} onChange={e=>setSearch(e.target.value)} onKeyDown={e=>e.key==='Enter'&&doSearch()}/>
        </div>
        <div style={{ width: '180px' }}>
          <Input type="select" value={status} onChange={e=>{setStatus(e.target.value);setPage(1);refresh();}}>
            <option value="">Tất cả trạng thái</option>
            {['PendingDisbursement','Active','Overdue','Closed'].map(s=><option key={s} value={s}>{s}</option>)}
          </Input>
        </div>
        <Button variant="outline" onClick={doSearch}>Tìm kiếm</Button>
      </div>
      <Card>
        <table className="data-table">
          <thead><tr><th>Mã KV</th><th>Khách hàng</th><th>Sản phẩm</th><th>Số tiền vay</th><th>Dư nợ</th><th>Lãi suất</th><th>Đến hạn</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            {loading ? <tr><td colSpan={9} className="empty-row"><div className="loading-shimmer"/></td></tr>
            : !data?.items?.length ? <tr><td colSpan={9} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Chưa có khoản vay</p></div></td></tr>
            : data.items.map(l => {
              const outstanding = l.outstandingAmount ?? l.approvedAmount;
              const pct = l.approvedAmount ? Math.round((1-outstanding/l.approvedAmount)*100) : 0;
              return (
                <tr key={l.id}>
                  <td>
                    <span className="code-badge">{l.loanAccountNo}</span>
                    {l.virtualAccountNo && <div className="cell-sub mt-1" style={{fontSize: '0.75rem', color: 'var(--primary)'}}>VA: {l.virtualAccountNo}</div>}
                  </td>
                  <td><div className="cell-main">{l.customer?.fullName||'—'}</div><div className="cell-sub">{l.customer?.phone}</div></td>
                  <td><span className="cell-sub">{l.product?.productName}</span></td>
                  <td>{formatCurrency(l.approvedAmount)}</td>
                  <td>
                    <strong style={{color:l.status==='Overdue'?'var(--red)':l.status==='Active'?'var(--amber)':'var(--text-secondary)'}}>{formatCurrency(outstanding)}</strong>
                    <div className="progress-bar"><div className="progress-fill" style={{width:`${pct}%`}}/></div>
                  </td>
                  <td><span className="cell-sub">{formatPercent(l.monthlyInterestRate)}/T</span></td>
                  <td><span className="cell-sub">{formatDate(l.maturityDate)}</span></td>
                  <td><StatusBadge status={l.status}/></td>
                  <td>
                    <div className="action-btns">
                      <Button size="sm" variant="outline" onClick={()=>setDetailId(l.id)}>📋 Chi tiết</Button>
                      {l.status==='PendingDisbursement' && can.disburse     && <Button size="sm" variant="outline" onClick={()=>setContractId(l.id)}>✍️ Ký HĐ</Button>}
                      {l.status==='PendingDisbursement' && can.disburse     && <Button size="sm" variant="primary" onClick={()=>setDisburseId(l.id)}>💵 Giải ngân</Button>}
                      {l.status==='Active'              && can.recordPayment && <Button size="sm" variant="success" onClick={()=>setPaymentId(l.id)}>💰 Thu tiền</Button>}
                      {(l.status==='Active' || l.status==='Overdue') && l.virtualAccountNo && (
                         <Button size="sm" variant="outline" onClick={()=>handleTestWebhook(l.virtualAccountNo)} title="Giả lập KH chuyển khoản qua Webhook" style={{borderColor: 'var(--green)', color: 'var(--green)'}}>🏦 Test Webhook</Button>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <Pagination paged={data} onPage={p=>{setPage(p); refresh();}}/>
      </Card>

      {detailId   && <LoanDetailModal  loanId={detailId}   onClose={()=>setDetailId(null)}/>}
      {contractId && <EContractModal   loanId={contractId} onClose={()=>setContractId(null)} onDone={()=>{setContractId(null);refresh();}}/>}
      {disburseId && <DisburseModal    loanId={disburseId} onClose={()=>setDisburseId(null)} onDone={()=>{setDisburseId(null);refresh();}}/>}
      {paymentId  && <PaymentModal     loanId={paymentId}  onClose={()=>setPaymentId(null)}  onDone={()=>{setPaymentId(null);refresh();}}/>}
    </div>
  );
}
