import { useState, useEffect } from 'react';
import { loanApi } from '../api/loanApi';
import { mapLoan } from '../mappers/loanMapper';
import { formatCurrency, formatDate, formatPercent } from '../../../utils/format';
import { StatusBadge } from '../../../components/Badge';
import Modal from '../../../components/Modal';
import { Button } from '../../../components/ui/Button';
import { useToast } from '../../../context/ToastContext';

export function LoanDetailModal({ loanId, onClose }) {
  const { toast } = useToast();
  const [loan, setLoan]         = useState(null);
  const [schedule, setSchedule] = useState([]);
  const [payments, setPayments] = useState([]);
  const [tab, setTab]           = useState('schedule');
  const [loading, setLoading]   = useState(true);

  useEffect(() => {
    Promise.all([loanApi.get(loanId), loanApi.schedule(loanId), loanApi.payments(loanId)])
      .then(([l, s, p]) => {
        setLoan(mapLoan(l));
        setSchedule(Array.isArray(s) ? s : s?.schedules || []);
        setPayments(Array.isArray(p) ? p : p?.payments || []);
      }).catch(console.error).finally(() => setLoading(false));
  }, [loanId]);

  const handleAction = async (actionFn, title, promptText) => {
    const reason = prompt(promptText || `Nhập lý do cho nghiệp vụ ${title}:`);
    if (reason === null) return;
    if (!reason.trim()) { toast('Vui lòng nhập lý do.', 'error'); return; }
    try {
      await actionFn(loanId, { reason, remarks: reason });
      toast(`Đã gửi yêu cầu ${title} thành công! Đang chờ phê duyệt.`, 'success');
      onClose();
    } catch (e) {
      toast(`Lỗi khi ${title}: ` + (e.message || 'Có lỗi xảy ra'), 'error');
    }
  };

  const outstanding = loan?.outstandingAmount ?? loan?.approvedAmount;
  const progress    = loan?.approvedAmount ? Math.round((1 - outstanding/loan.approvedAmount)*100) : 0;

  return (
    <Modal title={`💰 Chi tiết khoản vay — ${loan?.loanAccountNo || '...'}`} onClose={onClose} size="xl"
      actions={<Button variant="ghost" onClick={onClose}>Đóng</Button>}>
      {loading ? <div className="loading-shimmer" style={{height:200}}/> : (<>
        {/* Summary */}
        <div className="detail-grid">
          {[
            ['Khách hàng',   loan?.customer?.fullName],
            ['Sản phẩm',     loan?.product?.productName],
            ['Trạng thái',   <StatusBadge key="s" status={loan?.status}/>],
            ['Số tiền vay',  formatCurrency(loan?.approvedAmount)],
            ['Dư nợ còn lại',<span key="o" style={{color:'var(--amber)',fontWeight:700}}>{formatCurrency(outstanding)}</span>],
            ['Lãi suất',     formatPercent(loan?.monthlyInterestRate)+'/tháng'],
            ['Kỳ hạn',       loan?.tenorMonths+' tháng'],
            ['Giải ngân',    formatDate(loan?.disbursementDate)],
            ['Đáo hạn',      formatDate(loan?.maturityDate)],
          ].map(([label, val]) => (
            <div key={label} className="detail-item">
              <div className="detail-label">{label}</div>
              <div className="detail-value">{val || '—'}</div>
            </div>
          ))}
        </div>
        <div style={{marginBottom:'1.5rem'}}>
          <div style={{fontSize:'0.75rem',color:'var(--text-muted)',marginBottom:'0.4rem'}}>Tiến độ trả nợ: {progress}%</div>
          <div className="progress-bar" style={{height:8}}><div className="progress-fill" style={{width:`${progress}%`}}/></div>
        </div>
      </>)}

      {/* Tabs */}
      <div className="modal-tabs" style={{marginTop:'-1.5rem'}}>
        {[['schedule','📅 Lịch trả nợ'],['payments','💳 Lịch sử TT'],['ops','⚙️ Nghiệp vụ']].map(([k,l])=>(
          <div key={k} className={`modal-tab${tab===k?' active':''}`} onClick={()=>setTab(k)}>{l}</div>
        ))}
      </div>

      {tab === 'schedule' && (
        <div className="modal-tab-content" style={{padding:0}}>
          <table className="data-table">
            <thead><tr><th>Kỳ</th><th>Đến hạn</th><th>Gốc</th><th>Lãi</th><th>Tổng</th><th>Còn lại</th><th>Trạng thái</th></tr></thead>
            <tbody>
              {!schedule.length ? <tr><td colSpan={7} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Chưa có lịch</p></div></td></tr>
              : schedule.map(s=>(
                <tr key={s.installmentNo} className={s.status==='Overdue'?'row-overdue':s.status==='Paid'?'row-paid':''}>
                  <td className="text-center">{s.installmentNo}</td>
                  <td>{formatDate(s.dueDate)}</td>
                  <td>{formatCurrency(s.principalDue)}</td>
                  <td>{formatCurrency(s.interestDue)}</td>
                  <td><strong>{formatCurrency(s.principalDue+s.interestDue)}</strong></td>
                  <td>{formatCurrency(s.remainingDueAmount)}</td>
                  <td><StatusBadge status={s.status}/></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {tab === 'payments' && (
        <div className="modal-tab-content" style={{padding:0}}>
          <table className="data-table">
            <thead><tr><th>Ngày thu</th><th>Số tiền</th><th>Phương thức</th><th>Ghi chú</th></tr></thead>
            <tbody>
              {!payments.length ? <tr><td colSpan={4} className="empty-row"><div className="empty-state"><span className="empty-icon">💳</span><p>Chưa có thanh toán</p></div></td></tr>
              : payments.map((p,i)=>(
                <tr key={i}>
                  <td>{formatDate(p.paymentDate)}</td>
                  <td><strong>{formatCurrency(p.amount)}</strong></td>
                  <td>{p.paymentMethod==='Cash'?'Tiền mặt':'Chuyển khoản'}</td>
                  <td><span className="cell-sub">{p.remarks||'—'}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {tab === 'ops' && (
        <div className="modal-tab-content" style={{padding:'20px'}}>
          <div style={{ display:'flex', gap:'10px', flexWrap:'wrap' }}>
            <Button variant="outline" onClick={() => handleAction(loanApi.restructure, 'Cơ cấu lại nợ')}>🔄 Cơ cấu lại nợ (Restructure)</Button>
            <Button variant="outline" onClick={() => handleAction(loanApi.reschedule, 'Dời ngày')}>📅 Dời ngày (Reschedule)</Button>
            <Button variant="outline" onClick={() => handleAction(loanApi.manualAdjustment, 'Sửa tiền/Miễn giảm')}>📝 Sửa tiền / Miễn giảm (Maker-Checker)</Button>
            <Button variant="danger" onClick={() => handleAction(loanApi.writeOff, 'Xóa Nợ')}>🗑 Xóa Nợ (Write-off)</Button>
          </div>
          <div style={{ marginTop: '20px', padding: '15px', background: 'var(--bg-card)', borderRadius: '8px', fontSize: '0.85rem' }}>
            <strong>💡 Hướng dẫn:</strong> Các nghiệp vụ này yêu cầu sự phê duyệt của cấp Quản lý (Maker-Checker). Yêu cầu sẽ bay thẳng vào mục <b>Hàng đợi phê duyệt</b> để quản lý xem xét trước khi có hiệu lực.
          </div>
        </div>
      )}
    </Modal>
  );
}
