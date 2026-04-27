import { useState, useEffect } from 'react';
import { loanApi } from '../api/loanApi';
import { mapLoan } from '../mappers/loanMapper';
import { formatCurrency, formatDate, formatPercent } from '../../../utils/format';
import { StatusBadge } from '../../../components/Badge';
import Modal from '../../../components/Modal';
import { Modal as UIModal } from '../../../components/ui/Modal';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { useToast } from '../../../context/ToastContext';
import { useT } from '../../../i18n/useT';
import { useNotifications } from '../../../context/NotificationContext';


// ── Inline sub-modal for ops ─────────────────────────────────────────────────
function OpsModal({ title, onClose, onSubmit, submitting, children }) {
  return (
    <UIModal title={title} onClose={onClose} width="460px">
      <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        {children}
        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end', marginTop: '0.5rem' }}>
          <Button variant="ghost" onClick={onClose} disabled={submitting}>Huỷ</Button>
          <Button variant="primary" onClick={onSubmit} disabled={submitting}>
            {submitting ? 'Đang gửi…' : 'Gửi yêu cầu'}
          </Button>
        </div>
      </div>
    </UIModal>
  );
}

// ── Early Settlement preview modal ───────────────────────────────────────────
function EarlySettleModal({ loanId, onClose, onDone }) {
  const { toast } = useToast();
  const { refreshCount } = useNotifications();
  const [date, setDate] = useState(new Date().toISOString().split('T')[0]);
  const [preview, setPreview] = useState(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handlePreview = async () => {
    if (!date) { toast('Vui lòng chọn ngày tất toán.', 'error'); return; }
    setLoadingPreview(true);
    try {
      const data = await loanApi.previewEarlySettle(loanId, date);
      setPreview(data);
    } catch (e) {
      toast('Lỗi xem trước: ' + (e.message || 'Có lỗi xảy ra'), 'error');
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleSettle = async () => {
    setSubmitting(true);
    try {
      await loanApi.earlySettle(loanId, { settlementDate: date });
      refreshCount();
      toast('Đã gửi yêu cầu tất toán sớm. Đang chờ phê duyệt.', 'success');
      onDone();
    } catch (e) {
      toast('Lỗi tất toán: ' + (e.message || 'Có lỗi xảy ra'), 'error');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <UIModal title="🏁 Tất toán sớm (Early Settlement)" onClose={onClose} width="480px">
      <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <Input type="date" label="Ngày tất toán" value={date} onChange={e => setDate(e.target.value)} />
        <Button variant="outline" onClick={handlePreview} disabled={loadingPreview}>
          {loadingPreview ? 'Đang tính…' : '🔍 Xem trước số tiền'}
        </Button>

        {preview && (
          <div style={{ padding: '1rem', background: 'var(--bg-muted)', borderRadius: '12px', fontSize: '0.88rem' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
              {[
                ['Gốc còn lại',       formatCurrency(preview.remainingPrincipal)],
                ['Lãi đến ngày TT',   formatCurrency(preview.proRataInterest)],
                ['Phí tất toán sớm',  formatCurrency(preview.earlySettlementFee)],
                ['Phí phạt chưa thu', formatCurrency(preview.unpaidLateFees)],
                ['Tổng cần thanh toán',formatCurrency(preview.totalSettlementAmount)],
              ].map(([k, v]) => (
                <div key={k}>
                  <div style={{ color: 'var(--text-muted)', fontSize: '0.78rem' }}>{k}</div>
                  <div style={{ fontWeight: 700, color: 'var(--navy)' }}>{v}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
          <Button variant="ghost" onClick={onClose}>Huỷ</Button>
          <Button variant="primary" onClick={handleSettle} disabled={!preview || submitting}>
            {submitting ? 'Đang xử lý…' : '✅ Xác nhận tất toán'}
          </Button>
        </div>
      </div>
    </UIModal>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
export function LoanDetailModal({ loanId, onClose }) {
  const { toast } = useToast();
  const { t } = useT();
  const { refreshCount } = useNotifications();
  const [loan, setLoan]         = useState(null);
  const [schedule, setSchedule] = useState([]);


  const [payments, setPayments] = useState([]);
  const [tab, setTab]           = useState('schedule');
  const [loading, setLoading]   = useState(true);

  // Active sub-modal: null | 'restructure' | 'reschedule' | 'adjust' | 'writeoff' | 'earlysettle' | 'reversal'
  const [subModal, setSubModal] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Form state for each sub-modal
  const [restructureForm, setRestructureForm] = useState({ newTenorMonths: '', newMonthlyRate: '', reason: '' });
  const [rescheduleForm,  setRescheduleForm]  = useState({ newPaymentDay: '', reason: '' });
  const [adjustForm,      setAdjustForm]      = useState({ adjustmentType: 'LateFee', amount: '', reasonCode: 'CustomerHardship', note: '' });
  const [writeoffForm,    setWriteoffForm]    = useState({ reason: '', confirmed: false });
  const [reversalForm,    setReversalForm]    = useState({ paymentId: '', reason: '' });

  const reload = () => {
    setLoading(true);
    Promise.all([loanApi.get(loanId), loanApi.schedule(loanId), loanApi.payments(loanId)])
      .then(([l, s, p]) => {
        setLoan(mapLoan(l));
        setSchedule(Array.isArray(s) ? s : s?.schedules || []);
        setPayments(Array.isArray(p) ? p : p?.payments || []);
      }).catch(console.error).finally(() => setLoading(false));
  };

  useEffect(() => { reload(); }, [loanId]);

  const closeSubModal = () => setSubModal(null);
  const openReversalModal = (paymentId) => {
    setReversalForm({ paymentId: String(paymentId), reason: '' });
    setSubModal('reversal');
  };

  const submitOp = async (apiFn, successMsg, payload) => {
    setSubmitting(true);
    try {
      await apiFn(loanId, payload);
      toast(successMsg, 'success');
      closeSubModal();
      onClose();
      // Refresh notification bell so new backend notification appears immediately
      refreshCount();
    } catch (e) {
      toast('Lỗi: ' + (e.message || 'Có lỗi xảy ra'), 'error');
    } finally {
      setSubmitting(false);
    }
  };


  const outstanding = loan?.outstandingAmount ?? loan?.approvedAmount;
  const progress    = loan?.approvedAmount ? Math.round((1 - outstanding / loan.approvedAmount) * 100) : 0;

  return (
    <>
      {/* ── Sub-modal: Restructure ── */}
      {subModal === 'restructure' && (
        <OpsModal
          title="🔄 Cơ cấu lại nợ (Restructure)"
          onClose={closeSubModal}
          submitting={submitting}
          onSubmit={() => submitOp(
            loanApi.restructure,
            'Đã gửi yêu cầu cơ cấu! Đang chờ phê duyệt.',
            { newTenorMonths: +restructureForm.newTenorMonths, newMonthlyInterestRate: +restructureForm.newMonthlyRate / 100, reason: restructureForm.reason }
          )}
        >
          <Input label="Kỳ hạn mới (tháng)" type="number" min="1" value={restructureForm.newTenorMonths}
            onChange={e => setRestructureForm(f => ({ ...f, newTenorMonths: e.target.value }))} />
          <Input label="Lãi suất mới (%/tháng)" type="number" step="0.01" min="0" value={restructureForm.newMonthlyRate}
            onChange={e => setRestructureForm(f => ({ ...f, newMonthlyRate: e.target.value }))} />
          <Input type="textarea" label="Lý do" value={restructureForm.reason}
            onChange={e => setRestructureForm(f => ({ ...f, reason: e.target.value }))} />
        </OpsModal>
      )}

      {/* ── Sub-modal: Reschedule ── */}
      {subModal === 'reschedule' && (
        <OpsModal
          title="📅 Dời ngày trả nợ (Reschedule)"
          onClose={closeSubModal}
          submitting={submitting}
          onSubmit={() => submitOp(
            loanApi.reschedule,
            'Đã gửi yêu cầu dời ngày! Đang chờ phê duyệt.',
            { newPaymentDayOfMonth: +rescheduleForm.newPaymentDay, reason: rescheduleForm.reason }
          )}
        >
          <Input label="Ngày trả nợ mới (1–28)" type="number" min="1" max="28" value={rescheduleForm.newPaymentDay}
            onChange={e => setRescheduleForm(f => ({ ...f, newPaymentDay: e.target.value }))} />
          <Input type="textarea" label="Lý do" value={rescheduleForm.reason}
            onChange={e => setRescheduleForm(f => ({ ...f, reason: e.target.value }))} />
        </OpsModal>
      )}

      {/* ── Sub-modal: Manual Adjustment ── */}
      {subModal === 'adjust' && (
        <OpsModal
          title="📝 Sửa tiền / Miễn giảm (Maker-Checker)"
          onClose={closeSubModal}
          submitting={submitting}
          onSubmit={() => submitOp(
            loanApi.manualAdjustment,
            'Đã gửi yêu cầu điều chỉnh! Đang chờ phê duyệt.',
            { adjustmentType: adjustForm.adjustmentType, amount: +adjustForm.amount, reasonCode: adjustForm.reasonCode, note: adjustForm.note }
          )}
        >
          <Input type="select" label="Loại điều chỉnh" value={adjustForm.adjustmentType}
            onChange={e => setAdjustForm(f => ({ ...f, adjustmentType: e.target.value }))}>
            <option value="LateFee">Phí phạt trả chậm</option>
            <option value="Interest">Lãi vay</option>
            <option value="Principal">Gốc</option>
            <option value="Fee">Phí khác</option>
          </Input>
          <Input label="Số tiền (âm = giảm, dương = tăng)" type="number" value={adjustForm.amount}
            onChange={e => setAdjustForm(f => ({ ...f, amount: e.target.value }))} />
          <Input type="select" label="Lý do" value={adjustForm.reasonCode}
            onChange={e => setAdjustForm(f => ({ ...f, reasonCode: e.target.value }))}>
            <option value="CustomerHardship">Khó khăn tài chính</option>
            <option value="SystemError">Lỗi hệ thống</option>
            <option value="CustomerRequest">Yêu cầu khách hàng</option>
            <option value="CourtOrder">Lệnh tòa án</option>
          </Input>
          <Input type="textarea" label="Ghi chú" value={adjustForm.note}
            onChange={e => setAdjustForm(f => ({ ...f, note: e.target.value }))} />
        </OpsModal>
      )}

      {/* ── Sub-modal: Write-off ── */}
      {subModal === 'writeoff' && (
        <OpsModal
          title="🗑 Xóa Nợ (Write-off)"
          onClose={closeSubModal}
          submitting={submitting}
          onSubmit={() => {
            if (!writeoffForm.confirmed) { toast('Vui lòng xác nhận trước khi xóa nợ.', 'error'); return; }
            submitOp(loanApi.writeOff, 'Đã gửi yêu cầu xóa nợ! Đang chờ phê duyệt.', { reason: writeoffForm.reason });
          }}
        >
          <div style={{ padding: '0.75rem', background: 'var(--red-bg)', borderRadius: '10px', fontSize: '0.85rem', color: 'var(--red)' }}>
            ⚠️ Xóa nợ là thao tác không thể hoàn tác. Khoản vay sẽ được ghi nhận là mất vốn và chuyển sang tài khoản xóa nợ.
          </div>
          <Input type="select" label="Lý do xóa nợ" value={writeoffForm.reason}
            onChange={e => setWriteoffForm(f => ({ ...f, reason: e.target.value }))}>
            <option value="">-- Chọn lý do --</option>
            <option value="Uncollectible">Không thể thu hồi</option>
            <option value="BankruptCustomer">Khách hàng phá sản</option>
            <option value="LegalResolution">Giải quyết pháp lý</option>
            <option value="Other">Lý do khác</option>
          </Input>
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.88rem', cursor: 'pointer' }}>
            <input type="checkbox" checked={writeoffForm.confirmed}
              onChange={e => setWriteoffForm(f => ({ ...f, confirmed: e.target.checked }))} />
            Tôi xác nhận đã đọc và hiểu hậu quả của việc xóa nợ này.
          </label>
        </OpsModal>
      )}

      {/* ── Sub-modal: Payment Reversal ── */}
      {subModal === 'reversal' && (
        <OpsModal
          title="↩️ Hoàn tiền / Đảo bút toán (Reversal)"
          onClose={closeSubModal}
          submitting={submitting}
          onSubmit={() => submitOp(
            loanApi.requestReversal,
            'Đã gửi yêu cầu đảo bút toán! Đang chờ phê duyệt.',
            { paymentId: reversalForm.paymentId ? +reversalForm.paymentId : undefined, reason: reversalForm.reason }
          )}
        >
          <Input
            type="select"
            label="Khoản thanh toán cần đảo"
            value={reversalForm.paymentId}
            onChange={e => setReversalForm(f => ({ ...f, paymentId: e.target.value }))}
          >
            <option value="">-- Chọn khoản thanh toán --</option>
            {payments.map((p, index) => {
              const paymentKey = p.id ?? `${p.paymentDate}-${p.amount}-${index}`;
              return (
                <option key={paymentKey} value={p.id ?? ''} disabled={!p.id}>
                  {`${formatDate(p.paymentDate)} • ${formatCurrency(p.amount)}${p.id ? ` • #${p.id}` : ''}`}
                </option>
              );
            })}
          </Input>
          <Input label="Lý do hoàn tiền" type="textarea" value={reversalForm.reason}
            onChange={e => setReversalForm(f => ({ ...f, reason: e.target.value }))} />
        </OpsModal>
      )}

      {/* ── Sub-modal: Early Settlement ── */}
      {subModal === 'earlysettle' && (
        <EarlySettleModal loanId={loanId} onClose={closeSubModal} onDone={() => { closeSubModal(); onClose(); }} />
      )}

      {/* ── Main loan detail modal ── */}
      <Modal title={`💰 ${t('loans.detail.title')} — ${loan?.loanAccountNo || '...'}`} onClose={onClose} size="xl"
        actions={<Button variant="ghost" onClick={onClose}>{t('common.close')}</Button>}>
        {loading ? <div className="loading-shimmer" style={{ height: 200 }} /> : (<>
          {/* Summary */}
          <div className="detail-grid">
            {[
              [t('loans.customer'),          loan?.customer?.fullName],
              [t('loans.product'),           loan?.product?.productName],
              [t('common.status'),           <StatusBadge key="s" status={loan?.status} />],
              [t('loans.approvedAmount'),    formatCurrency(loan?.approvedAmount)],
              [t('loans.outstanding'),       <span key="o" style={{ color: 'var(--amber)', fontWeight: 700 }}>{formatCurrency(outstanding)}</span>],
              [t('loans.interestRate'),      formatPercent(loan?.monthlyInterestRate) + '/tháng'],
              [t('loans.tenor'),             loan?.tenorMonths + ' tháng'],
              [t('loans.disbursedOn'),       formatDate(loan?.disbursementDate)],
              [t('loans.maturityDate'),      formatDate(loan?.maturityDate)],
            ].map(([label, val]) => (
              <div key={label} className="detail-item">
                <div className="detail-label">{label}</div>
                <div className="detail-value">{val || '—'}</div>
              </div>
            ))}
          </div>
          <div style={{ marginBottom: '1.5rem' }}>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.4rem' }}>{t('loans.detail.repaymentProgress')}: {progress}%</div>
            <div className="progress-bar" style={{ height: 8 }}><div className="progress-fill" style={{ width: `${progress}%` }} /></div>
          </div>
        </>)}

        {/* Tabs */}
        <div className="modal-tabs" style={{ marginTop: '-1.5rem' }}>
          {[['schedule', `📅 ${t('loans.detail.schedule')}`], ['payments', `💳 ${t('loans.detail.payments')}`], ['ops', `⚙️ ${t('loans.detail.operations')}`]].map(([k, l]) => (
            <div key={k} className={`modal-tab${tab === k ? ' active' : ''}`} onClick={() => setTab(k)}>{l}</div>
          ))}
        </div>

        {/* Schedule tab */}
        {tab === 'schedule' && (
          <div className="modal-tab-content" style={{ padding: 0 }}>
            <table className="data-table">
              <thead><tr><th>Kỳ</th><th>Đến hạn</th><th>Gốc</th><th>Lãi</th><th>Tổng</th><th>Còn lại</th><th>Trạng thái</th></tr></thead>
              <tbody>
                {!schedule.length
                  ? <tr><td colSpan={7} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>Chưa có lịch</p></div></td></tr>
                  : schedule.map(s => (
                    <tr key={s.installmentNo} className={s.status === 'Overdue' ? 'row-overdue' : s.status === 'Paid' ? 'row-paid' : ''}>
                      <td className="text-center">{s.installmentNo}</td>
                      <td>{formatDate(s.dueDate)}</td>
                      <td>{formatCurrency(s.principalDue)}</td>
                      <td>{formatCurrency(s.interestDue)}</td>
                      <td><strong>{formatCurrency(s.principalDue + s.interestDue)}</strong></td>
                      <td>{formatCurrency(s.remainingDueAmount)}</td>
                      <td><StatusBadge status={s.status} /></td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Payments tab */}
        {tab === 'payments' && (
          <div className="modal-tab-content" style={{ padding: 0 }}>
            <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'flex-end' }}>
              <Button variant="outline" onClick={() => openReversalModal(payments[0]?.id ?? '')} style={{ fontSize: '0.8rem' }} disabled={!payments.length}>
                ↩️ Yêu cầu đảo bút toán
              </Button>
            </div>
            <table className="data-table">
              <thead><tr><th>Ngày thu</th><th>Số tiền</th><th>Phương thức</th><th>Ghi chú</th><th>Thao tác</th></tr></thead>
              <tbody>
                {!payments.length
                  ? <tr><td colSpan={5} className="empty-row"><div className="empty-state"><span className="empty-icon">💳</span><p>Chưa có thanh toán</p></div></td></tr>
                  : payments.map((p, i) => (
                    <tr key={p.id ?? i}>
                      <td>{formatDate(p.paymentDate)}</td>
                      <td><strong>{formatCurrency(p.amount)}</strong></td>
                      <td>{p.paymentMethod === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản'}</td>
                      <td><span className="cell-sub">{p.remarks || '—'}</span></td>
                      <td>
                        <Button
                          variant="ghost"
                          onClick={() => openReversalModal(p.id)}
                          disabled={!p.id}
                          style={{ fontSize: '0.78rem', padding: '0.3rem 0.65rem' }}
                        >
                          ↩️ Đảo bút toán
                        </Button>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Ops tab */}
        {tab === 'ops' && (
          <div className="modal-tab-content" style={{ padding: '1.25rem' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem' }}>
              <Button variant="outline" onClick={() => setSubModal('restructure')}>🔄 Cơ cấu lại nợ</Button>
              <Button variant="outline" onClick={() => setSubModal('reschedule')}>📅 Dời ngày trả nợ</Button>
              <Button variant="outline" onClick={() => setSubModal('adjust')}>📝 Điều chỉnh / Miễn giảm</Button>
              <Button variant="outline" onClick={() => setSubModal('earlysettle')}>🏁 Tất toán sớm</Button>
              <Button variant="danger"  onClick={() => setSubModal('writeoff')}>🗑 Xóa Nợ</Button>
            </div>
            <div style={{ marginTop: '1.25rem', padding: '1rem', background: 'var(--bg-muted)', borderRadius: '10px', fontSize: '0.83rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
              💡 <strong>Maker-Checker:</strong> Cơ cấu, dời ngày và điều chỉnh sẽ vào <b>Hàng đợi phê duyệt</b> để cấp Quản lý xét duyệt. Tất toán sớm có hiệu lực ngay.
            </div>
          </div>
        )}
      </Modal>
    </>
  );
}
