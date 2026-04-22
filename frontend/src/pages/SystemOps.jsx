import { useState } from 'react';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { useToast } from '../context/ToastContext';
import { systemApi } from '../features/system/api/systemApi';

export default function SystemOps() {
  const [dateStr, setDateStr] = useState('');
  const { toast } = useToast();

  const handleRunJobs = async (type) => {
    try {
      if (type === 'eod') {
        const res = await systemApi.runEod(dateStr);
        toast(`Chạy EOD thành công! Processed: ${res.totalProcessed || 0}`, 'success');
      } else {
        const res = await systemApi.runOverdue(dateStr);
        toast(`Chạy Overdue thành công! Processed: ${res.totalProcessed || 0}`, 'success');
      }
    } catch (e) {
      toast(e.response?.data?.message || 'Lỗi hệ thống', 'error');
    }
  };

  return (
    <div className="page">
      <div className="page-header"><h2>⚙️ Quản trị hệ thống (System Ops)</h2></div>
      
      <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem' }}>
        <Card style={{ flex: 1 }}>
          <h3>Batch Jobs (EOD)</h3>
          <p style={{ color: 'var(--text-muted)' }}>Mặc định hệ thống sẽ tự chạy vào cuối ngày. Dùng chức năng này để giả lập hoặc trigger lại cho ngày đã qua.</p>
          <div style={{ marginTop: '1rem', display: 'flex', gap: '1rem', alignItems: 'flex-end' }}>
            <div style={{ width: '200px' }}>
              <Input type="date" label="Business Date" value={dateStr} onChange={e => setDateStr(e.target.value)} />
            </div>
            <Button variant="primary" onClick={() => handleRunJobs('eod')}>Chạy End-Of-Day</Button>
            <Button variant="outline" onClick={() => handleRunJobs('overdue')}>Chạy Update Quá Hạn</Button>
          </div>
        </Card>

        <Card style={{ flex: 1 }}>
          <h3>Backup & Data</h3>
          <p style={{ color: 'var(--text-muted)' }}>Quản trị hệ thống sao lưu và phục hồi dữ liệu thông qua PowerShell Tools.</p>
          <div style={{ marginTop: '1rem' }}>
            <Button variant="outline" disabled>Chạy Backup Ngay</Button>
            <p style={{ fontSize: '0.8rem', marginTop: '0.5rem' }}>* Vui lòng chạy script .\scripts\backup.ps1 từ terminal production.</p>
          </div>
        </Card>
      </div>
    </div>
  );
}
