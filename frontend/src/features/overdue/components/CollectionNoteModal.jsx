import { useState } from 'react';
import { loanApi } from '../../loans/api/loanApi';
import { today } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { useToast } from '../../../context/ToastContext';

export function CollectionNoteModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [noteText, setNoteText] = useState('');
  const [noteDate, setNoteDate] = useState(today());
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState('');

  const submit = async () => {
    if (!noteText.trim()) { setError('Vui lòng nhập nội dung ghi chú'); return; }
    setError(''); setLoading(true);
    try { 
      await loanApi.addNote(loanId, { noteText, noteDate }); 
      toast('Đã thêm ghi chú thu hồi!'); 
      onDone(); 
    }
    catch(e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal title="📝 Ghi chú thu hồi" onClose={onClose}
      actions={<><button className="btn btn-ghost" onClick={onClose}>Hủy</button><button className="btn btn-primary" onClick={submit} disabled={loading}>{loading?<><span className="spinner"/>...</>:'📝 Lưu'}</button></>}>
      <div className="form-group"><label>Ngày ghi chú <span className="required">*</span></label><input type="date" value={noteDate} onChange={e=>setNoteDate(e.target.value)}/></div>
      <div className="form-group">
        <label>Nội dung <span className="required">*</span></label>
        <textarea value={noteText} onChange={e=>setNoteText(e.target.value)} rows={4} placeholder="Khách hàng hứa thanh toán vào... / Đã liên hệ qua sđt... / Gặp trực tiếp tại..."/>
      </div>
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}
