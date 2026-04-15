import { useState } from 'react';
import { applicationApi } from '../api/applicationApi';
import Modal from '../../../components/Modal';
import { Button } from '../../../components/ui/Button';
import { useToast } from '../../../context/ToastContext';

export function RejectApplicationModal({ id, onClose, onDone }) {
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const { toast } = useToast();
  
  const submit = async () => {
    setLoading(true);
    try { 
      await applicationApi.reject(id, { reason }); 
      toast('Đã từ chối hồ sơ.', 'warning'); 
      onDone(); 
    }
    catch(e) { toast(e.message,'error'); }
    finally { setLoading(false); }
  };
  
  return (
    <Modal title="❌ Từ chối hồ sơ" onClose={onClose}
      actions={<><Button variant="ghost" onClick={onClose}>Hủy</Button><Button variant="danger" onClick={submit} loading={loading}>❌ Từ chối</Button></>}>
      <div className="form-group">
        <label>Lý do từ chối <span className="required">*</span></label>
        <textarea value={reason} onChange={e=>setReason(e.target.value)} rows={3} placeholder="Nhập lý do..." />
      </div>
    </Modal>
  );
}
