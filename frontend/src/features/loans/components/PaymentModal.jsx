import { useEffect, useState } from 'react';
import { loanApi } from '../api/loanApi';
import { today } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { useToast } from '../../../context/ToastContext';
import { ewalletApi } from '../../ewallet/api/ewalletApi';

export function PaymentModal({ loanId, customerId, onClose, onDone }) {
  const { toast } = useToast();
  const [form, setForm] = useState({
    amount: '',
    paymentDate: today(),
    paymentMethod: 'Cash',
    remarks: '',
    eWalletId: '',
  });
  const [wallets, setWallets] = useState([]);
  const [loading, setLoading] = useState(false);
  const [walletLoading, setWalletLoading] = useState(false);
  const [error, setError] = useState('');

  const set = (key, value) => setForm(current => ({ ...current, [key]: value }));

  useEffect(() => {
    if (form.paymentMethod !== 'EWallet' || !customerId) return;

    setWalletLoading(true);
    ewalletApi.listByCustomer(customerId)
      .then(data => setWallets((Array.isArray(data) ? data : []).filter(w => w.status === 'Active')))
      .catch(e => setError(e.message))
      .finally(() => setWalletLoading(false));
  }, [form.paymentMethod, customerId]);

  const submit = async () => {
    setError('');
    setLoading(true);

    try {
      if (form.paymentMethod === 'EWallet') {
        if (!form.eWalletId) throw new Error('Select an active e-wallet.');
        await loanApi.payFromEWallet(loanId, { eWalletId: Number(form.eWalletId), amount: Number(form.amount) });
      } else {
        const { eWalletId, ...payload } = form;
        await loanApi.pay(loanId, { ...payload, amount: Number(form.amount) });
      }

      toast('Payment recorded successfully.');
      onDone();
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title="Record payment"
      onClose={onClose}
      actions={(
        <>
          <button className="btn btn-ghost" onClick={onClose}>Cancel</button>
          <button className="btn btn-success" onClick={submit} disabled={loading}>
            {loading ? <><span className="spinner" />...</> : 'Record'}
          </button>
        </>
      )}
    >
      <div className="form-row">
        <div className="form-group">
          <label>Amount (VND) <span className="required">*</span></label>
          <input type="number" value={form.amount} onChange={e => set('amount', e.target.value)} placeholder="0" />
          <p className="text-xs text-blue-400 mt-1">
            Overpayments are moved to suspense for later processing.
          </p>
        </div>
        <div className="form-group">
          <label>Payment date <span className="required">*</span></label>
          <input type="date" value={form.paymentDate} onChange={e => set('paymentDate', e.target.value)} disabled={form.paymentMethod === 'EWallet'} />
        </div>
      </div>

      <div className="form-group">
        <label>Payment method</label>
        <select value={form.paymentMethod} onChange={e => set('paymentMethod', e.target.value)}>
          <option value="Cash">Cash</option>
          <option value="BankTransfer">Bank transfer</option>
          <option value="EWallet">E-Wallet (MoMo/VNPay)</option>
        </select>
      </div>

      {form.paymentMethod === 'EWallet' && (
        <div className="form-group">
          <label>Linked wallet</label>
          <select value={form.eWalletId} onChange={e => set('eWalletId', e.target.value)} disabled={walletLoading}>
            <option value="">{walletLoading ? 'Loading wallets...' : 'Select active wallet'}</option>
            {wallets.map(wallet => (
              <option key={wallet.id} value={wallet.id}>
                {wallet.walletProvider} - {wallet.walletAccountId} - {wallet.walletUserName || 'Active'}
              </option>
            ))}
          </select>
          {!walletLoading && wallets.length === 0 && (
            <p className="cell-sub">No active wallet. Link MoMo/VNPay in Customers first.</p>
          )}
        </div>
      )}

      <div className="form-group">
        <label>Remarks</label>
        <input value={form.remarks} onChange={e => set('remarks', e.target.value)} placeholder="Reference, note..." disabled={form.paymentMethod === 'EWallet'} />
      </div>

      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}
