import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (u = username, p = password) => {
    setError('');
    setLoading(true);
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
            {error && <div className="error-msg">{error}</div>}
            <Button type="submit" variant="primary" block loading={loading}>
              Sign in
            </Button>
          </form>
        </section>
      </div>
    </div>
  );
}
