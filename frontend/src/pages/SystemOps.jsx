import { useState, useEffect, useRef, useCallback } from 'react';
import { Card, CardHeader, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Modal } from '../components/ui/Modal';
import { useToast } from '../context/ToastContext';
import { useT } from '../i18n/useT';
import { useNotifications } from '../context/NotificationContext';
import { systemApi } from '../features/system/api/systemApi';
import { adminApi }  from '../features/system/api/adminApi';



const COOLDOWN_SECONDS = 30;

const ROLE_LABELS = {
  Admin: 'Admin', Manager: 'Quản lý', Approver: 'Phê duyệt',
  LoanOfficer: 'Cán bộ tín dụng', Collector: 'Thu hồi nợ',
};
const ROLE_COLORS = {
  Admin: '#5d7ce2', Manager: '#1e9b69', Approver: '#c98a1b',
  LoanOfficer: '#1f5fbf', Collector: '#c34f4f',
};

function RoleBadge({ role }) {
  return (
    <span style={{
      padding: '0.2em 0.65em', borderRadius: '999px', fontSize: '0.72rem', fontWeight: 700,
      background: `${ROLE_COLORS[role] || '#aaa'}22`, color: ROLE_COLORS[role] || '#555',
    }}>
      {ROLE_LABELS[role] || role}
    </span>
  );
}

function ConfirmDialog({ message, onConfirm, onCancel }) {
  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 9999,
      background: 'rgba(10,28,56,0.45)', backdropFilter: 'blur(4px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }}>
      <div style={{
        background: '#fff', borderRadius: '18px', padding: '2rem 2.2rem',
        maxWidth: '400px', width: '90%', boxShadow: '0 28px 72px rgba(16,35,63,0.22)',
        animation: 'pageIn 0.22s ease',
      }}>
        <h3 style={{ marginBottom: '0.6rem', color: 'var(--navy)', fontSize: '1.05rem', fontWeight: 800 }}>Xác nhận</h3>
        <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', lineHeight: 1.6 }}>{message}</p>
        <div style={{ display: 'flex', gap: '0.75rem', marginTop: '1.5rem', justifyContent: 'flex-end' }}>
          <Button variant="ghost" onClick={onCancel}>Huỷ</Button>
          <Button variant="primary" onClick={onConfirm}>Xác nhận</Button>
        </div>
      </div>
    </div>
  );
}

