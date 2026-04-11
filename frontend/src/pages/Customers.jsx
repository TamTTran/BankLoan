import { useState, useEffect, useCallback } from 'react';
import { api } from '../api';
import { formatDate, today } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import Modal from '../components/Modal';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

function CustomerModal({ customer, onClose, onSaved }) {
  const { toast } = useToast();
  const [form, setForm] = useState({
    fullName: customer?.fullName || '', idNumber: customer?.idNumber || '',
    phone: customer?.phone || '', dateOfBirth: customer?.dateOfBirth?.substring(0,10) || '',
    address: customer?.address || '',
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const set = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = async () => {
    setError(''); setLoading(true);
    try {
      if (customer) await api.customers.update(customer.id, form);
      else          await api.customers.create(form);
      toast(customer ? 'Cập nhật thành công!' : 'Tạo khách hàng thành công!');
      onSaved();
    } catch (e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal
      title={customer ? '✏️ Cập nhật khách hàng' : '👤 Thêm khách hàng mới'}
      onClose={onClose}
      actions={<>
        <button className="btn btn-ghost" onClick={onClose}>Hủy</button>
        <button className="btn btn-primary" onClick={handleSubmit} disabled={loading}>
          {loading ? <><span className="spinner" /> Đang lưu...</> : '💾 Lưu'}
        </button>
      </>}
    >
      <div className="form-row">
        <div className="form-group">
          <label>Họ và tên <span className="required">*</span></label>
          <input value={form.fullName} onChange={e => set('fullName', e.target.value)} placeholder="Nguyễn Văn A" />
        </div>
        <div className="form-group">
          <label>CCCD / CMND <span className="required">*</span></label>
          <input value={form.idNumber} onChange={e => set('idNumber', e.target.value)} placeholder="079100000001" disabled={!!customer} />
        </div>
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Điện thoại <span className="required">*</span></label>
          <input value={form.phone} onChange={e => set('phone', e.target.value)} placeholder="0901234567" />
        </div>
        <div className="form-group">
          <label>Ngày sinh</label>
          <input type="date" value={form.dateOfBirth} onChange={e => set('dateOfBirth', e.target.value)} />
        </div>
      </div>
      <div className="form-group">
        <label>Địa chỉ</label>
        <input value={form.address} onChange={e => set('address', e.target.value)} placeholder="123 Nguyễn Huệ, Q1, TP.HCM" />
      </div>
      {customer?.creditScore && (
        <div className="form-group" style={{marginTop:'0.5rem', padding:'0.75rem', background:'var(--surface-50)', borderRadius:'var(--radius)'}}>
           <div style={{fontSize:'0.85rem', color:'var(--text-muted)'}}>Điểm tín dụng CIC</div>
           <div style={{fontWeight:600, color: customer.creditStatus==='Poor'?'var(--red)':customer.creditStatus==='Excellent'?'var(--green)':'var(--amber)'}}>
             {customer.creditScore} — {customer.creditStatus}
           </div>
        </div>
      )}
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

export default function Customers() {
  const { can } = useAuth();
  const [data, setData] = useState(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState(null); // null | { customer? }
  const [confirm, setConfirm] = useState(null);
  const { toast } = useToast();

  const load = useCallback(async (p = page, q = search) => {
    setLoading(true);
    try {
      const res = await api.customers.list({ page: p, pageSize: 15, search: q });
      setData(res.items ? res : { items: Array.isArray(res) ? res : [], totalPages:1, page:1 });
    } catch {}
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(1, ''); }, []);

  const handleSearch = () => { setPage(1); load(1, search); };
  const handlePage   = (p) => { setPage(p); load(p, search); };
  const handleSaved  = () => { setModal(null); load(page, search); };

  const handleCheckCredit = (c) => {
    setConfirm({
      msg: `Kiểm tra tín dụng CIC cho ${c.fullName}?`,
      action: async () => {
        try {
          await api.customers.checkCredit(c.id);
          toast('Đã cập nhật tình trạng tín dụng!');
          load(page, search);
        } catch(e) {
          toast(e.message, 'error');
        }
      }
    });
  };

  const runConfirm = async () => {
    await confirm.action();
    setConfirm(null);
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>👥 Khách hàng</h2>
        {can.createCustomer && <button className="btn btn-primary" onClick={() => setModal({})}>+ Thêm khách hàng</button>}
      </div>

      <div className="filter-bar">
        <input className="search-input" placeholder="🔍  Tìm theo tên, CCCD, điện thoại..."
          value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleSearch()} />
        <button className="btn btn-outline" onClick={handleSearch}>Tìm kiếm</button>
      </div>

      <div className="card">
        <table className="data-table">
          <thead>
            <tr><th>ID</th><th>Họ tên</th><th>CCCD/CMND</th><th>Điện thoại</th><th>Tín dụng</th><th>Trạng thái</th><th>Thao tác</th></tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={7} className="empty-row"><div className="loading-shimmer" /></td></tr>
            ) : !data?.items?.length ? (
              <tr><td colSpan={7} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Không có dữ liệu</p></div></td></tr>
            ) : data.items.map(c => (
              <tr key={c.id}>
                <td><span className="code-badge">#{c.id}</span></td>
                <td><div className="cell-main">{c.fullName}</div><div className="cell-sub">{c.address}</div></td>
                <td><span className="mono">{c.idNumber}</span></td>
                <td>{c.phone}</td>
                <td>
                  {c.creditScore ? (
                    <div><strong style={{color: c.creditStatus==='Poor'?'var(--red)':c.creditStatus==='Excellent'?'var(--green)':'var(--amber)'}}>{c.creditScore}</strong> <span className="cell-sub">{c.creditStatus}</span></div>
                  ) : <span className="cell-sub">Chưa ktra</span>}
                </td>
                <td><StatusBadge status={c.status || 'Active'} /></td>
                <td>
                  {can.createCustomer && (
                    <div className="action-btns">
                      <button className="btn btn-sm btn-outline" onClick={() => handleCheckCredit(c)}>🔍 CIC</button>
                      <button className="btn btn-sm btn-outline" onClick={() => setModal({ customer: c })}>✏️ Sửa</button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Pagination paged={data} onPage={handlePage} />
      </div>

      {modal && (
        <CustomerModal
          customer={modal.customer}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
      {confirm && <ConfirmDialog message={confirm.msg} onConfirm={runConfirm} onCancel={() => setConfirm(null)} />}
    </div>
  );
}
