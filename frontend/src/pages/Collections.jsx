import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { collectionsApi } from '../features/collections/api/collectionsApi';
import Modal from '../components/Modal';
import { Button } from '../components/ui/Button';
import { Card, CardBody, CardHeader } from '../components/ui/Card';

const ACTION_TYPES = ['Call', 'Visit', 'SMS', 'Letter', 'Legal'];
const OUTCOMES = [
  { value: 'Contacted', label: 'Da lien he' },
  { value: 'NoAnswer', label: 'Khong nghe may' },
  { value: 'PromiseToPay', label: 'Hen tra no' },
  { value: 'Refused', label: 'Tu choi' },
  { value: 'Escalated', label: 'Chuyen cap tren' },
];

const PRIORITY_LABELS = {
  High: 'Cao',
  Medium: 'Trung binh',
  Low: 'Thap',
};

const PROMISE_LABELS = {
  Broken: 'That bai',
  Kept: 'Da tra',
  Pending: 'Cho',
};

function PriorityPill({ priority }) {
  return (
    <span className={`priority-pill priority-pill-${String(priority || '').toLowerCase()}`}>
      {PRIORITY_LABELS[priority] || priority || '-'}
    </span>
  );
}

function PromisePill({ date, status }) {
  if (!date) return '-';
  const tone = status === 'Broken' ? 'danger' : status === 'Kept' ? 'success' : 'warning';

  return (
    <span className={`promise-pill promise-pill-${tone}`}>
      {date} ({PROMISE_LABELS[status] || status || 'Cho'})
    </span>
  );
}

function SummaryMetric({ label, value, alert = false }) {
  return (
    <div className={`summary-metric${alert ? ' summary-metric-alert' : ''}`}>
      <span className="summary-metric-label">{label}</span>
      <strong className="summary-metric-value">{value}</strong>
    </div>
  );
}

