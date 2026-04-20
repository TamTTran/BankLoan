import { useState, useEffect, useCallback } from 'react';
import { customerApi } from '../features/customers/api/customerApi';
import { formatDate, today } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import Modal from '../components/Modal';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

function EkycModal({ customer, onClose, onVerified }) {
  const { toast } = useToast();
  const [step, setStep] = useState(0); // 0: init, 1: scanning face, 2: reading nfc/ocr, 3: verifying, 4: done
  const [result, setResult] = useState(null);

  useEffect(() => {
    if (step === 1) {
      setTimeout(() => setStep(2), 1500);
    } else if (step === 2) {
      setTimeout(() => setStep(3), 1500);
    } else if (step === 3) {
      customerApi.verifyEkyc(customer.id).then(res => {
        setResult(res);
        setStep(4);
        if (res.status === 'Verified') {
          toast(`eKYC Thành công! Tỉ lệ khớp khuôn mặt: ${res.faceMatchScore}%. Liveness: ${res.livenessScore}%`);
          onVerified();
        } else {
          toast(`eKYC không đạt: ${res.status}`, 'warning');
        }
      }).catch(e => {
        toast(e.message, 'error');
        onClose();
      });
    }
  }, [step]);

  return (
    <Modal
      title="📸 Xác thực Sinh trắc học eKYC"
      onClose={onClose}
      actions={step !== 4 ? null : <Button variant="primary" onClick={onClose}>Đóng</Button>}
    >
      <div style={{ textAlign: 'center', padding: '1rem 0' }}>
        <h3 style={{ marginBottom: '0.5rem' }}>{customer.fullName}</h3>
        <p style={{ color: 'var(--text-muted)' }}>CCCD: <span className="mono">{customer.idNumber}</span></p>

        <div style={{ margin: '2rem auto', width: '200px', height: '200px', border: '2px dashed var(--border)', borderRadius: '1rem', position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--bg-elevated)', overflow: 'hidden' }}>
          {step === 0 && <Button variant="primary" onClick={() => setStep(1)}>🎥 Bắt đầu quét khuôn mặt</Button>}
          {step > 0 && step < 4 && (
            <div style={{ textAlign: 'center' }}>
              <div className="spinner" style={{ width: '40px', height: '40px', margin: '0 auto 1rem' }} />
              <div style={{ fontWeight: 500, color: 'var(--primary)' }}>
                {step === 1 ? 'Đang phân tích Liveness...' : step === 2 ? 'Đang trích xuất OCR & NFC...' : 'Đang xử lý kết quả...'}
              </div>
            </div>
          )}
          {step === 4 && result && (
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>{result.status === 'Verified' ? '✅' : '⚠️'}</div>
              <div style={{ fontWeight: 600, color: result.status === 'Verified' ? 'var(--green)' : 'var(--amber)' }}>{result.status}</div>
            </div>
          )}
        </div>

        {step === 4 && result && (
          <div style={{ textAlign: 'left', background: 'var(--bg-subtle)', padding: '1rem', borderRadius: 'var(--radius)', fontSize: '0.9rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-muted)' }}>Liveness Score:</span>
              <strong style={{ color: result.livenessScore >= 95 ? 'var(--green)' : 'var(--red)' }}>{result.livenessScore}%</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-muted)' }}>Face Match:</span>
              <strong style={{ color: result.faceMatchScore >= 80 ? 'var(--green)' : 'var(--amber)' }}>{result.faceMatchScore}%</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--text-muted)' }}>OCR Confidence:</span>
              <strong style={{ color: result.ocrConfidence >= 85 ? 'var(--green)' : 'var(--amber)' }}>{result.ocrConfidence}%</strong>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}

function CustomerModal({ customer, onClose, onSaved }) {
  const { toast } = useToast();
  const [form, setForm] = useState({
    fullName: customer?.fullName || '', idNumber: customer?.idNumber || '',
    phone: customer?.phone || '', dateOfBirth: customer?.dateOfBirth?.substring(0,10) || '',
    address: customer?.address || '',
    bankAccountNumber: customer?.bankAccountNumber || '',
    bankCode: customer?.bankCode || ''
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const set = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = async () => {
    setError(''); setLoading(true);
    try {
      if (customer) await customerApi.update(customer.id, form);
      else          await customerApi.create(form);
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
        <Button variant="ghost" onClick={onClose}>Hủy</Button>
        <Button variant="primary" onClick={handleSubmit} loading={loading}>💾 Lưu</Button>
      </>}
    >
      <div className="form-row">
        <Input label="Họ và tên *" value={form.fullName} onChange={e => set('fullName', e.target.value)} placeholder="Nguyễn Văn A" />
        <Input label="CCCD / CMND *" value={form.idNumber} onChange={e => set('idNumber', e.target.value)} placeholder="079100000001" disabled={!!customer} />
      </div>
      <div className="form-row">
        <Input label="Điện thoại *" value={form.phone} onChange={e => set('phone', e.target.value)} placeholder="0901234567" />
        <Input type="date" label="Ngày sinh" value={form.dateOfBirth} onChange={e => set('dateOfBirth', e.target.value)} />
      </div>
      <Input label="Địa chỉ" value={form.address} onChange={e => set('address', e.target.value)} placeholder="123 Nguyễn Huệ, Q1, TP.HCM" />
      <div className="form-row">
        <Input label="Số tài khoản NH giải ngân" value={form.bankAccountNumber} onChange={e => set('bankAccountNumber', e.target.value)} placeholder="1903..." />
        <Input type="select" label="Mã Ngân Hàng" value={form.bankCode} onChange={e => set('bankCode', e.target.value)}>
           <option value="">-- Chọn NH --</option>
           <option value="NAPAS">NAPAS (Nội bộ)</option>
           <option value="VCB">Vietcombank</option>
           <option value="TCB">Techcombank</option>
           <option value="MB">MB Bank</option>
        </Input>
      </div>
      {customer?.creditScore && (
        <div className="form-group" style={{marginTop:'0.5rem', padding:'0.75rem', background:'var(--bg-elevated)', borderRadius:'var(--radius)'}}>
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
  const [modal, setModal] = useState(null); // null | { type: 'edit'|'ekyc', customer? }
  const [confirm, setConfirm] = useState(null);
  const { toast } = useToast();

  const load = useCallback(async (p = page, q = search) => {
    setLoading(true);
    try {
      const res = await customerApi.list({ page: p, pageSize: 15, search: q });
      setData(res.items ? res : { items: Array.isArray(res) ? res : [], totalPages:1, page:1 });
    } catch (e) {
      toast(e.message || 'Không thể tải dữ liệu', 'error');
    }
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
          await customerApi.checkCredit(c.id);
          toast('Đã cập nhật tình trạng tín dụng!');
          load(page, search);
        } catch(e) {
          toast(e.message, 'error');
        }
      }
    });
  };

  const handleVerifyEkyc = (c) => {
    setModal({ type: 'ekyc', customer: c });
  };

  const runConfirm = async () => {
    await confirm.action();
    setConfirm(null);
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>👥 Khách hàng</h2>
        {can.createCustomer && <Button variant="primary" onClick={() => setModal({ type: 'edit', customer: null })}>+ Thêm khách hàng</Button>}
      </div>

      <div className="filter-bar">
        <div style={{ flex: 1, minWidth: '200px' }}>
          <Input placeholder="🔍 Tìm theo tên, CCCD, điện thoại..."
            value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleSearch()} />
        </div>
        <Button variant="outline" onClick={handleSearch}>Tìm kiếm</Button>
      </div>

      <Card>
        <table className="data-table">
          <thead>
            <tr><th>ID</th><th>Họ tên</th><th>CCCD/CMND</th><th>Điện thoại</th><th>Tín dụng</th><th>eKYC</th><th>Trạng thái</th><th>Thao tác</th></tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={8} className="empty-row"><div className="loading-shimmer" /></td></tr>
            ) : !data?.items?.length ? (
              <tr><td colSpan={8} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Không có dữ liệu</p></div></td></tr>
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
                <td>
                  {c.ekycStatus === 'Verified' ? <span style={{color: 'var(--green)', fontWeight: 'bold'}}>Verified</span> :
                   c.ekycStatus === 'Pending' ? <span style={{color: 'var(--text-muted)'}}>- Pending -</span> :
                   <span style={{color: 'var(--amber)'}}>{c.ekycStatus}</span>}
                </td>
                <td><StatusBadge status={c.status || 'Active'} /></td>
                <td>
                  {can.createCustomer && (
                    <div className="action-btns">
                      <Button size="sm" variant="outline" onClick={() => handleVerifyEkyc(c)}>📸 eKYC</Button>
                      <Button size="sm" variant="outline" onClick={() => handleCheckCredit(c)}>🔍 CIC</Button>
                      <Button size="sm" variant="outline" onClick={() => setModal({ type: 'edit', customer: c })}>✏️ Sửa</Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Pagination paged={data} onPage={handlePage} />
      </Card>

      {modal && modal.type === 'edit' && (
        <CustomerModal
          customer={modal.customer}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
      {modal && modal.type === 'ekyc' && (
        <EkycModal
          customer={modal.customer}
          onClose={() => { setModal(null); load(page, search); }}
          onVerified={() => { /* updated on close anyway */ }}
        />
      )}
      {confirm && <ConfirmDialog message={confirm.msg} onConfirm={runConfirm} onCancel={() => setConfirm(null)} />}
    </div>
  );
}
