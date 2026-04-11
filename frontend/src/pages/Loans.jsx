import { useState, useEffect, useCallback } from 'react';
import { api } from '../api';
import { formatCurrency, formatDate, formatPercent, today } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import Modal from '../components/Modal';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

/* ── Loan Detail Modal (tabs: schedule + payments) ── */
function LoanDetailModal({ loanId, onClose }) {
  const [loan, setLoan]         = useState(null);
  const [schedule, setSchedule] = useState([]);
  const [payments, setPayments] = useState([]);
  const [tab, setTab]           = useState('schedule');
  const [loading, setLoading]   = useState(true);

  useEffect(() => {
    Promise.all([api.loans.get(loanId), api.loans.schedule(loanId), api.loans.payments(loanId)])
      .then(([l, s, p]) => {
        setLoan(l);
        setSchedule(Array.isArray(s) ? s : s?.schedules || []);
        setPayments(Array.isArray(p) ? p : p?.payments || []);
      }).catch(console.error).finally(() => setLoading(false));
  }, [loanId]);

  const outstanding = loan?.outstandingAmount ?? loan?.approvedAmount;
  const progress    = loan?.approvedAmount ? Math.round((1 - outstanding/loan.approvedAmount)*100) : 0;

  return (
    <Modal title={`💰 Chi tiết khoản vay — ${loan?.loanAccountNo || '...'}`} onClose={onClose} size="xl"
      actions={<button className="btn btn-ghost" onClick={onClose}>Đóng</button>}>
      {loading ? <div className="loading-shimmer" style={{height:200}}/> : (<>
        {/* Summary */}
        <div className="detail-grid">
          {[
            ['Khách hàng',   loan?.customer?.fullName],
            ['Sản phẩm',     loan?.product?.productName],
            ['Trạng thái',   <StatusBadge key="s" status={loan?.status}/>],
            ['Số tiền vay',  formatCurrency(loan?.approvedAmount)],
            ['Dư nợ còn lại',<span key="o" style={{color:'var(--amber)',fontWeight:700}}>{formatCurrency(outstanding)}</span>],
            ['Lãi suất',     formatPercent(loan?.monthlyInterestRate)+'/tháng'],
            ['Kỳ hạn',       loan?.tenorMonths+' tháng'],
            ['Giải ngân',    formatDate(loan?.disbursementDate)],
            ['Đáo hạn',      formatDate(loan?.maturityDate)],
          ].map(([label, val]) => (
            <div key={label} className="detail-item">
              <div className="detail-label">{label}</div>
              <div className="detail-value">{val || '—'}</div>
            </div>
          ))}
        </div>
        <div style={{marginBottom:'1.5rem'}}>
          <div style={{fontSize:'0.75rem',color:'var(--text-muted)',marginBottom:'0.4rem'}}>Tiến độ trả nợ: {progress}%</div>
          <div className="progress-bar" style={{height:8}}><div className="progress-fill" style={{width:`${progress}%`}}/></div>
        </div>
      </>)}

      {/* Tabs */}
      <div className="modal-tabs" style={{marginTop:'-1.5rem'}}>
        {[['schedule','📅 Lịch trả nợ'],['payments','💳 Lịch sử TT']].map(([k,l])=>(
          <div key={k} className={`modal-tab${tab===k?' active':''}`} onClick={()=>setTab(k)}>{l}</div>
        ))}
      </div>

      {tab === 'schedule' && (
        <div className="modal-tab-content" style={{padding:0}}>
          <table className="data-table">
            <thead><tr><th>Kỳ</th><th>Đến hạn</th><th>Gốc</th><th>Lãi</th><th>Tổng</th><th>Còn lại</th><th>Trạng thái</th></tr></thead>
            <tbody>
              {!schedule.length ? <tr><td colSpan={7} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Chưa có lịch</p></div></td></tr>
              : schedule.map(s=>(
                <tr key={s.installmentNo} className={s.status==='Overdue'?'row-overdue':s.status==='Paid'?'row-paid':''}>
                  <td className="text-center">{s.installmentNo}</td>
                  <td>{formatDate(s.dueDate)}</td>
                  <td>{formatCurrency(s.principalDue)}</td>
                  <td>{formatCurrency(s.interestDue)}</td>
                  <td><strong>{formatCurrency(s.principalDue+s.interestDue)}</strong></td>
                  <td>{formatCurrency(s.remainingDueAmount)}</td>
                  <td><StatusBadge status={s.status}/></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {tab === 'payments' && (
        <div className="modal-tab-content" style={{padding:0}}>
          <table className="data-table">
            <thead><tr><th>Ngày thu</th><th>Số tiền</th><th>Phương thức</th><th>Ghi chú</th></tr></thead>
            <tbody>
              {!payments.length ? <tr><td colSpan={4} className="empty-row"><div className="empty-state"><span className="empty-icon">💳</span><p>Chưa có thanh toán</p></div></td></tr>
              : payments.map((p,i)=>(
                <tr key={i}>
                  <td>{formatDate(p.paymentDate)}</td>
                  <td><strong>{formatCurrency(p.amount)}</strong></td>
                  <td>{p.paymentMethod==='Cash'?'Tiền mặt':'Chuyển khoản'}</td>
                  <td><span className="cell-sub">{p.remarks||'—'}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}

/* ── Disburse Modal ── */
function DisburseModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [date, setDate]       = useState(today());
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');
  const submit = async () => {
    setError(''); setLoading(true);
    try { await api.loans.disburse(loanId,{disbursementDate:date}); toast('Giải ngân thành công!'); onDone(); }
    catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };
  return (
    <Modal title="💵 Xác nhận giải ngân" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-primary" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'💵 Giải ngân'}</button></>}>
      <div className="form-group"><label>Ngày giải ngân <span className="required">*</span></label><input type="date" value={date} onChange={e=>setDate(e.target.value)}/></div>
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

/* ── Payment Modal ── */
function PaymentModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [form, setForm] = useState({ amount:'', paymentDate:today(), paymentMethod:'Cash', remarks:'' });
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');
  const set = (k,v) => setForm(f=>({...f,[k]:v}));
  const submit = async () => {
    setError(''); setLoading(true);
    try { await api.loans.pay(loanId,{...form,amount:+form.amount}); toast('Ghi nhận thanh toán thành công!'); onDone(); }
    catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };
  return (
    <Modal title="💰 Thu tiền trả nợ" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-success" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'✅ Ghi nhận'}</button></>}>
      <div className="form-row">
        <div className="form-group"><label>Số tiền (VNĐ) <span className="required">*</span></label><input type="number" value={form.amount} onChange={e=>set('amount',e.target.value)} placeholder="0"/></div>
        <div className="form-group"><label>Ngày thu <span className="required">*</span></label><input type="date" value={form.paymentDate} onChange={e=>set('paymentDate',e.target.value)}/></div>
      </div>
      <div className="form-group">
        <label>Phương thức</label>
        <select value={form.paymentMethod} onChange={e=>set('paymentMethod',e.target.value)}>
          <option value="Cash">Tiền mặt</option>
          <option value="BankTransfer">Chuyển khoản</option>
        </select>
      </div>
      <div className="form-group"><label>Ghi chú</label><input value={form.remarks} onChange={e=>set('remarks',e.target.value)} placeholder="Số GD, ghi chú..."/></div>
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

/* ── Main Loans Page ── */
export default function Loans() {
  const { can } = useAuth();
  const { toast } = useToast();
  const [data, setData]       = useState(null);
  const [page, setPage]       = useState(1);
  const [search, setSearch]   = useState('');
  const [status, setStatus]   = useState('');
  const [loading, setLoading] = useState(true);
  const [detailId, setDetailId]   = useState(null);
  const [disburseId, setDisburseId] = useState(null);
  const [paymentId, setPaymentId]   = useState(null);

  const load = useCallback(async (p=1,q='',s='') => {
    setLoading(true);
    try {
      const params={page:p,pageSize:15};
      if(q) params.search=q; if(s) params.status=s;
      setData(await api.loans.list(params));
    } catch{}
    finally{setLoading(false);}
  },[]);

  useEffect(()=>{load();},[]);
  const doSearch = () => { setPage(1); load(1,search,status); };
  const refresh  = () => load(page,search,status);

  return (
    <div className="page">
      <div className="page-header"><h2>💰 Khoản vay</h2></div>
      <div className="filter-bar">
        <input className="search-input" placeholder="🔍  Tìm theo mã KV, khách hàng..." value={search} onChange={e=>setSearch(e.target.value)} onKeyDown={e=>e.key==='Enter'&&doSearch()}/>
        <select className="select-filter" value={status} onChange={e=>{setStatus(e.target.value);setPage(1);load(1,search,e.target.value);}}>
          <option value="">Tất cả</option>
          {['PendingDisbursement','Active','Overdue','Closed'].map(s=><option key={s} value={s}>{s}</option>)}
        </select>
        <button className="btn btn-outline" onClick={doSearch}>Tìm kiếm</button>
      </div>
      <div className="card">
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
                  <td><span className="code-badge">{l.loanAccountNo}</span></td>
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
                      <button className="btn btn-sm btn-outline" onClick={()=>setDetailId(l.id)}>📋 Chi tiết</button>
                      {l.status==='PendingDisbursement' && can.disburse     && <button className="btn btn-sm btn-primary" onClick={()=>setDisburseId(l.id)}>💵 Giải ngân</button>}
                      {l.status==='Active'              && can.recordPayment && <button className="btn btn-sm btn-success" onClick={()=>setPaymentId(l.id)}>💰 Thu tiền</button>}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <Pagination paged={data} onPage={p=>{setPage(p);load(p,search,status);}}/>
      </div>

      {detailId   && <LoanDetailModal  loanId={detailId}   onClose={()=>setDetailId(null)}/>}
      {disburseId && <DisburseModal    loanId={disburseId} onClose={()=>setDisburseId(null)} onDone={()=>{setDisburseId(null);refresh();}}/>}
      {paymentId  && <PaymentModal     loanId={paymentId}  onClose={()=>setPaymentId(null)}  onDone={()=>{setPaymentId(null);refresh();}}/>}
    </div>
  );
}
