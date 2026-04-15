import { useState, useEffect } from 'react';
import { customerApi } from '../../customers/api/customerApi';
import { productApi } from '../../products/api/productApi';
import { applicationApi } from '../api/applicationApi';
import { formatCurrency, parseTenors, formatPercent } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { Button } from '../../../components/ui/Button';
import { useToast } from '../../../context/ToastContext';

export function ApplicationFormModal({ onClose, onSaved }) {
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
      customerApi.list({ pageSize:500 }),
      productApi.list(),
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
    try { setPreview(await applicationApi.preview({ productId:+productId, amount:+requestedAmount, tenorMonths:+requestedTenorMonths })); }
    catch(e) { setError(e.message); }
    finally { setPrevLoading(false); }
  };

  const handleSubmit = async () => {
    setError(''); setLoading(true);
    try {
      await applicationApi.create({ ...form, customerId:+form.customerId, productId:+form.productId, requestedAmount:+form.requestedAmount, requestedTenorMonths:+form.requestedTenorMonths });
      toast('Tạo hồ sơ vay thành công!');
      onSaved();
    } catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal title="📋 Tạo hồ sơ vay" onClose={onClose} size="lg"
      actions={<>
        <Button variant="ghost" onClick={onClose}>Hủy</Button>
        <Button variant="outline" onClick={handlePreview} loading={prevLoading}>🧮 Tính toán</Button>
        <Button variant="primary" onClick={handleSubmit} loading={loading}>📋 Tạo hồ sơ</Button>
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
