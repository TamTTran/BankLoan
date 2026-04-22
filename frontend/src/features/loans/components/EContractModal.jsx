import { useState, useEffect } from 'react';
import { Modal } from '../../../components/ui/Modal';
import { Button } from '../../../components/ui/Button';
import { useToast } from '../../../context/ToastContext';
import { httpClient } from '../../../shared/api/httpClient';

export function EContractModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [signing, setSigning] = useState(false);
  const [contract, setContract] = useState(null);
  const [step, setStep] = useState(0); // 0: Check, 1: Animation, 2: Signed

  useEffect(() => {
    checkContract();
  }, []);

  const checkContract = async () => {
    try {
      const res = await httpClient('GET', `/loans/${loanId}/contract`);
      if (res.success && res.data) {
        setContract(res.data);
        setStep(2);
      }
    } catch (err) {
      // 404 means not signed yet
      if (err.response?.status === 404) {
        setStep(0);
      } else {
        toast('Lỗi kiểm tra hợp đồng: ' + err.message, 'error');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleSign = async () => {
    setSigning(true);
    setStep(1); // start animation

    // Fake an animation delay for "Bank-grade Digital experience"
    setTimeout(async () => {
      try {
        const res = await httpClient('POST', `/loans/${loanId}/sign-contract`);
        setContract(res.data);
        setStep(2);
        toast('Ký số thành công! Hợp đồng đã được lưu vết.', 'success');
        if(onDone) setTimeout(onDone, 1500);
      } catch (err) {
        toast(err.message || 'Lỗi khi ký hợp đồng', 'error');
        setStep(0);
      } finally {
        setSigning(false);
      }
    }, 2000); // 2s scanning animation
  };

  return (
    <Modal title="Ký Hợp Đồng Điện Tử" onClose={onClose} width="500px">
      <div className="econtract-modal">
        {loading ? (
          <div className="flex-center p-4"><div className="loading-shimmer w-100 h-32"/></div>
        ) : step === 0 ? (
          <div className="verify-step text-center">
            <div className="icon-pulse mx-auto mb-3" style={{fontSize:'3rem'}}>📄</div>
            <h4>Chưa có Chữ ký số</h4>
            <p className="text-secondary mt-2">Khoản vay này cần phải được ký điện tử để đảm bảo tính pháp lý (Mã băm SHA-256 + TimeStamp) trước khi có thể giải ngân.</p>
            <div className="mt-4">
              <Button onClick={handleSign} loading={signing} variant="primary" className="w-100">Cấp chữ ký số tự động</Button>
            </div>
          </div>
        ) : step === 1 ? (
          <div className="anim-step text-center py-4">
            <div className="scanning-bar"></div>
            <h5 className="mt-4 text-glow">Đang mã hóa hợp đồng (SHA-256)...</h5>
            <p className="text-secondary fs-sm">Hệ thống đang cấp chứng thư số và đóng dấu Timestamp.</p>
            
            <style>{`
              .scanning-bar {
                width: 100%; height: 40px; background: rgba(59, 130, 246, 0.1); border-radius: 4px; overflow: hidden; position: relative;
              }
              .scanning-bar::after {
                content: ''; position: absolute; top:0; left:0; width: 30%; height: 100%; background: var(--primary);
                animation: scanBar 1.5s infinite ease-in-out alternate;
              }
              @keyframes scanBar { 0% { left: 0; } 100% { left: 70%; } }
              .text-glow { animation: pulse 1s infinite alternate; }
            `}</style>
          </div>
        ) : (
          <div className="result-step text-center">
            <div className="icon-success mx-auto mb-3" style={{fontSize: '3.5rem', color:'var(--green)'}}>✅</div>
            <h4>Hợp đồng điện tử đã ký!</h4>
            <div className="bg-light p-3 rounded mt-3 text-start fs-sm" style={{fontFamily: 'monospace', wordBreak: 'break-all'}}>
              <div><strong>Số HĐ:</strong> {contract?.contractNumber}</div>
              <div className="mt-2"><strong>Mã Băm (SHA-256):</strong><br/> <span style={{color:'var(--primary)'}}>{contract?.pdfContentHash}</span></div>
              <div className="mt-2 text-secondary">Thời gian pháp lý: {new Date(contract?.signedAt).toLocaleString()}</div>
            </div>
            <div className="mt-4">
              <Button onClick={onDone || onClose} variant="success" className="w-100">Tiếp tục giải ngân C.k</Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
