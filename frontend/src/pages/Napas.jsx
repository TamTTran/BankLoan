import { useEffect, useMemo, useState } from 'react';
import QRCode from 'qrcode';
import { Button } from '../components/ui/Button';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import { napasApi } from '../features/napas/api/napasApi';
import { formatCurrency, formatDate, formatDateTime, formatNumber } from '../utils/format';

const PAGE_SIZE = 20;

const TAB_CONFIG = [
  { key: 'transactions', label: 'Transactions', roles: ['Admin', 'Manager', 'LoanOfficer'] },
  { key: 'qr', label: 'VietQR', roles: ['Admin', 'Manager', 'LoanOfficer', 'Collector'] },
  { key: 'merchant', label: 'Merchant', roles: ['Admin', 'Manager'] },
  { key: 'reconciliation', label: 'Reconciliation', roles: ['Admin', 'Manager'] },
];

function dateInput(days = 0) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function statusTone(status) {
  const key = String(status || '').toUpperCase();
  if (key === 'SUCCESS' || key === 'COMPLETED' || key === 'ACTIVE') return { color: 'var(--green)', bg: 'rgba(19, 115, 51, 0.1)' };
  if (key === 'FAILED' || key === 'REVERSED') return { color: 'var(--red)', bg: 'rgba(195, 79, 79, 0.1)' };
  if (key === 'PENDING') return { color: 'var(--amber)', bg: 'rgba(201, 138, 27, 0.12)' };
  return { color: 'var(--text-secondary)', bg: 'var(--bg-muted)' };
}

function StatusPill({ status }) {
  const tone = statusTone(status);
  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      borderRadius: 999,
      padding: '0.18rem 0.6rem',
      fontSize: '0.75rem',
      fontWeight: 800,
      color: tone.color,
      background: tone.bg,
      whiteSpace: 'nowrap',
    }}>
      {status || 'N/A'}
    </span>
  );
}

function Metric({ label, value, tone }) {
  return (
    <div style={{
      border: '1px solid var(--border)',
      borderRadius: '8px',
      padding: '0.9rem 1rem',
      background: 'var(--surface)',
      minWidth: 0,
    }}>
      <div style={{ color: 'var(--text-muted)', fontSize: '0.76rem', fontWeight: 750 }}>{label}</div>
      <div style={{ color: tone || 'var(--navy)', fontSize: '1.25rem', fontWeight: 850, marginTop: '0.25rem' }}>{value}</div>
    </div>
  );
}

function Field({ label, value }) {
  return (
    <div style={{ minWidth: 0 }}>
      <div style={{ color: 'var(--text-muted)', fontSize: '0.74rem', fontWeight: 750, marginBottom: '0.2rem' }}>{label}</div>
      <div style={{ color: 'var(--text-primary)', fontSize: '0.9rem', fontWeight: 650, overflowWrap: 'anywhere' }}>{value ?? '-'}</div>
    </div>
  );
}

function EmptyRow({ colSpan, text = 'No records found.' }) {
  return (
    <tr>
      <td colSpan={colSpan} className="empty-row">
        <div className="empty-state">{text}</div>
      </td>
    </tr>
  );
}

function TransactionDetail({ transaction }) {
  if (!transaction) return null;
  return (
    <Card>
      <CardHeader title="Selected transaction" />
      <CardBody>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem' }}>
          <Field label="Reference ID" value={transaction.referenceId || transaction.napasReferenceId} />
          <Field label="Transaction ID" value={transaction.transactionId || transaction.napasTransactionId} />
          <Field label="Status" value={<StatusPill status={transaction.status || transaction.disbursementStatus} />} />
          <Field label="Amount" value={formatCurrency(transaction.amount)} />
          <Field label="Bank" value={transaction.bankCode} />
          <Field label="Account" value={transaction.accountNumber} />
          <Field label="Processed" value={formatDateTime(transaction.processedAt || transaction.confirmedAt)} />
          <Field label="Source" value={transaction.source} />
        </div>
        {(transaction.errorCode || transaction.errorMessage) && (
          <div style={{ marginTop: '1rem', color: 'var(--red)', fontSize: '0.88rem', fontWeight: 650 }}>
            {transaction.errorCode ? `${transaction.errorCode}: ` : ''}{transaction.errorMessage}
          </div>
        )}
      </CardBody>
    </Card>
  );
}