// ── Tabs ─────────────────────────────────────────────────────────────────────
export default function SystemOps() {
  const { toast } = useToast();
  const { t } = useT();
  const [activeTab, setActiveTab] = useState('batch');

  const TABS = [
    { key: 'batch',  label: `⚙️ ${t('system.tabs.batch')}` },
    { key: 'users',  label: `👥 ${t('system.tabs.users')}` },
    { key: 'period', label: `📅 ${t('system.tabs.period')}` },
  ];

  return (
    <div className="page">
      <div className="page-header">
        <h2>{t('system.title')}</h2>
      </div>

      {/* Tab bar */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', borderBottom: '2px solid var(--border)', paddingBottom: '0' }}>
        {TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            style={{
              padding: '0.6rem 1.1rem', border: 'none', background: 'transparent',
              fontFamily: 'inherit', fontSize: '0.88rem', fontWeight: 700, cursor: 'pointer',
              color: activeTab === tab.key ? 'var(--blue)' : 'var(--text-muted)',
              borderBottom: activeTab === tab.key ? '2px solid var(--blue)' : '2px solid transparent',
              marginBottom: '-2px', transition: 'color 0.18s',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'batch'  && <BatchTab toast={toast} />}
      {activeTab === 'users'  && <UsersTab toast={toast} />}
      {activeTab === 'period' && <PeriodTab toast={toast} />}
    </div>
  );
}

// ── Batch Jobs Tab ────────────────────────────────────────────────────────────
function BatchTab({ toast }) {
  const { refreshCount } = useNotifications();
  const [dateStr, setDateStr]   = useState('');
  const [runningJob, setRunningJob] = useState(null);
  const [cooldown, setCooldown] = useState(0);
  const [confirm, setConfirm]   = useState(null);
  const timerRef = useRef(null);

  const startCooldown = useCallback(() => {
    setCooldown(COOLDOWN_SECONDS);
    timerRef.current = setInterval(() => {
      setCooldown(s => { if (s <= 1) { clearInterval(timerRef.current); return 0; } return s - 1; });
    }, 1000);
  }, []);
  useEffect(() => () => clearInterval(timerRef.current), []);

  const handleRunJobs = async (type) => {
    setRunningJob(type);
    try {
      const result = type === 'eod'
        ? await systemApi.runEod(dateStr)
        : await systemApi.runOverdue(dateStr);
      const processed = type === 'eod'
        ? (result?.overdueResult?.totalLoansProcessed ?? 0)
        : (result?.totalLoansProcessed ?? 0);
      toast(`Ran ${type === 'eod' ? 'EOD' : 'overdue update'} successfully. Loans processed: ${processed}`, 'success');
      refreshCount(); // Bell may have new BatchJobCompleted notification
    } catch (e) {
      toast(e.message || 'System operation failed', 'error');
    } finally {
      setRunningJob(null);
      startCooldown();
    }
  };


  const isLocked = runningJob !== null || cooldown > 0;

  return (
    <>
      {confirm && (
        <ConfirmDialog
          message={`Run End-Of-Day batch${dateStr ? ` for ${dateStr}` : ' for today'}? This will process all active loans and cannot be undone.`}
          onConfirm={() => { setConfirm(null); handleRunJobs(confirm); }}
          onCancel={() => setConfirm(null)}
        />
      )}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.5rem' }}>
        <Card hoverable>
          <CardHeader title="Batch Jobs" subtitle="Run end-of-day processing or re-run overdue aging for the selected business date." />
          <CardBody>
            <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <div style={{ minWidth: '180px' }}>
                <Input type="date" label="Business Date" value={dateStr} onChange={e => setDateStr(e.target.value)} />
              </div>
              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                <Button variant="primary" onClick={() => setConfirm('eod')} disabled={isLocked}
                  title={cooldown > 0 ? `Please wait ${cooldown}s` : undefined}>
                  {runningJob === 'eod' ? 'Running EOD…' : cooldown > 0 ? `End-Of-Day (${cooldown}s)` : 'Run End-Of-Day'}
                </Button>
                <Button variant="outline" onClick={() => handleRunJobs('overdue')} disabled={isLocked}
                  title={cooldown > 0 ? `Please wait ${cooldown}s` : undefined}>
                  {runningJob === 'overdue' ? 'Running…' : cooldown > 0 ? `Overdue (${cooldown}s)` : 'Run Overdue Update'}
                </Button>
              </div>
            </div>
            {cooldown > 0 && (
              <p style={{ marginTop: '0.85rem', fontSize: '0.78rem', color: 'var(--amber)', fontWeight: 600 }}>
                ⏳ Buttons locked for {cooldown}s after last run.
              </p>
            )}
          </CardBody>
        </Card>

        <Card hoverable>
          <CardHeader title="Backup & Data" subtitle="Database backup and restore operations are handled from the production terminal." />
          <CardBody>
            <Button variant="outline" disabled style={{ opacity: 0.65 }}>Run Backup Now</Button>
            <p style={{ marginTop: '1rem', fontSize: '0.82rem', color: 'var(--text-muted)' }}>
              Run{' '}
              <code style={{ background: 'var(--bg-muted)', padding: '0.1em 0.4em', borderRadius: '5px', fontSize: '0.9em' }}>
                .\scripts\backup.ps1
              </code>{' '}
              from the production terminal.
            </p>
          </CardBody>
        </Card>
      </div>
    </>
  );
}

// ── Users Tab ─────────────────────────────────────────────────────────────────
const ROLES = ['Admin', 'Manager', 'Approver', 'LoanOfficer', 'Collector'];

function UsersTab({ toast }) {
  const [users, setUsers]       = useState([]);
  const [loading, setLoading]   = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [saving, setSaving]     = useState(false);
  const [newUser, setNewUser]   = useState({ username: '', fullName: '', role: 'LoanOfficer', password: '' });

  const loadUsers = async () => {
    setLoading(true);
    try { setUsers(await adminApi.getUsers()); }
    catch (e) { toast('Lỗi tải danh sách: ' + e.message, 'error'); }
    finally { setLoading(false); }
  };
  useEffect(() => { loadUsers(); }, []);

  const handleCreate = async () => {
    if (!newUser.username.trim() || !newUser.password.trim()) {
      toast('Vui lòng điền đầy đủ tên đăng nhập và mật khẩu.', 'error'); return;
    }
    setSaving(true);
    try {
      await adminApi.createUser(newUser);
      toast('Tạo người dùng thành công!', 'success');
      setShowCreate(false);
      setNewUser({ username: '', fullName: '', role: 'LoanOfficer', password: '' });
      loadUsers();
    } catch (e) {
      toast('Lỗi: ' + (e.message || 'Có lỗi xảy ra'), 'error');
    } finally { setSaving(false); }
  };

  const handleLock = async (id, isLocked) => {
    try {
      if (isLocked) { await adminApi.unlockAccount(id); toast('Đã mở khóa tài khoản.', 'success'); }
      else          { await adminApi.lockAccount(id, 60); toast('Đã khóa tài khoản 60 phút.', 'success'); }
      loadUsers();
    } catch (e) { toast('Lỗi: ' + e.message, 'error'); }
  };

  const handleRoleChange = async (id, role) => {
    try {
      await adminApi.changeRole(id, role);
      toast('Đã đổi vai trò.', 'success');
      loadUsers();
    } catch (e) { toast('Lỗi: ' + e.message, 'error'); }
  };

  return (
    <>
      {showCreate && (
        <Modal title="👤 Tạo người dùng mới" onClose={() => setShowCreate(false)} width="440px">
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <Input label="Tên đăng nhập" value={newUser.username} onChange={e => setNewUser(u => ({ ...u, username: e.target.value }))} />
            <Input label="Họ và tên" value={newUser.fullName} onChange={e => setNewUser(u => ({ ...u, fullName: e.target.value }))} />
            <Input type="select" label="Vai trò" value={newUser.role} onChange={e => setNewUser(u => ({ ...u, role: e.target.value }))}>
              {ROLES.map(r => <option key={r} value={r}>{ROLE_LABELS[r]}</option>)}
            </Input>
            <Input type="password" label="Mật khẩu tạm" value={newUser.password} onChange={e => setNewUser(u => ({ ...u, password: e.target.value }))} />
            <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
              <Button variant="ghost" onClick={() => setShowCreate(false)} disabled={saving}>Huỷ</Button>
              <Button variant="primary" onClick={handleCreate} disabled={saving}>{saving ? 'Đang tạo…' : 'Tạo người dùng'}</Button>
            </div>
          </div>
        </Modal>
      )}

      <Card>
        <CardHeader title="Quản lý người dùng" subtitle="Tạo, khoá, đổi vai trò người dùng hệ thống."
          action={<Button variant="primary" onClick={() => setShowCreate(true)}>+ Tạo mới</Button>} />
        <CardBody>
          {loading ? <div className="loading-shimmer" style={{ height: 200 }} /> : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Tên đăng nhập</th>
                    <th>Họ và tên</th>
                    <th>Vai trò</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {users.length === 0 && (
                    <tr><td colSpan={5} className="empty-row"><div className="empty-state">Không có người dùng nào.</div></td></tr>
                  )}
                  {users.map(u => {
                    const isLocked = u.status === 'Locked' || !!u.lockedUntil;
                    return (
                      <tr key={u.id}>
                        <td><strong>{u.username}</strong></td>
                        <td>{u.fullName || '—'}</td>
                        <td>
                          <select
                            value={u.role}
                            onChange={e => handleRoleChange(u.id, e.target.value)}
                            style={{ border: 'none', background: 'transparent', fontFamily: 'inherit', fontSize: '0.85rem', cursor: 'pointer' }}
                          >
                            {ROLES.map(r => <option key={r} value={r}>{ROLE_LABELS[r]}</option>)}
                          </select>
                        </td>
                        <td>
                          <span style={{ color: isLocked ? 'var(--red)' : u.status === 'Inactive' ? 'var(--text-muted)' : 'var(--green)', fontWeight: 600, fontSize: '0.82rem' }}>
                            {isLocked ? '🔒 Bị khóa' : u.status === 'Inactive' ? '⭕ Không hoạt động' : '✅ Hoạt động'}
                          </span>
                        </td>
                        <td>
                          <Button variant={isLocked ? 'outline' : 'ghost'} onClick={() => handleLock(u.id, isLocked)}
                            style={{ fontSize: '0.78rem', padding: '0.3rem 0.65rem' }}>
                            {isLocked ? '🔓 Mở khóa' : '🔒 Khóa'}
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </>
  );
}

// ── Period / BOD Tab ──────────────────────────────────────────────────────────
function PeriodTab({ toast }) {
  const [period, setPeriod] = useState(null);
  const [bodDate, setBodDate] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setLoading(true);
    try { setPeriod(await systemApi.getAccountingPeriod()); }
    catch (e) { /* no current period is ok */ }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const doAction = async (fn, msg) => {
    setBusy(true);
    try { await fn(); toast(msg, 'success'); load(); }
    catch (e) { toast('Lỗi: ' + (e.message || 'Có lỗi xảy ra'), 'error'); }
    finally { setBusy(false); }
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.5rem' }}>

      {/* BOD Card */}
      <Card hoverable>
        <CardHeader title="Beginning of Day (BOD)" subtitle="Mở kỳ giao dịch ngày mới trước khi bắt đầu hoạt động." />
        <CardBody>
          <div style={{ marginBottom: '1rem' }}>
            <Input type="date" label="Ngày giao dịch" value={bodDate} onChange={e => setBodDate(e.target.value)} />
          </div>
          <Button
            variant="primary"
            disabled={busy || !bodDate}
            onClick={() => doAction(() => systemApi.runBod(bodDate), 'Đã chạy BOD thành công!')}
          >
            {busy ? 'Đang xử lý…' : '▶ Mở BOD'}
          </Button>
        </CardBody>
      </Card>

      {/* Accounting Period Card */}
      <Card hoverable>
        <CardHeader title="Kỳ kế toán" subtitle="Quản lý trạng thái kỳ kế toán hiện tại." />
        <CardBody>
          {loading ? <div className="loading-shimmer" style={{ height: 80 }} /> : (
            <>
              {period ? (
                <div style={{ marginBottom: '1.25rem', padding: '0.85rem 1rem', background: 'var(--bg-muted)', borderRadius: '10px' }}>
                  <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', fontWeight: 700 }}>KỲ HIỆN TẠI</div>
                  <div style={{ fontSize: '1.1rem', fontWeight: 800, color: 'var(--navy)', marginTop: '0.2rem' }}>
                    {String(period.month).padStart(2, '0')}/{period.year}
                  </div>
                  <div style={{ marginTop: '0.25rem' }}>
                    <span style={{ fontSize: '0.8rem', fontWeight: 700, color: period.status === 'Open' ? 'var(--green)' : 'var(--red)' }}>
                      {period.status === 'Open' ? '✅ Đang mở' : '🔒 Đã đóng'}
                    </span>
                  </div>
                </div>
              ) : (
                <div style={{ marginBottom: '1.25rem', color: 'var(--text-muted)', fontSize: '0.88rem' }}>Chưa có kỳ kế toán nào.</div>
              )}
              <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                <Button
                  variant="primary"
                  disabled={busy || !period || period.status === 'Open'}
                  onClick={() => doAction(() => systemApi.reopenPeriod(period.year, period.month), 'Đã mở lại kỳ kế toán!')}
                >
                  Mở lại kỳ hiện tại
                </Button>
                <Button
                  variant="outline"
                  disabled={busy || !period || period.status !== 'Open'}
                  onClick={() =>
                    doAction(() => systemApi.closePeriod(period.year, period.month), 'Đã đóng kỳ kế toán!')
                  }
                >
                  Đóng kỳ hiện tại
                </Button>
              </div>
            </>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
