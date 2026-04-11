import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';

const QUICK = [
  { label: '👑 admin',       user: 'admin',       pass: 'Admin@123' },
  { label: '📋 officer01',   user: 'officer01',   pass: 'Officer@123' },
  { label: '✅ approver01',  user: 'approver01',  pass: 'Approver@123' },
  { label: '🔍 collector01', user: 'collector01', pass: 'Collector@123' },
];

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError]       = useState('');
  const [loading, setLoading]   = useState(false);
  const { login } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();

  const handleSubmit = async (u = username, p = password) => {
    setError(''); setLoading(true);
    try {
      await login(u, p);
      navigate('/dashboard');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-bg">
      <div className="login-card">
        <div className="brand-login">
          <div className="brand-login-icon">💳</div>
          <h1>LoanFlow <span>Lite</span></h1>
          <p>Hệ thống quản lý khoản vay</p>
        </div>

        <form className="login-form" style={{ display:'flex', flexDirection:'column', gap:'1.1rem' }}
              onSubmit={e => { e.preventDefault(); handleSubmit(); }}>
          <div className="form-group">
            <label>Tên đăng nhập</label>
            <input value={username} onChange={e => setUsername(e.target.value)} placeholder="officer01" autoComplete="username" />
          </div>
          <div className="form-group">
            <label>Mật khẩu</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="••••••••" autoComplete="current-password" />
          </div>
          {error && <div className="error-msg">{error}</div>}
          <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
            {loading ? <><span className="spinner" /> Đang đăng nhập...</> : 'Đăng nhập'}
          </button>
        </form>

        <div className="login-hints">
          <p>Tài khoản demo — click để đăng nhập nhanh:</p>
          <div className="hint-grid">
            {QUICK.map(q => (
              <button key={q.user} className="quick-btn" onClick={() => handleSubmit(q.user, q.pass)}>
                {q.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