function TransactionsTab() {
  const { toast } = useToast();
  const [filters, setFilters] = useState({
    fromDate: dateInput(-7),
    toDate: dateInput(),
    status: '',
    transactionType: '',
    page: 1,
    pageSize: PAGE_SIZE,
  });
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [lookupType, setLookupType] = useState('reference');
  const [lookupValue, setLookupValue] = useState('');
  const [selected, setSelected] = useState(null);

  const totals = useMemo(() => {
    const items = data?.items || [];
    return {
      count: data?.totalCount ?? items.length,
      amount: items.reduce((sum, item) => sum + Number(item.amount || 0), 0),
      failed: items.filter(item => String(item.status || '').toUpperCase() === 'FAILED').length,
    };
  }, [data]);

  const runSearch = async (page = 1) => {
    const payload = { ...filters, page };
    setFilters(payload);
    setLoading(true);
    try {
      const result = await napasApi.searchTransactions(payload);
      setData(result);
      if (result?.items?.length) setSelected(result.items[0]);
    } catch (e) {
      toast(e.message || 'Failed to load NAPAS transactions', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    runSearch(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleLookup = async () => {
    if (!lookupValue.trim()) {
      toast('Enter a reference or transaction ID.', 'warning');
      return;
    }
    setLoading(true);
    try {
      const value = lookupValue.trim();
      const result = lookupType === 'transaction'
        ? await napasApi.getTransaction(value)
        : lookupType === 'disbursement'
          ? await napasApi.getDisbursementStatus(value)
          : await napasApi.getTransactionByReference(value);
      setSelected(result);
      toast('Transaction loaded.', 'success');
    } catch (e) {
      toast(e.message || 'Transaction lookup failed', 'error');
    } finally {
      setLoading(false);
    }
  };

  const totalPages = data?.totalPages || Math.max(1, Math.ceil((data?.totalCount || 0) / PAGE_SIZE));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.9rem' }}>
        <Metric label="Total records" value={formatNumber(totals.count)} />
        <Metric label="Listed amount" value={formatCurrency(totals.amount)} />
        <Metric label="Failed in page" value={formatNumber(totals.failed)} tone={totals.failed ? 'var(--red)' : 'var(--green)'} />
      </div>

      <Card>
        <CardHeader
          title="Transaction search"
          action={<Button variant="primary" onClick={() => runSearch(1)} loading={loading}>Search</Button>}
        />
        <CardBody>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.85rem', alignItems: 'end' }}>
            <Input label="From date" type="date" value={filters.fromDate} onChange={e => setFilters(f => ({ ...f, fromDate: e.target.value }))} />
            <Input label="To date" type="date" value={filters.toDate} onChange={e => setFilters(f => ({ ...f, toDate: e.target.value }))} />
            <Input label="Status" type="select" value={filters.status} onChange={e => setFilters(f => ({ ...f, status: e.target.value }))}>
              <option value="">All</option>
              <option value="SUCCESS">SUCCESS</option>
              <option value="PENDING">PENDING</option>
              <option value="FAILED">FAILED</option>
            </Input>
            <Input label="Type" type="select" value={filters.transactionType} onChange={e => setFilters(f => ({ ...f, transactionType: e.target.value }))}>
              <option value="">All</option>
              <option value="IBFT">IBFT</option>
              <option value="QR_PAYMENT">QR_PAYMENT</option>
            </Input>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Quick lookup" />
        <CardBody>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 180px), 1fr))', gap: '0.75rem', alignItems: 'end' }}>
            <Input label="Lookup by" type="select" value={lookupType} onChange={e => setLookupType(e.target.value)}>
              <option value="reference">Reference ID</option>
              <option value="transaction">Transaction ID</option>
              <option value="disbursement">Disbursement status</option>
            </Input>
            <Input label="Value" value={lookupValue} onChange={e => setLookupValue(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleLookup()} />
            <Button variant="outline" onClick={handleLookup} loading={loading}>Lookup</Button>
          </div>
        </CardBody>
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 320px), 1fr))', gap: '1rem' }}>
        <Card>
          <CardHeader title="Transactions" />
          <CardBody>
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>NAPAS Tx</th>
                    <th>Status</th>
                    <th>Amount</th>
                    <th>Bank</th>
                    <th>Processed</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr><td colSpan={7} className="empty-row"><div className="loading-shimmer" /></td></tr>
                  ) : !data?.items?.length ? (
                    <EmptyRow colSpan={7} />
                  ) : data.items.map(item => (
                    <tr key={`${item.referenceId}-${item.transactionId}`} style={{ cursor: 'pointer' }} onClick={() => setSelected(item)}>
                      <td><span className="code-badge">{item.referenceId}</span></td>
                      <td>{item.transactionId}</td>
                      <td><StatusPill status={item.status} /></td>
                      <td>{formatCurrency(item.amount)}</td>
                      <td>{item.bankCode}</td>
                      <td>{formatDateTime(item.processedAt)}</td>
                      <td><Button size="sm" variant="outline" onClick={(e) => { e.stopPropagation(); setSelected(item); }}>View</Button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem', gap: '1rem' }}>
              <span style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                Page {data?.page || filters.page} of {totalPages}
              </span>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <Button size="sm" variant="outline" disabled={(data?.page || filters.page) <= 1 || loading} onClick={() => runSearch((data?.page || filters.page) - 1)}>Previous</Button>
                <Button size="sm" variant="outline" disabled={(data?.page || filters.page) >= totalPages || loading} onClick={() => runSearch((data?.page || filters.page) + 1)}>Next</Button>
              </div>
            </div>
          </CardBody>
        </Card>
        <TransactionDetail transaction={selected} />
      </div>
    </div>
  );
}

