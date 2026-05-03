import { useState, useEffect, useCallback, useMemo } from 'react';
import { customerApi } from '../features/customers/api/customerApi';
import { formatDate, today } from '../utils/format';
import { StatusBadge } from '../components/Badge';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import Modal from '../components/Modal';
import Pagination from '../components/Pagination';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { useT } from '../i18n/useT';

function EkycModal({ customer, onClose, onVerified }) {
  const { toast } = useToast();
  const { t } = useT();
  const [step, setStep] = useState(0); // 0: init, 1: scanning face, 2: reading nfc/ocr, 3: verifying, 4: done
  const [result, setResult] = useState(null);

  useEffect(() => {
    let mounted = true;

    const run = async () => {
      if (step === 1) {
        setTimeout(() => { if (mounted) setStep(2); }, 1500);
      } else if (step === 2) {
        setTimeout(() => { if (mounted) setStep(3); }, 1500);
      } else if (step === 3) {
        try {
          const res = await customerApi.verifyEkyc(customer.id);
          if (!mounted) return;
          setResult(res);
          setStep(4);
          if (res.status === 'Verified') {
            toast(t('customers.ekycSuccess', { faceMatchScore: res.faceMatchScore, livenessScore: res.livenessScore }));
            onVerified();
          } else {
            toast(t('customers.ekycFailed', { status: res.status }), 'warning');
          }
        } catch (e) {
          if (!mounted) return;
          toast(e.message, 'error');
          onClose();
        }
      }
    };

    run();
    return () => { mounted = false; };
  }, [step, customer.id, toast, onVerified, onClose]);

  return (
    <Modal
      title={t('customers.ekycTitle')}
      onClose={onClose}
      actions={step !== 4 ? null : <Button variant="primary" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div style={{ textAlign: 'center', padding: '1rem 0' }}>
        <h3 style={{ marginBottom: '0.5rem' }}>{customer.fullName}</h3>
        <p style={{ color: 'var(--text-muted)' }}>{t('customers.idNumber')}: <span className="mono">{customer.idNumber}</span></p>

        <div style={{ margin: '2rem auto', width: '200px', height: '200px', border: '2px dashed var(--border)', borderRadius: '1rem', position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--bg-elevated)', overflow: 'hidden' }}>
          {step === 0 && <Button variant="primary" onClick={() => setStep(1)}>{t('customers.ekycStart')}</Button>}
          {step > 0 && step < 4 && (
            <div style={{ textAlign: 'center' }}>
              <div className="spinner" style={{ width: '40px', height: '40px', margin: '0 auto 1rem' }} />
              <div style={{ fontWeight: 500, color: 'var(--primary)' }}>
                {step === 1 ? t('customers.ekycStepOne') : step === 2 ? t('customers.ekycStepTwo') : t('customers.ekycStepThree')}
              </div>
            </div>
          )}
          {step === 4 && result && (
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>{result.status === 'Verified' ? '✅' : '⚠️'}</div>
              <div style={{ fontWeight: 600, color: result.status === 'Verified' ? 'var(--green)' : 'var(--amber)' }}>{result.status}</div>
            </div>
          )}
        </div>

        {step === 4 && result && (
          <div style={{ textAlign: 'left', background: 'var(--bg-subtle)', padding: '1rem', borderRadius: 'var(--radius)', fontSize: '0.9rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-muted)' }}>{t('customers.livenessScore')}:</span>
              <strong style={{ color: result.livenessScore >= 95 ? 'var(--green)' : 'var(--red)' }}>{result.livenessScore}%</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
              <span style={{ color: 'var(--text-muted)' }}>{t('customers.faceMatch')}:</span>
              <strong style={{ color: result.faceMatchScore >= 80 ? 'var(--green)' : 'var(--amber)' }}>{result.faceMatchScore}%</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--text-muted)' }}>{t('customers.ocrConfidence')}:</span>
              <strong style={{ color: result.ocrConfidence >= 85 ? 'var(--green)' : 'var(--amber)' }}>{result.ocrConfidence}%</strong>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}

function EWalletModal({ customer, onClose }) {
  const { toast } = useToast();
  const [wallets, setWallets] = useState([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [linkForm, setLinkForm] = useState({ walletProvider: 'MoMo', walletAccountId: customer.phone || '' });
  const [otpForm, setOtpForm] = useState({ linkingId: '', otpCode: '' });
  const [error, setError] = useState('');

  const loadWallets = useCallback(async () => {
    setLoading(true);
    try {
      const data = await customerApi.wallets(customer.id);
      setWallets(Array.isArray(data) ? data : []);
      const pending = (Array.isArray(data) ? data : []).find(w => w.status === 'PendingVerification');
      if (pending) setOtpForm(f => ({ ...f, linkingId: String(pending.id) }));
    } catch (e) {
      setError(e.message || 'Cannot load wallets.');
    } finally {
      setLoading(false);
    }
  }, [customer.id]);

  useEffect(() => { loadWallets(); }, [loadWallets]);

  const linkWallet = async () => {
    setError('');
    setSubmitting(true);
    try {
      const linked = await customerApi.linkWallet(customer.id, linkForm);
      toast('Wallet link request sent. Enter OTP from provider.', 'success');
      setOtpForm({ linkingId: String(linked.id), otpCode: '' });
      await loadWallets();
    } catch (e) {
      setError(e.message);
    } finally {
      setSubmitting(false);
    }
  };

  const verifyOtp = async () => {
    setError('');
    setSubmitting(true);
    try {
      await customerApi.verifyWalletOtp(customer.id, { linkingId: Number(otpForm.linkingId), otpCode: otpForm.otpCode });
      toast('Wallet verified.', 'success');
      setOtpForm({ linkingId: '', otpCode: '' });
      await loadWallets();
    } catch (e) {
      setError(e.message);
    } finally {
      setSubmitting(false);
    }
  };

  const unlinkWallet = async (walletId) => {
    setError('');
    setSubmitting(true);
    try {
      await customerApi.unlinkWallet(customer.id, walletId);
      toast('Wallet unlinked.', 'success');
      await loadWallets();
    } catch (e) {
      setError(e.message);
    } finally {
      setSubmitting(false);
    }
  };

  const activeWallets = wallets.filter(w => w.status === 'Active');

  return (
    <Modal
      title={`E-Wallet - ${customer.fullName}`}
      onClose={onClose}
      actions={<Button variant="ghost" onClick={onClose}>Close</Button>}
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div className="form-row">
          <Input type="select" label="Provider" value={linkForm.walletProvider}
            onChange={e => setLinkForm(f => ({ ...f, walletProvider: e.target.value }))}>
            <option value="MoMo">MoMo</option>
            <option value="VNPay">VNPay</option>
          </Input>
          <Input label="Wallet phone / ID" value={linkForm.walletAccountId}
            onChange={e => setLinkForm(f => ({ ...f, walletAccountId: e.target.value }))}
            placeholder="0901234567" />
        </div>
        <Button variant="primary" onClick={linkWallet} disabled={submitting || !linkForm.walletAccountId.trim()}>
          Link wallet
        </Button>

        <div className="form-row">
          <Input type="select" label="Pending link" value={otpForm.linkingId}
            onChange={e => setOtpForm(f => ({ ...f, linkingId: e.target.value }))}>
            <option value="">Select pending wallet</option>
            {wallets.filter(w => w.status === 'PendingVerification').map(w => (
              <option key={w.id} value={w.id}>{w.walletProvider} - {w.walletAccountId}</option>
            ))}
          </Input>
          <Input label="OTP" value={otpForm.otpCode}
            onChange={e => setOtpForm(f => ({ ...f, otpCode: e.target.value }))}
            placeholder="123456" />
        </div>
        <Button variant="outline" onClick={verifyOtp} disabled={submitting || !otpForm.linkingId || !otpForm.otpCode.trim()}>
          Verify OTP
        </Button>

        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr><th>Provider</th><th>Wallet</th><th>Name</th><th>Status</th><th>Action</th></tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={5} className="empty-row"><div className="loading-shimmer" /></td></tr>
              ) : wallets.length === 0 ? (
                <tr><td colSpan={5} className="empty-row"><div className="empty-state">No wallets linked.</div></td></tr>
              ) : wallets.map(w => (
                <tr key={w.id}>
                  <td><span className="code-badge">{w.walletProvider}</span></td>
                  <td>{w.walletAccountId}</td>
                  <td>{w.walletUserName || '-'}</td>
                  <td><StatusBadge status={w.status} /></td>
                  <td>
                    <Button size="sm" variant="ghost" onClick={() => unlinkWallet(w.id)} disabled={submitting}>
                      Unlink
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {activeWallets.length > 0 && (
          <p className="cell-sub">Active wallets are available for loan payment and e-wallet disbursement.</p>
        )}
        {error && <div className="error-msg">{error}</div>}
      </div>
    </Modal>
  );
}

function CustomerModal({ customer, onClose, onSaved }) {
  const { toast } = useToast();
  const { t } = useT();
  const [form, setForm] = useState({
    fullName: customer?.fullName || '', idNumber: customer?.idNumber || '',
    phone: customer?.phone || '', dateOfBirth: customer?.dateOfBirth?.substring(0,10) || '',
    address: customer?.address || '',
    bankAccountNumber: customer?.bankAccountNumber || '',
    bankCode: customer?.bankCode || ''
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const set = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = async () => {
    setError(''); setLoading(true);
    try {
      if (customer) await customerApi.update(customer.id, form);
      else          await customerApi.create(form);
      toast(customer ? t('customers.updateSuccess') : t('customers.createSuccess'));
      onSaved();
    } catch (e) { setError(e.message); }
    finally { setLoading(false); }
  };

  return (
    <Modal
      title={customer ? t('customers.editCustomer') : t('customers.createCustomer')}
      onClose={onClose}
      actions={<>
        <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        <Button variant="primary" onClick={handleSubmit} loading={loading}>{t('common.save')}</Button>
      </>}
    >
      <div className="form-row">
        <Input label={`${t('customers.fullName')} *`} value={form.fullName} onChange={e => set('fullName', e.target.value)} placeholder={t('customers.fullNamePlaceholder')} />
        <Input label={`${t('customers.idNumberLabel')} *`} value={form.idNumber} onChange={e => set('idNumber', e.target.value)} placeholder="079100000001" disabled={!!customer} />
      </div>
      <div className="form-row">
        <Input label={`${t('customers.phone')} *`} value={form.phone} onChange={e => set('phone', e.target.value)} placeholder="0901234567" />
        <Input type="date" label={t('customers.dateOfBirth')} value={form.dateOfBirth} onChange={e => set('dateOfBirth', e.target.value)} />
      </div>
      <Input label={t('customers.address')} value={form.address} onChange={e => set('address', e.target.value)} placeholder={t('customers.addressPlaceholder')} />
      <div className="form-row">
        <Input label={t('customers.bankAccountNumber')} value={form.bankAccountNumber} onChange={e => set('bankAccountNumber', e.target.value)} placeholder="1903..." />
        <Input type="select" label={t('customers.bankCode')} value={form.bankCode} onChange={e => set('bankCode', e.target.value)}>
           <option value="">{t('customers.selectBank')}</option>
           <option value="NAPAS">{t('customers.bankInternal')}</option>
           <option value="VCB">Vietcombank</option>
           <option value="TCB">Techcombank</option>
           <option value="MB">MB Bank</option>
        </Input>
      </div>
      {customer?.creditScore && (
        <div className="form-group" style={{marginTop:'0.5rem', padding:'0.75rem', background:'var(--bg-elevated)', borderRadius:'var(--radius)'}}>
           <div style={{fontSize:'0.85rem', color:'var(--text-muted)'}}>{t('customers.creditScore')}</div>
           <div style={{fontWeight:600, color: customer.creditStatus==='Poor'?'var(--red)':customer.creditStatus==='Excellent'?'var(--green)':'var(--amber)'}}>
             {customer.creditScore} — {customer.creditStatus}
           </div>
        </div>
      )}
      {error && <div className="error-msg">{error}</div>}
    </Modal>
  );
}

export default function Customers() {
  const { can } = useAuth();
  const { t } = useT();
  const [data, setData] = useState(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState(null); // null | { type: 'edit'|'ekyc'|'wallet', customer? }
  const [confirm, setConfirm] = useState(null);
  const { toast } = useToast();

  const load = useCallback(async (p = page, q = search) => {
    setLoading(true);
    try {
      const res = await customerApi.list({ page: p, pageSize: 15, search: q });
      setData(res.items ? res : { items: Array.isArray(res) ? res : [], totalPages:1, page:1 });
    } catch (e) {
      toast(e.message || t('customers.loadError'), 'error');
    }
    finally { setLoading(false); }
  }, [page, search, t, toast]);

  useEffect(() => { load(1, ''); }, []);

  const handleSearch = () => { setPage(1); load(1, search); };
  const handlePage   = (p) => { setPage(p); load(p, search); };
  const handleSaved  = () => { setModal(null); load(page, search); };

  const handleCheckCredit = (c) => {
    setConfirm({
      msg: t('customers.checkCreditConfirm', { fullName: c.fullName }),
      action: async () => {
        try {
          await customerApi.checkCredit(c.id);
          toast(t('customers.creditUpdated'));
          load(page, search);
        } catch(e) {
          toast(e.message, 'error');
        }
      }
    });
  };

  const handleVerifyEkyc = (c) => {
    setModal({ type: 'ekyc', customer: c });
  };

  const runConfirm = async () => {
    await confirm.action();
    setConfirm(null);
  };

  const summary = useMemo(() => {
    const items = data?.items || [];
    const verifiedCount = items.filter(c => c.ekycStatus === 'Verified').length;
    const pendingCount = items.filter(c => c.ekycStatus === 'Pending').length;
    const scored = items.filter(c => Number.isFinite(Number(c.creditScore)));
    const avgCreditScore = scored.length
      ? scored.reduce((sum, c) => sum + Number(c.creditScore || 0), 0) / scored.length
      : 0;
    return {
      total: items.length,
      verifiedCount,
      pendingCount,
      avgCreditScore,
    };
  }, [data]);

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>{t('customers.title')}</h2>
          <p className="subtitle">{t('customers.subtitle')}</p>
        </div>
        {can.createCustomer && <Button variant="primary" onClick={() => setModal({ type: 'edit', customer: null })}>{t('customers.createCustomer')}</Button>}
      </div>

      <div className="customer-summary">
        <div className="customer-summary-card">
          <span>{t('customers.summaryTotal')}</span>
          <strong>{summary.total}</strong>
        </div>
        <div className="customer-summary-card">
          <span>{t('customers.summaryVerified')}</span>
          <strong>{summary.verifiedCount}</strong>
        </div>
        <div className="customer-summary-card">
          <span>{t('customers.summaryPending')}</span>
          <strong>{summary.pendingCount}</strong>
        </div>
        <div className="customer-summary-card">
          <span>{t('customers.summaryAvgScore')}</span>
          <strong>{summary.avgCreditScore ? summary.avgCreditScore.toFixed(0) : '0'}</strong>
        </div>
      </div>

      <div className="filter-bar">
        <div style={{ flex: 1, minWidth: '200px' }}>
          <Input placeholder={t('customers.searchPlaceholder')}
            value={search} onChange={e => setSearch(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleSearch()} />
        </div>
        <Button variant="outline" onClick={handleSearch}>{t('common.search')}</Button>
      </div>

      <Card>
        <div className="table-responsive">
        <table className="data-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>{t('customers.fullName')}</th>
              <th>{t('customers.idNumberLabel')}</th>
              <th>{t('customers.phone')}</th>
              <th>{t('customers.creditScore')}</th>
              <th>{t('customers.ekyc')}</th>
              <th>{t('common.status')}</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={8} className="empty-row"><div className="loading-shimmer" /></td></tr>
            ) : !data?.items?.length ? (
              <tr><td colSpan={8} className="empty-row"><div className="empty-state"><span className="empty-icon">📭</span><p>{t('common.noData')}</p></div></td></tr>
            ) : data.items.map(c => (
              <tr key={c.id}>
                <td><span className="code-badge">#{c.id}</span></td>
                <td><div className="cell-main">{c.fullName}</div><div className="cell-sub">{c.address}</div></td>
                <td><span className="mono">{c.idNumber}</span></td>
                <td>{c.phone}</td>
                <td>
                  {c.creditScore ? (
                    <div><strong style={{color: c.creditStatus==='Poor'?'var(--red)':c.creditStatus==='Excellent'?'var(--green)':'var(--amber)'}}>{c.creditScore}</strong> <span className="cell-sub">{c.creditStatus}</span></div>
                  ) : <span className="cell-sub">{t('customers.notChecked')}</span>}
                </td>
                <td>
                  {c.ekycStatus === 'Verified' ? <span style={{color: 'var(--green)', fontWeight: 'bold'}}>{t('customers.verified')}</span> :
                   c.ekycStatus === 'Pending' ? <span style={{color: 'var(--text-muted)'}}>- {t('customers.pending')} -</span> :
                   <span style={{color: 'var(--amber)'}}>{c.ekycStatus}</span>}
                </td>
                <td><StatusBadge status={c.status || 'Active'} /></td>
                <td>
                  {can.createCustomer && (
                    <div className="action-btns">
                      <Button size="sm" variant="outline" onClick={() => handleVerifyEkyc(c)}>{t('customers.ekycBtn')}</Button>
                      <Button size="sm" variant="outline" onClick={() => handleCheckCredit(c)}>{t('customers.cicBtn')}</Button>
                      <Button size="sm" variant="outline" onClick={() => setModal({ type: 'wallet', customer: c })}>Wallet</Button>
                      <Button size="sm" variant="outline" onClick={() => setModal({ type: 'edit', customer: c })}>{t('common.edit')}</Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
        <Pagination paged={data} onPage={handlePage} />
      </Card>

      {modal && modal.type === 'edit' && (
        <CustomerModal
          customer={modal.customer}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
      {modal && modal.type === 'ekyc' && (
        <EkycModal
          customer={modal.customer}
          onClose={() => { setModal(null); load(page, search); }}
          onVerified={() => { /* updated on close anyway */ }}
        />
      )}
      {modal && modal.type === 'wallet' && (
        <EWalletModal
          customer={modal.customer}
          onClose={() => setModal(null)}
        />
      )}
      {confirm && <ConfirmDialog message={confirm.msg} onConfirm={runConfirm} onCancel={() => setConfirm(null)} />}
    </div>
  );
}
