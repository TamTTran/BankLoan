import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { useToast } from '../context/ToastContext';
import * as signalR from '@microsoft/signalr';

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [step, setStep] = useState(1); // 1: Login, 2: MFA, 3: Change Password
  const [mfaToken, setMfaToken] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const { login, verifyMfa } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();

  const handleSubmit = async (u = username, p = password) => {
    setError('');
    setLoading(true);
    try {
      if (step === 1) {
        const res = await login(u, p);
        if (res?.requiresPasswordChange) {
          setMfaToken(res.mfaToken);
          setStep(3);
        } else if (res?.requiresMfa) {
          setMfaToken(res.mfaToken);
          setStep(2);
        } else {
          navigate('/dashboard');
        }
      } else if (step === 2) {
        await verifyMfa(mfaToken, totpCode);
        navigate('/dashboard');
      } else if (step === 3) {
        // We need an api method in AuthContext or use httpClient here
        // We'll call httpClient directly or update authApi
        const { httpClient } = await import('../shared/api/httpClient');
        const res = await httpClient('POST', '/auth/change-expired-password', { mfaToken, newPassword });
        if (res?.requiresMfa) {
          setMfaToken(res.mfaToken);
          setStep(2);
          setNewPassword('');
          toast('Đã đổi mật khẩu. Vui lòng xác thực MFA.', 'info');
        } else {
          // Success login without MFA
          const { setToken, setUser } = await import('../shared/utils/authStorage');
          setToken(res.accessToken);
          setUser(res);
          window.location.href = '/dashboard'; // force reload to update AuthContext state
        }
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (step !== 2 || !mfaToken) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/api/hubs/notification', { accessTokenFactory: () => mfaToken })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveNotification', (notification) => {
      if (notification?.title && notification?.message) {
        toast(`${notification.title}: ${notification.message}`, 'info');
      }
    });

    connection.start().catch(console.error);

    return () => {
      connection.stop();
    };
  }, [step, mfaToken, toast]);

  return (
    <div className="login-bg">
      <div className="login-shell">
        <section className="login-hero">
          <div className="login-hero-mark">LF</div>
          <p className="login-eyebrow">Loan operations workspace</p>
          <h1>Control the full lending cycle from one console.</h1>
          <p className="login-lead">
            Review applications, track active portfolio, watch delinquency signals,
            and keep branch operations aligned with a clean daily workflow.
          </p>

          <div className="login-highlights">
            <div className="login-highlight-card">
              <span className="login-highlight-value">Applications</span>
              <span className="login-highlight-label">From intake to approval, disbursement and repayment tracking.</span>
            </div>
            <div className="login-highlight-card">
              <span className="login-highlight-value">Collections</span>
              <span className="login-highlight-label">Overdue visibility, portfolio movement and daily branch discipline.</span>
            </div>
          </div>
        </section>

        <section className="login-card">
          <div className="brand-login">
            <div className="brand-login-icon">LF</div>
            <p className="login-card-kicker">Secure sign in</p>
            <h1>LoanFlow <span>Lite</span></h1>
            <p>Sign in to continue to the lending workspace.</p>
          </div>

          <form className="login-form" onSubmit={(e) => { e.preventDefault(); handleSubmit(); }}>
            {step === 1 ? (
              <>
                <Input
                  label="Username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="officer01"
                  autoComplete="username"
                />
                <Input
                  type="password"
                  label="Password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  autoComplete="current-password"
                />
              </>
            ) : step === 2 ? (
              <>
                <div style={{ padding: '1rem', background: 'var(--blue-50)', color: 'var(--navy)', borderRadius: 12, marginBottom: '1rem', fontSize: '0.9rem', lineHeight: 1.5 }}>
                  Mở Google Authenticator trên điện thoại, chọn tài khoản LoanFlow và nhập mã 6 số đang hiển thị. Mã sẽ đổi sau mỗi 30 giây.
                </div>
                <Input
                  label="Google Authenticator code"
                  value={totpCode}
                  onChange={(e) => setTotpCode(e.target.value)}
                  placeholder="123456"
                  autoComplete="one-time-code"
                  autoFocus
                />
              </>
            ) : (
              <>
                <div style={{ padding: '1rem', background: 'var(--amber-bg)', color: 'var(--amber)', borderRadius: 12, marginBottom: '1rem', fontSize: '0.85rem' }}>
                  ⚠️ Mật khẩu của bạn đã hết hạn hoặc cần được đổi. Vui lòng cập nhật mật khẩu mới.
                </div>
                <Input
                  type="password"
                  label="Mật khẩu mới"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="Nhập mật khẩu mới"
                  autoFocus
                />
              </>
            )}
            {error && <div className="error-msg">{error}</div>}
            <Button type="submit" variant="primary" block loading={loading}>
              {step === 1 ? 'Sign in' : step === 2 ? 'Verify MFA' : 'Update Password'}
            </Button>
          </form>
        </section>
      </div>
    </div>
  );
}
