import { useState } from 'react';
import { loanApi } from '../api/loanApi';
import { formatCurrency, formatDate } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { useToast } from '../../../context/ToastContext';

export function EarlySettleModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const [date, setDate] = useState(new Date().toISOString().split('T')[0]);
  const [preview, setPreview] = useState(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handlePreview = async () => {
    if (!date) return;
    setLoading(true);
    try {
      const data = await loanApi.previewEarlySettle(loanId, date);
      // httpClient already unwraps { success, data } — but guard defensively
      setPreview(data?.totalSettlementAmount !== undefined ? data : data?.data ?? data);
    } catch (e) {
      toast(e.message || 'Lỗi khi tính toán tất toán', 'error');
    } finally {
      setLoading(false);
    }
  };

  const handleSettle = async () => {
    if (!preview) return;
    setSubmitting(true);
    try {
      await loanApi.earlySettle(loanId, { settlementDate: date, paymentMethod: 'Transfer', remarks: 'Khách hàng tất toán trước hạn qua Hệ thống' });
      toast('Tất toán khoản vay thành công!', 'success');
      onDone();
    } catch (e) {
      toast(e.message || 'Tất toán thất bại', 'error');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal title="Tất toán trước hạn (Early Settlement)" onClose={onClose} size="lg"
      actions={<>
        <Button variant="ghost" onClick={onClose}>Hủy</Button>
        <Button variant="danger" disabled={!preview || submitting} onClick={handleSettle}>
          {submitting ? 'Đang xử lý...' : 'Xác nhận Tất toán'}
        </Button>
      </>}>
      
      <div style={{ marginBottom: '1.5rem', display: 'flex', gap: '1rem', alignItems: 'flex-end' }}>
        <div style={{ flex: 1 }}>
          <Input 
            type="date" 
            label="Ngày tất toán" 
            value={date} 
            onChange={(e) => { setDate(e.target.value); setPreview(null); }} 
          />
        </div>
        <Button variant="outline" onClick={handlePreview} disabled={loading || !date}>
          {loading ? 'Đang tính...' : 'Tính toán'}
        </Button>
      </div>

      {preview && (
        <div className="card" style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
          <h4 style={{ marginBottom: '1rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.5rem' }}>Chi tiết Tất toán</h4>
          
          <div className="detail-grid" style={{ gridTemplateColumns: '1fr 1fr' }}>
            <div className="detail-item">
              <div className="detail-label">Ngày tính toán</div>
              <div className="detail-value">{formatDate(preview.settlementDate)}</div>
            </div>
            <div className="detail-item">
              <div className="detail-label">Số ngày lãi tích lũy</div>
              <div className="detail-value">{preview.daysAccrued} ngày</div>
            </div>
          </div>

          <table className="data-table" style={{ marginTop: '1rem' }}>
            <tbody>
              <tr>
                <td>Dư nợ gốc còn lại (Principal Balance)</td>
                <td className="text-right"><strong>{formatCurrency(preview.principalBalance)}</strong></td>
              </tr>
              <tr>
                <td>Lãi tính đến ngày tất toán (Interest Accrued)</td>
                <td className="text-right"><strong>{formatCurrency(preview.interestAccrued)}</strong></td>
              </tr>
              <tr>
                <td>Phí trả nợ trước hạn (Early Settlement Fee)</td>
                <td className="text-right"><strong>{formatCurrency(preview.earlySettlementFee)}</strong></td>
              </tr>
              <tr>
                <td>Phí phạt trễ hạn chưa thu (Late Fees)</td>
                <td className="text-right"><strong>{formatCurrency(preview.lateFeesDue)}</strong></td>
              </tr>
              <tr style={{ background: 'rgba(239, 68, 68, 0.1)' }}>
                <td style={{ fontSize: '1.1rem', color: 'var(--red)', fontWeight: 'bold' }}>TỔNG SỐ TIỀN PHẢI THU</td>
                <td className="text-right" style={{ fontSize: '1.2rem', color: 'var(--red)', fontWeight: 'bold' }}>
                  {formatCurrency(preview.totalSettlementAmount)}
                </td>
              </tr>
            </tbody>
          </table>
          <div style={{ marginTop: '1rem', fontSize: '0.85rem', color: 'var(--text-muted)' }}>
            * Lưu ý: Khi xác nhận tất toán, hệ thống sẽ tự động hạch toán toàn bộ số tiền trên và đóng khoản vay (Status = Closed). Các giao dịch này không thể hoàn tác.
          </div>
        </div>
      )}

    </Modal>
  );
}
