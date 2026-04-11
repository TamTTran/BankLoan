import { useState, useEffect, useCallback } from 'react';
import { api } from '../api';
import { formatCurrency, formatDate, parseTenors, formatPercent, today } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import Modal from '../components/Modal';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

/* ── Create Application Modal ── */
function ApplicationModal({ onClose, onSaved }) {
  const { toast } = useToast();
  const [customers, setCustomers] = useState([]);
  const [products,  setProducts]  = useState([]);
  const [tenors,    setTenors]    = useState([]);
  const [preview,   setPreview]   = useState(null);
  const [form, setForm] = useState({ customerId:'', productId:'', requestedAmount:'', requestedTenorMonths:'', purpose:'', remarks:'' });
  const [error, setError]     = useState('');
  const [loading, setLoading] = useState(false);
  const [prevLoading, setPrevLoading] = useState(false);
  const set = (k,v) => setForm(f => ({...f,[k]:v}));

  useEffect(() => {
    Promise.all([
      api.customers.list({ pageSize:500 }),
      api.products.list(),
    ]).then(([c, p]) => {
      setCustomers(c.items || (Array.isArray(c) ? c : []));
      setProducts(Array.isArray(p) ? p : []);
    });
  }, []);

  const handleProductChange = (pid) => {
    set('productId', pid);
    const product = products.find(p => String(p.id) === pid);
    if (product) setTenors(parseTenors(product.allowedTenorsJson));
    setPreview(null);
  };

  const handlePreview = async () => {
    const { productId, requestedAmount, requestedTenorMonths } = form;
    if (!productId || !requestedAmount || !requestedTenorMonths) { setError('Chọn sản phẩm, nhập số tiền và kỳ hạn'); return; }
    setError(''); setPrevLoading(true);
    try { setPreview(await api.applications.preview({ productId:+productId, amount:+requestedAmount, tenorMonths:+requestedTenorMonths })); }
    catch(e) { setError(e.message); }
    finally { setPrevLoading(false); }
  };

  const handleSubmit = async () => {
    setError(''); setLoading(true);
    try {
      await api.applications.create({ ...form, customerId:+form.customerId, productId:+form.productId, requestedAmount:+form.requestedAmount, requestedTenorMonths:+form.requestedTenorMonths });
      toast('Tạo hồ sơ vay thành công!');
      onSaved();
    } catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal title="📋 Tạo hồ sơ vay" onClose={onClose} size="lg"
      actions={<>
        <button className="btn btn-ghost" onClick={onClose}>Hủy</button>
        <button className="btn btn-outline" onClick={handlePreview} disabled={prevLoading}>{prevLoading ? <><span className="spinner"/>Đang tính...</> : '🧮 Tính toán'}</button>
        <button className="btn btn-primary" onClick={handleSubmit} disabled={loading}>{loading ? <><span className="spinner"/>Đang tạo...</> : '📋 Tạo hồ sơ'}</button>
      </>}
    >
      <div className="form-row">
        <div className="form-group">
          <label>Khách hàng <span className="required">*</span></label>
          <select value={form.customerId} onChange={e => set('customerId', e.target.value)}>
            <option value="">-- Chọn khách hàng --</option>
            {customers.map(c => <option key={c.id} value={c.id}>{c.fullName} — {c.phone}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Sản phẩm vay <span className="required">*</span></label>
          <select value={form.productId} onChange={e => handleProductChange(e.target.value)}>
            <option value="">-- Chọn sản phẩm --</option>
            {products.map(p => <option key={p.id} value={p.id}>{p.productName} — {formatPercent(p.monthlyInterestRate)}/tháng</option>)}
          </select>
        </div>
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Số tiền vay (VNĐ) <span className="required">*</span></label>
          <input type="number" value={form.requestedAmount} onChange={e => set('requestedAmount', e.target.value)} placeholder="20000000" min="1000000" />
        </div>
        <div className="form-group">
          <label>Kỳ hạn (tháng) <span className="required">*</span></label>
          <select value={form.requestedTenorMonths} onChange={e => set('requestedTenorMonths', e.target.value)}>
            <option value="">-- Chọn kỳ hạn --</option>
            {tenors.map(t => <option key={t} value={t}>{t} tháng</option>)}
          </select>
        </div>
      </div>
      <div className="form-group">
        <label>Mục đích vay <span className="required">*</span></label>
        <input value={form.purpose} onChange={e => set('purpose', e.target.value)} placeholder="Mua xe máy, cải thiện nhà ở..." />
      </div>
      <div className="form-group">
        <label>Ghi chú</label>
        <textarea value={form.remarks} onChange={e => set('remarks', e.target.value)} rows={2} placeholder="Thông tin thêm..." />
      </div>
      {preview && (
        <div className="preview-box">
          <h4>📊 Dự kiến thanh toán</h4>
          <div className="preview-grid">
            <div><span>Trả hàng tháng</span><strong>{formatCurrency(preview.monthlyInstallment)}</strong></div>
            <div><span>Tổng thanh toán</span><strong>{formatCurrency(preview.totalRepayment)}</strong></div>
            <div><span>Tổng lãi</span><strong>{formatCurrency(preview.totalInterest)}</strong></div>
            <div><span>Phí xử lý</span><strong>{formatCurrency(preview.processingFee)}</strong></div>
          </div>
        </div>
      )}
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

/* ── Reject Modal ── */
function RejectModal({ id, onClose, onDone }) {
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const { toast } = useToast();
  const submit = async () => {
    setLoading(true);
    try { await api.applications.reject(id, { reason }); toast('Đã từ chối hồ sơ.', 'warning'); onDone(); }
    catch(e) { toast(e.message,'error'); }
    finally { setLoading(false); }
  };
  return (
    <Modal title="❌ Từ chối hồ sơ" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-danger" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'❌ Từ chối'}</button></>}>
      <div className="form-group">
        <label>Lý do từ chối <span className="required">*</span></label>
        <textarea value={reason} onChange={e=>setReason(e.target.value)} rows={3} placeholder="Nhập lý do..." />
      </div>
    </Modal>
  );
}

export default function Applications() {
  const { can } = useAuth();
  const { toast } = useToast();
  const [data, setData]       = useState(null);
  const [page, setPage]       = useState(1);
  const [search, setSearch]   = useState('');
  const [status, setStatus]   = useState('');
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [confirm, setConfirm] = useState(null);
  const [rejectId, setRejectId] = useState(null);

  const load = useCallback(async (p=1, q='', s='') => {
    setLoading(true);
    try {
      const params = { page:p, pageSize:15 };
      if (q) params.search = q;
      if (s) params.status = s;
      setData(await api.applications.list(params));
    } catch{}
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, []);

  const doSearch = () => { setPage(1); load(1, search, status); };

  const submitApp = (id) => setConfirm({
    msg: 'Nộp hồ sơ để gửi phê duyệt?',
    action: async () => { await api.applications.submit(id); toast('Hồ sơ đã được nộp!'); load(page, search, status); }
  });
  const approveApp = (id) => setConfirm({
    msg: 'Xác nhận phê duyệt hồ sơ vay này?',
    action: async () => { await api.applications.approve(id); toast('Hồ sơ đã được phê duyệt!'); load(page, search, status); }
  });
  const createLoan = (id) => setConfirm({
    msg: 'Tạo khoản vay từ hồ sơ đã duyệt?',
    action: async () => { await api.loans.createFromApp(id); toast('Khoản vay đã được tạo! Tiến hành giải ngân.'); load(page, search, status); }
  });

  const runConfirm = async () => {
    try { await confirm.action(); } catch(e) { toast(e.message,'error'); }
    setConfirm(null);
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>📋 Hồ sơ vay</h2>
        {can.createApplication && <button className="btn btn-primary" onClick={() => setShowCreate(true)}>+ Tạo hồ sơ</button>}
      </div>
      <div className="filter-bar">
        <input className="search-input" placeholder="🔍  Tìm theo khách hàng..." value={search} onChange={e=>setSearch(e.target.value)} onKeyDown={e=>e.key==='Enter'&&doSearch()} />
        <select className="select-filter" value={status} onChange={e=>{setStatus(e.target.value);setPage(1);load(1,search,e.target.value);}}>
          <option value="">Tất cả</option>
          {['Draft','Submitted','Approved','Rejected'].map(s=><option key={s} value={s}>{s}</option>)}
        </select>
        <button className="btn btn-outline" onClick={doSearch}>Tìm kiếm</button>
      </div>
      <div className="card">
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
                    {a.status==='Draft'      && can.submitApplication  && <button className="btn btn-sm btn-outline" onClick={()=>submitApp(a.id)}>📤 Nộp</button>}
                    {a.status==='Submitted'  && can.approveApplication && <button className="btn btn-sm btn-success" onClick={()=>approveApp(a.id)}>✅ Duyệt</button>}
                    {a.status==='Submitted'  && can.approveApplication && <button className="btn btn-sm btn-danger"  onClick={()=>setRejectId(a.id)}>❌ Từ chối</button>}
                    {a.status==='Approved'   && can.createLoan          && <button className="btn btn-sm btn-primary" onClick={()=>createLoan(a.id)}>💰 Tạo KV</button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Pagination paged={data} onPage={p=>{setPage(p);load(p,search,status);}} />
      </div>
      {showCreate && <ApplicationModal onClose={()=>setShowCreate(false)} onSaved={()=>{setShowCreate(false);load(page,search,status);}} />}
      {confirm    && <ConfirmDialog message={confirm.msg} onConfirm={runConfirm} onCancel={()=>setConfirm(null)} />}
      {rejectId   && <RejectModal id={rejectId} onClose={()=>setRejectId(null)} onDone={()=>{setRejectId(null);load(page,search,status);}} />}
    </div>
  );
}