function MerchantTab() {
  const { toast } = useToast();
  const [merchant, setMerchant] = useState(null);
  const [settings, setSettings] = useState(null);
  const [loading, setLoading] = useState(false);

  const loadMerchant = async () => {
    setLoading(true);
    try {
      const [merchantInfo, merchantSettings] = await Promise.all([
        napasApi.getMerchant(),
        napasApi.getMerchantSettings(),
      ]);
      setMerchant(merchantInfo);
      setSettings(merchantSettings);
    } catch (e) {
      toast(e.message || 'Failed to load NAPAS merchant', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMerchant();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 340px), 1fr))', gap: '1rem' }}>
      <Card>
        <CardHeader title="Merchant profile" action={<Button size="sm" variant="outline" onClick={loadMerchant} loading={loading}>Refresh</Button>} />
        <CardBody>
          {loading && !merchant ? <div className="loading-shimmer" style={{ height: 180 }} /> : (
            <div style={{ display: 'grid', gap: '1rem' }}>
              <Field label="Merchant ID" value={merchant?.merchantId} />
              <Field label="Merchant name" value={merchant?.merchantName} />
              <Field label="Merchant code" value={merchant?.merchantCode} />
              <Field label="Category" value={`${merchant?.categoryCode || '-'} ${merchant?.categoryName || ''}`} />
              <Field label="Status" value={<StatusPill status={merchant?.status} />} />
              <Field label="Settlement account" value={`${merchant?.bankCode || '-'} / ${merchant?.settlementAccount || '-'}`} />
              <Field label="Registered" value={formatDate(merchant?.registeredAt)} />
            </div>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Merchant settings" />
        <CardBody>
          {loading && !settings ? <div className="loading-shimmer" style={{ height: 180 }} /> : (
            <div style={{ display: 'grid', gap: '1rem' }}>
              <Field label="Single transaction limit" value={formatCurrency(settings?.singleTransactionLimit)} />
              <Field label="Daily limit" value={formatCurrency(settings?.dailyTransactionLimit)} />
              <Field label="Monthly limit" value={formatCurrency(settings?.monthlyTransactionLimit)} />
              <Field label="Channels" value={(settings?.enabledChannels || []).join(', ') || '-'} />
              <Field label="Allowed banks" value={(settings?.allowedBankCodes || []).join(', ') || '-'} />
              <Field label="Auto settlement" value={settings?.autoSettlement ? 'Enabled' : 'Disabled'} />
              <Field label="Settlement time" value={settings?.settlementTime} />
              <Field label="Callback" value={settings?.callbackEnabled ? settings?.callbackUrl : 'Disabled'} />
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

function ReconciliationTab() {
  const { toast } = useToast();
  const [date, setDate] = useState(dateInput());
  const [type, setType] = useState('settlement');
  const [files, setFiles] = useState([]);
  const [fileContent, setFileContent] = useState(null);
  const [matchResult, setMatchResult] = useState(null);
  const [loading, setLoading] = useState(false);

  const listFiles = async () => {
    if (!date) return;
    setLoading(true);
    try {
      setFiles(await napasApi.listRecoFiles(date));
    } catch (e) {
      toast(e.message || 'Failed to load reconciliation files', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    listFiles();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const downloadFile = async (fileType = type) => {
    setLoading(true);
    try {
      const result = await napasApi.downloadRecoFile(date, fileType);
      setType(fileType);
      setFileContent(result);
      toast('Reconciliation file loaded.', 'success');
    } catch (e) {
      toast(e.message || 'Failed to load reconciliation content', 'error');
    } finally {
      setLoading(false);
    }
  };

  const matchFile = async () => {
    setLoading(true);
    try {
      const result = await napasApi.matchRecoFile(date);
      setMatchResult(result);
      toast('Reconciliation match completed.', 'success');
    } catch (e) {
      toast(e.message || 'Reconciliation match failed', 'error');
    } finally {
      setLoading(false);
    }
  };

  const exportCsv = () => {
    if (!fileContent?.rawCsv) return;
    const blob = new Blob([fileContent.rawCsv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `NAPAS_${date}_${type}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      <Card>
        <CardHeader title="Reconciliation controls" />
        <CardBody>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 170px), 1fr))', gap: '0.75rem', alignItems: 'end' }}>
            <Input label="Business date" type="date" value={date} onChange={e => setDate(e.target.value)} />
            <Input label="File type" type="select" value={type} onChange={e => setType(e.target.value)}>
              <option value="settlement">settlement</option>
              <option value="dispute">dispute</option>
              <option value="reversal">reversal</option>
            </Input>
            <Button variant="outline" onClick={listFiles} loading={loading}>List files</Button>
            <Button variant="outline" onClick={() => downloadFile(type)} loading={loading}>Load file</Button>
            <Button variant="primary" onClick={matchFile} loading={loading}>Run match</Button>
          </div>
        </CardBody>
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 320px), 1fr))', gap: '1rem' }}>
        <Card>
          <CardHeader title="Available files" />
          <CardBody>
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>File</th>
                    <th>Type</th>
                    <th>Records</th>
                    <th>Generated</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {loading && !files.length ? (
                    <tr><td colSpan={5} className="empty-row"><div className="loading-shimmer" /></td></tr>
                  ) : !files.length ? (
                    <EmptyRow colSpan={5} text="No NAPAS files for this date." />
                  ) : files.map(file => (
                    <tr key={`${file.fileName}-${file.fileType}`}>
                      <td>{file.fileName}</td>
                      <td>{file.fileType}</td>
                      <td>{formatNumber(file.recordCount)}</td>
                      <td>{formatDateTime(file.generatedAt)}</td>
                      <td><Button size="sm" variant="outline" onClick={() => downloadFile(file.fileType)}>Open</Button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Match result"
            action={fileContent?.rawCsv ? <Button size="sm" variant="outline" onClick={exportCsv}>Export CSV</Button> : null}
          />
          <CardBody>
            {matchResult ? (
              <div style={{ display: 'grid', gap: '1rem' }}>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.75rem' }}>
                  <Metric label="NAPAS records" value={formatNumber(matchResult.totalNapasRecords)} />
                  <Metric label="Internal records" value={formatNumber(matchResult.totalInternalRecords)} />
                  <Metric label="Matched" value={formatNumber(matchResult.matchedCount)} tone="var(--green)" />
                  <Metric label="Exceptions" value={formatNumber(matchResult.createdExceptions)} tone={matchResult.createdExceptions ? 'var(--red)' : 'var(--green)'} />
                  <Metric label="Difference" value={formatCurrency(matchResult.amountDifference)} tone={Number(matchResult.amountDifference) ? 'var(--red)' : 'var(--green)'} />
                </div>
                <MismatchList title="Unmatched NAPAS" items={matchResult.unmatchedNapasRecords} />
                <MismatchList title="Unmatched internal" items={matchResult.unmatchedInternalRecords} internal />
                <MismatchList title="Amount mismatches" items={matchResult.amountMismatches} mismatch />
              </div>
            ) : (
              <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                {fileContent ? `${fileContent.records?.length || 0} records loaded for ${fileContent.businessDate}.` : 'No match result yet.'}
              </div>
            )}
          </CardBody>
        </Card>
      </div>

      {fileContent?.records?.length > 0 && (
        <Card>
          <CardHeader title="File records" />
          <CardBody>
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Transaction</th>
                    <th>Status</th>
                    <th>Amount</th>
                    <th>Bank</th>
                    <th>Fee</th>
                    <th>Processed</th>
                  </tr>
                </thead>
                <tbody>
                  {fileContent.records.slice(0, 50).map(record => (
                    <tr key={`${record.referenceId}-${record.transactionId}`}>
                      <td>{record.referenceId}</td>
                      <td>{record.transactionId}</td>
                      <td><StatusPill status={record.status} /></td>
                      <td>{formatCurrency(record.amount)}</td>
                      <td>{record.bankCode}</td>
                      <td>{formatCurrency(record.feeAmount)}</td>
                      <td>{formatDateTime(record.processedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}

function MismatchList({ title, items = [], internal = false, mismatch = false }) {
  if (!items?.length) return null;
  return (
    <div>
      <h4 style={{ margin: '0 0 0.5rem', color: 'var(--navy)', fontSize: '0.95rem' }}>{title}</h4>
      <div style={{ display: 'grid', gap: '0.4rem' }}>
        {items.slice(0, 5).map((item, index) => (
          <div key={`${title}-${index}`} style={{
            border: '1px solid var(--border)',
            borderRadius: '8px',
            padding: '0.65rem 0.75rem',
            fontSize: '0.84rem',
            color: 'var(--text-secondary)',
          }}>
            <strong style={{ color: 'var(--text-primary)' }}>{item.referenceId || item.napasReferenceId}</strong>
            {mismatch ? ` | NAPAS ${formatCurrency(item.napasAmount)} / Internal ${formatCurrency(item.internalAmount)}` : ''}
            {internal ? ` | ${formatCurrency(item.amount)} | ${item.disbursementStatus}` : ''}
            {!internal && !mismatch ? ` | ${formatCurrency(item.amount)} | ${item.status}` : ''}
          </div>
        ))}
      </div>
    </div>
  );
}

function QrTab() {
  const { toast } = useToast();
  const [form, setForm] = useState({
    loanId: '',
    amount: '',
    description: 'LoanFlow payment',
    bankCode: '',
    accountNumber: '',
    accountName: '',
    expiryMinutes: 30,
  });
  const [qr, setQr] = useState(null);
  const [qrImageUrl, setQrImageUrl] = useState('');
  const [decodeValue, setDecodeValue] = useState('');
  const [decoded, setDecoded] = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function renderQr() {
      if (!qr?.qrString) {
        setQrImageUrl('');
        return;
      }
      try {
        const url = await QRCode.toDataURL(qr.qrString, { width: 220, margin: 1 });
        if (!cancelled) setQrImageUrl(url);
      } catch {
        if (!cancelled) setQrImageUrl(qr.qrImageBase64 ? `data:image/png;base64,${qr.qrImageBase64}` : '');
      }
    }
    renderQr();
    return () => { cancelled = true; };
  }, [qr]);

  const setField = (key, value) => setForm(prev => ({ ...prev, [key]: value }));

  const generateQr = async () => {
    if (!form.description.trim()) {
      toast('Description is required.', 'warning');
      return;
    }
    setLoading(true);
    try {
      const result = await napasApi.generateQr({
        ...form,
        loanId: form.loanId ? Number(form.loanId) : undefined,
        amount: Number(form.amount || 0),
        expiryMinutes: Number(form.expiryMinutes || 0),
      });
      setQr(result);
      setDecodeValue(result.qrString);
      toast('VietQR generated.', 'success');
    } catch (e) {
      toast(e.message || 'VietQR generation failed', 'error');
    } finally {
      setLoading(false);
    }
  };

  const loadLoanQr = async () => {
    if (!form.loanId) {
      toast('Enter a loan ID first.', 'warning');
      return;
    }
    setLoading(true);
    try {
      const result = await napasApi.getLoanQr(Number(form.loanId));
      setQr(result);
      setDecodeValue(result.qrString);
      toast('Loan VietQR loaded.', 'success');
    } catch (e) {
      toast(e.message || 'Loan VietQR failed', 'error');
    } finally {
      setLoading(false);
    }
  };

  const decodeQr = async () => {
    if (!decodeValue.trim()) {
      toast('QR string is required.', 'warning');
      return;
    }
    setLoading(true);
    try {
      const result = await napasApi.decodeQr(decodeValue.trim());
      setDecoded(result);
      toast('QR decoded.', 'success');
    } catch (e) {
      toast(e.message || 'QR decode failed', 'error');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 320px), 1fr))', gap: '1rem' }}>
      <Card>
        <CardHeader title="Create VietQR" />
        <CardBody>
          <div style={{ display: 'grid', gap: '0.85rem' }}>
            <Input label="Loan ID" type="number" value={form.loanId} onChange={e => setField('loanId', e.target.value)} />
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 150px), 1fr))', gap: '0.75rem' }}>
              <Input label="Amount" type="number" min="0" value={form.amount} onChange={e => setField('amount', e.target.value)} />
              <Input label="Expiry minutes" type="number" min="0" value={form.expiryMinutes} onChange={e => setField('expiryMinutes', e.target.value)} />
            </div>
            <Input label="Description" value={form.description} onChange={e => setField('description', e.target.value)} />
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 150px), 1fr))', gap: '0.75rem' }}>
              <Input label="Bank code" value={form.bankCode} onChange={e => setField('bankCode', e.target.value.toUpperCase())} />
              <Input label="Account number" value={form.accountNumber} onChange={e => setField('accountNumber', e.target.value)} />
            </div>
            <Input label="Account name" value={form.accountName} onChange={e => setField('accountName', e.target.value)} />
            <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
              <Button variant="primary" onClick={generateQr} loading={loading}>Generate QR</Button>
              <Button variant="outline" onClick={loadLoanQr} loading={loading}>Load loan QR</Button>
            </div>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="QR result" />
        <CardBody>
          {qr ? (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 240px), 1fr))', gap: '1rem', alignItems: 'start' }}>
              <div style={{
                width: 240,
                minHeight: 240,
                border: '1px solid var(--border)',
                borderRadius: '8px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: '#fff',
              }}>
                {qrImageUrl ? <img src={qrImageUrl} alt="VietQR" style={{ width: 220, height: 220 }} /> : <span>QR</span>}
              </div>
              <div style={{ display: 'grid', gap: '0.85rem' }}>
                <Field label="Reference ID" value={qr.referenceId} />
                <Field label="Amount" value={formatCurrency(qr.amount)} />
                <Field label="Bank/account" value={`${qr.bankCode} / ${qr.accountNumber}`} />
                <Field label="Account name" value={qr.accountName} />
                <Field label="Generated" value={formatDateTime(qr.generatedAt)} />
                <Field label="Expires" value={formatDateTime(qr.expiresAt)} />
                <Input type="textarea" label="QR string" value={qr.qrString || ''} readOnly rows={4} />
              </div>
            </div>
          ) : (
            <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>No QR generated.</div>
          )}
        </CardBody>
      </Card>

      <Card style={{ gridColumn: '1 / -1' }}>
        <CardHeader title="Decode QR" action={<Button variant="outline" onClick={decodeQr} loading={loading}>Decode</Button>} />
        <CardBody>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 260px), 1fr))', gap: '1rem' }}>
            <Input type="textarea" label="QR string" value={decodeValue} onChange={e => setDecodeValue(e.target.value)} rows={6} />
            <div style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '1rem' }}>
              {decoded ? (
                <div style={{ display: 'grid', gap: '0.8rem' }}>
                  <Field label="Valid" value={decoded.isValid ? 'Yes' : 'No'} />
                  <Field label="Bank" value={decoded.bankCode} />
                  <Field label="Account" value={decoded.accountNumber} />
                  <Field label="Amount" value={formatCurrency(decoded.amount)} />
                  <Field label="Description" value={decoded.description} />
                  <Field label="Reference ID" value={decoded.referenceId} />
                  <Field label="Error" value={decoded.validationError} />
                </div>
              ) : (
                <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>No decoded data.</div>
              )}
            </div>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

export default function Napas() {
  const { user } = useAuth();
  const role = user?.role;
  const visibleTabs = useMemo(
    () => TAB_CONFIG.filter(tab => tab.roles.includes(role)),
    [role]
  );
  const [activeTab, setActiveTab] = useState(visibleTabs[0]?.key || 'qr');

  useEffect(() => {
    if (!visibleTabs.some(tab => tab.key === activeTab)) {
      setActiveTab(visibleTabs[0]?.key || 'qr');
    }
  }, [activeTab, visibleTabs]);

  return (
    <div className="page">
      <div className="page-header">
        <h2>NAPAS Operations</h2>
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', borderBottom: '2px solid var(--border)' }}>
        {visibleTabs.map(tab => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
            style={{
              padding: '0.65rem 1rem',
              border: 'none',
              borderBottom: activeTab === tab.key ? '2px solid var(--blue)' : '2px solid transparent',
              background: 'transparent',
              color: activeTab === tab.key ? 'var(--blue)' : 'var(--text-muted)',
              fontWeight: 800,
              cursor: 'pointer',
              marginBottom: '-2px',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'transactions' && <TransactionsTab />}
      {activeTab === 'qr' && <QrTab />}
      {activeTab === 'merchant' && <MerchantTab />}
      {activeTab === 'reconciliation' && <ReconciliationTab />}
    </div>
  );
}
