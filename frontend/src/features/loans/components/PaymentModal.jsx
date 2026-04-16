import { useState } from 'react';
import { loanApi } from '../api/loanApi';
import { today } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { useToast } from '../../../context/ToastContext';

export function PaymentModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [form, setForm] = useState({ amount:'', paymentDate:today(), paymentMethod:'Cash', remarks:'' });
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');
  
  const set = (k,v) => setForm(f=>({...f,[k]:v}));
  
  const submit = async () => {
    setError(''); setLoading(true);
    try { 
      await loanApi.pay(loanId, { ...form, amount:+form.amount }); 
      toast('Ghi nhận thanh toán thành công!'); 
      onDone(); 
    }
    catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };
  
  return (
    <Modal title="💰 Thu tiền trả nợ" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-success" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'✅ Ghi nhận'}</button></>}>
      <div className="form-row">
        <div className="form-group">
          <label>Số tiền (VNĐ) <span className="required">*</span></label>
          <input type="number" value={form.amount} onChange={e=>set('amount',e.target.value)} placeholder="0"/>
          <p className="text-xs text-blue-400 mt-1">Ghi chú: Nếu thu thừa, số tiền vượt mức sẽ tự động được chuyển vào tài khoản treo (Suspense) chờ xử lý.</p>
        </div>
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
