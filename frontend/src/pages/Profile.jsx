import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { httpClient } from '../shared/api/httpClient';
import { useToast } from '../context/ToastContext';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';

export default function Profile() {
  const { user, roleLabel } = useAuth();
  const { toast } = useToast();
  
  const [loading, setLoading] = useState(true);
  const [mfaSetup, setMfaSetup] = useState(null);
  const [totpCode, setTotpCode] = useState('');
  const [enabling, setEnabling] = useState(false);

  useEffect(() => {
    const fetchMfa = async () => {
      try {
        const res = await httpClient('GET', '/auth/mfa-setup');
        setMfaSetup(res);
      } catch (err) {
        toast('Lỗi tải cấu hình MFA: ' + err.message, 'error');
      } finally {
        setLoading(false);
      }
    };
    fetchMfa();
  }, [toast]);

  const handleEnableMfa = async () => {
    if (!totpCode || totpCode.length < 6) {
      toast('Vui lòng nhập đủ 6 số TOTP', 'error');
      return;
    }
    setEnabling(true);
    try {
      await httpClient('POST', '/auth/enable-mfa', { totpCode });
      toast('Bật MFA thành công!', 'success');
      setMfaSetup(prev => ({ ...prev, isEnabled: true }));
      setTotpCode('');
    } catch (err) {
      toast('Lỗi xác minh: ' + err.message, 'error');
    } finally {
      setEnabling(false);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>👤 Tài khoản cá nhân</h2>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(300px, 1fr) minmax(300px, 1fr)', gap: '1.5rem' }}>
        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1.1rem', fontWeight: 800 }}>Thông tin</h3>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <p><strong>Họ tên:</strong> {user?.fullName}</p>
            <p><strong>Tài khoản:</strong> {user?.username}</p>
            <p><strong>Vai trò:</strong> {roleLabel}</p>
          </div>
        </div>

        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1.1rem', fontWeight: 800 }}>Bảo mật (MFA)</h3>
          
          {loading ? (
            <p>Đang tải cấu hình bảo mật...</p>
          ) : mfaSetup?.isEnabled ? (
            <div>
              <p style={{ color: 'var(--green)', fontWeight: 800, fontSize: '1.2rem', marginBottom: '0.5rem' }}>✅ Đã bật MFA</p>
              <p style={{ color: 'var(--text-secondary)' }}>Tài khoản của bạn đang được bảo vệ bởi xác thực hai bước (TOTP).</p>
            </div>
          ) : mfaSetup?.qrCodeUri ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <p style={{ color: 'var(--text-secondary)' }}>Quét mã QR sau bằng ứng dụng Authenticator (Google Auth, Authy...):</p>
              <div style={{ background: '#fff', padding: '1rem', borderRadius: 12, alignSelf: 'flex-start', border: '1px solid #ddd' }}>
                {/* Dùng QR API hoặc img trực tiếp từ base64/svg nếu backend trả về. Ở đây giả định backend trả về URL cho google chart */}
                <img src={`https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(mfaSetup.qrCodeUri)}`} alt="MFA QR Code" width="180" height="180" />
              </div>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Mã bí mật (nếu không quét được QR): <strong>{mfaSetup.manualEntryKey}</strong></p>
              <div style={{ marginTop: '0.5rem', display: 'flex', gap: '0.75rem', alignItems: 'flex-end' }}>
                <Input 
                  label="Nhập mã 6 số từ app" 
                  value={totpCode} 
                  onChange={(e) => setTotpCode(e.target.value)} 
                  placeholder="123456" 
                  style={{ width: '150px' }}
                />
                <Button variant="primary" onClick={handleEnableMfa} disabled={enabling}>
                  {enabling ? '⏳ Đang xác minh...' : 'Bật MFA'}
                </Button>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
