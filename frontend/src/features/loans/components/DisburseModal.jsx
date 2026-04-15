import { useState } from 'react';
import { loanApi } from '../api/loanApi';
import { today } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { useToast } from '../../../context/ToastContext';

export function DisburseModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [date, setDate]       = useState(today());
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');
  
  const submit = async () => {
    setError(''); setLoading(true);
    try { 
      await loanApi.disburse(loanId, { disbursementDate: date }); 
      toast('Giải ngân thành công!'); 
      onDone(); 
    }
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
