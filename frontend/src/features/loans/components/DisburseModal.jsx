import { useEffect, useState } from 'react';
import { loanApi } from '../api/loanApi';
import { today } from '../../../utils/format';
import Modal from '../../../components/Modal';
import { useToast } from '../../../context/ToastContext';
import { ewalletApi } from '../../ewallet/api/ewalletApi';

export function DisburseModal({ loanId, customerId, onClose, onDone }) {
  const { toast } = useToast();
  const [date, setDate] = useState(today());
  const [method, setMethod] = useState('BankTransfer');
  const [eWalletId, setEWalletId] = useState('');
  const [wallets, setWallets] = useState([]);
  const [loading, setLoading] = useState(false);
  const [walletLoading, setWalletLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (method !== 'EWallet' || !customerId) return;

    setWalletLoading(true);
    ewalletApi.listByCustomer(customerId)
      .then(data => setWallets((Array.isArray(data) ? data : []).filter(w => w.status === 'Active')))
      .catch(e => setError(e.message))
      .finally(() => setWalletLoading(false));
  }, [method, customerId]);

  const submit = async () => {
    setError('');
    setLoading(true);

    try {
      if (method === 'EWallet') {
        if (!eWalletId) throw new Error('Select an active e-wallet.');
        await loanApi.disburseToEWallet(loanId, { eWalletId: Number(eWalletId) });
      } else {
        await loanApi.disburse(loanId, { disbursementDate: date });
      }

      toast('Disbursement completed.');
      onDone();
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title="Confirm disbursement"
      onClose={onClose}
      actions={(
        <>
          <button className="btn btn-ghost" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" onClick={submit} disabled={loading}>
            {loading ? <><span className="spinner" />...</> : 'Disburse'}
          </button>
        </>
      )}
    >
      <div className="form-group">
        <label>Disbursement method</label>
        <select value={method} onChange={e => setMethod(e.target.value)}>
          <option value="BankTransfer">Bank transfer / NAPAS</option>
          <option value="EWallet">E-Wallet (MoMo/VNPay)</option>
        </select>
      </div>

      {method === 'BankTransfer' && (
        <div className="form-group">
          <label>Disbursement date <span className="required">*</span></label>
          <input type="date" value={date} onChange={e => setDate(e.target.value)} />
        </div>
      )}

      {method === 'EWallet' && (
        <div className="form-group">
          <label>Linked wallet</label>
          <select value={eWalletId} onChange={e => setEWalletId(e.target.value)} disabled={walletLoading}>
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

      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}