export default function Collections() {
  const { can } = useAuth();
  const [queue, setQueue] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedLoan, setSelectedLoan] = useState(null);
  const [history, setHistory] = useState([]);
  const [showActionModal, setShowActionModal] = useState(false);
  const [showHistoryModal, setShowHistoryModal] = useState(false);
  const [actionForm, setActionForm] = useState({
    actionType: 'Call',
    actionDate: new Date().toISOString().split('T')[0],
    outcome: 'Contacted',
    nextFollowUpDate: '',
    promiseDate: '',
    promiseAmount: '',
    notes: '',
  });

  const isManagerView = can.viewAllCollections;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = isManagerView
        ? await collectionsApi.getAllQueue()
        : await collectionsApi.getMyQueue();
      setQueue(data);

      if (isManagerView) {
        const nextSummary = await collectionsApi.getSummary();
        setSummary(nextSummary);
      }
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  }, [isManagerView]);

  useEffect(() => {
    load();
  }, [load]);

  const resetActionForm = () => {
    setActionForm({
      actionType: 'Call',
      actionDate: new Date().toISOString().split('T')[0],
      outcome: 'Contacted',
      nextFollowUpDate: '',
      promiseDate: '',
      promiseAmount: '',
      notes: '',
    });
  };

  const openActionModal = (loan) => {
    setSelectedLoan(loan);
    resetActionForm();
    setShowActionModal(true);
  };

  const openHistory = async (loan) => {
    setSelectedLoan(loan);
    setShowHistoryModal(true);
    const data = await collectionsApi.getHistory(loan.loanId);
    setHistory(data);
  };

  const submitAction = async (event) => {
    event.preventDefault();

    try {
      const payload = {
        actionType: actionForm.actionType,
        actionDate: actionForm.actionDate,
        outcome: actionForm.outcome,
        nextFollowUpDate: actionForm.nextFollowUpDate || null,
        notes: actionForm.notes || null,
      };

      if (actionForm.outcome === 'PromiseToPay') {
        payload.promiseDate = actionForm.promiseDate;
        payload.promiseAmount = parseFloat(actionForm.promiseAmount);
      }

      await collectionsApi.addAction(selectedLoan.loanId, payload);
      setShowActionModal(false);
      setSelectedLoan(null);
      load();
    } catch (error) {
      alert(error.message || 'Loi');
    }
  };

  const fmt = (n) => Number(n || 0).toLocaleString('vi-VN');

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>Thu hoi no</h2>
          <p className="subtitle">
            {isManagerView
              ? 'Quan ly tat ca khoan vay qua han.'
              : 'Danh sach khoan vay duoc giao.'}
          </p>
        </div>
      </div>

      {summary && (
        <div className="collections-summary">
          <SummaryMetric label="Tong qua han" value={summary.totalOverdueLoans} />
          <SummaryMetric label="Da phan cong" value={summary.totalAssigned} />
          <SummaryMetric label="Chua phan cong" value={summary.totalUnassigned} />
          <SummaryMetric label="Lien he hom nay" value={summary.actionsToday} />
          <SummaryMetric label="Hen tra cho" value={summary.promisesPending} />
          <SummaryMetric label="Hen tra that bai" value={summary.promisesBroken} alert />
        </div>
      )}

      <Card hoverable>
        <CardHeader
          title="Collection queue"
          subtitle={isManagerView ? 'Manager view across all overdue loans.' : 'Collector workbench for assigned cases.'}
          action={<span className="dashboard-date-chip">{queue.length} khoan vay</span>}
        />
        <CardBody className="table-shell">
          {loading ? (
            <div className="loading-shimmer" />
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Ma khoan vay</th>
                  <th>Khach hang</th>
                  <th>SDT</th>
                  <th>Du no qua han</th>
                  <th>DPD</th>
                  <th>Uu tien</th>
                  <th>Collector</th>
                  <th>Ket qua cuoi</th>
                  <th>Hen tra</th>
                  <th>Thao tac</th>
                </tr>
              </thead>
              <tbody>
                {queue.map((item) => (
                  <tr key={item.loanId}>
                    <td><span className="code-badge">{item.loanAccountNo}</span></td>
                    <td><div className="cell-main">{item.customerName}</div></td>
                    <td><span className="cell-sub">{item.customerPhone}</span></td>
                    <td>{fmt(item.overdueAmount)}</td>
                    <td><span className="code-badge">{item.dpd} ngay</span></td>
                    <td><PriorityPill priority={item.priority} /></td>
                    <td>{item.assignedCollector || <span className="cell-sub">Chua phan</span>}</td>
                    <td>{OUTCOMES.find((o) => o.value === item.lastActionOutcome)?.label || '-'}</td>
                    <td><PromisePill date={item.lastPromiseDate} status={item.lastPromiseStatus} /></td>
                    <td>
                      <div className="collections-actions">
                        {can.logCollectionAction && (
                          <Button size="sm" onClick={() => openActionModal(item)}>
                            Ghi nhan
                          </Button>
                        )}
                        <Button size="sm" variant="ghost" onClick={() => openHistory(item)}>
                          Lich su
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
                {queue.length === 0 && (
                  <tr>
                    <td colSpan="10" className="empty-row">
                      <div className="empty-state">
                        <span className="empty-icon">0</span>
                        <p>Khong co khoan vay qua han nao.</p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          )}
        </CardBody>
      </Card>

      {showActionModal && selectedLoan && (
        <Modal
          title={`Ghi nhan lien he - ${selectedLoan.loanAccountNo}`}
          onClose={() => setShowActionModal(false)}
          actions={
            <>
              <Button variant="ghost" type="button" onClick={() => setShowActionModal(false)}>
                Huy
              </Button>
              <Button type="submit" form="collection-action-form">
                Luu
              </Button>
            </>
          }
        >
          <form id="collection-action-form" onSubmit={submitAction}>
            <div className="form-group">
              <label>Hinh thuc lien he</label>
              <select
                value={actionForm.actionType}
                onChange={(e) => setActionForm((current) => ({ ...current, actionType: e.target.value }))}
              >
                {ACTION_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Ngay lien he</label>
              <input
                type="date"
                value={actionForm.actionDate}
                onChange={(e) => setActionForm((current) => ({ ...current, actionDate: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>Ket qua</label>
              <select
                value={actionForm.outcome}
                onChange={(e) => setActionForm((current) => ({ ...current, outcome: e.target.value }))}
              >
                {OUTCOMES.map((outcome) => (
                  <option key={outcome.value} value={outcome.value}>{outcome.label}</option>
                ))}
              </select>
            </div>

            {actionForm.outcome === 'PromiseToPay' && (
              <>
                <div className="form-group">
                  <label>Ngay hen tra</label>
                  <input
                    type="date"
                    value={actionForm.promiseDate}
                    onChange={(e) => setActionForm((current) => ({ ...current, promiseDate: e.target.value }))}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>So tien hen tra</label>
                  <input
                    type="number"
                    value={actionForm.promiseAmount}
                    onChange={(e) => setActionForm((current) => ({ ...current, promiseAmount: e.target.value }))}
                    required
                  />
                </div>
              </>
            )}

            <div className="form-group">
              <label>Ngay follow-up tiep</label>
              <input
                type="date"
                value={actionForm.nextFollowUpDate}
                onChange={(e) => setActionForm((current) => ({ ...current, nextFollowUpDate: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label>Ghi chu</label>
              <textarea
                rows="3"
                value={actionForm.notes}
                onChange={(e) => setActionForm((current) => ({ ...current, notes: e.target.value }))}
                placeholder="Ghi chu chi tiet..."
              />
            </div>
          </form>
        </Modal>
      )}

      {showHistoryModal && (
        <Modal
          title={`Lich su thu hoi - ${selectedLoan?.loanAccountNo || ''}`}
          onClose={() => setShowHistoryModal(false)}
          size="lg"
        >
          <div className="collections-history-scroll">
            {history.length === 0 ? (
              <p className="history-empty">Chua co lich su lien he.</p>
            ) : (
              <div className="timeline">
                {history.map((item) => (
                  <div key={item.id} className="timeline-item">
                    <div className="timeline-header">
                      <strong>{item.actionType} - {OUTCOMES.find((o) => o.value === item.outcome)?.label || item.outcome}</strong>
                      <small>{item.actionDate}</small>
                    </div>
                    <div className="timeline-meta">Collector: {item.collectorName}</div>
                    {item.promiseDate && (
                      <div className="timeline-detail">
                        Hen tra: {item.promiseDate} - {fmt(item.promiseAmount)} - <PromisePill date={item.promiseDate} status={item.promiseStatus} />
                      </div>
                    )}
                    {item.notes && <div className="timeline-note">"{item.notes}"</div>}
                    {item.nextFollowUpDate && <div className="timeline-detail">Follow-up: {item.nextFollowUpDate}</div>}
                  </div>
                ))}
              </div>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}
