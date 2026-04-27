import { useState } from 'react';
import { useToast } from '../context/ToastContext';

export default function RegulatoryReports() {
  const { toast } = useToast();
  const [reportType, setReportType] = useState('BIEU_1A');
  const [reportDate, setReportDate] = useState(new Date().toISOString().split('T')[0]);
  const [generating, setGenerating] = useState(false);

  const handleGenerate = async (format) => {
    setGenerating(true);
    try {
      // Simulate API call
      await new Promise(r => setTimeout(r, 1000));
      toast(`✅ Đã xuất báo cáo ${reportType} định dạng ${format.toUpperCase()}`, 'success');
    } catch (e) {
      toast('Lỗi xuất báo cáo', 'error');
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>📜 Báo cáo NHNN & Regulatory</h2>
          <p className="subtitle">Xuất báo cáo theo mẫu quy định (Biểu 1A, 1B, CIC, CAR)</p>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: '2rem' }}>
        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)', boxShadow: 'var(--shadow-sm)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1rem' }}>Tham số báo cáo</h3>
          
          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label>Loại báo cáo</label>
            <select className="form-input" value={reportType} onChange={e => setReportType(e.target.value)}>
              <option value="BIEU_1A">Biểu 1A - Dư nợ cho vay theo ngành</option>
              <option value="BIEU_1B">Biểu 1B - Phân loại nợ</option>
              <option value="CAR">Báo cáo An toàn vốn (CAR)</option>
              <option value="CIC">Báo cáo CIC (Hồ sơ tín dụng)</option>
              <option value="LCR">Tỷ lệ đảm bảo thanh khoản (LCR)</option>
            </select>
          </div>

          <div className="form-group" style={{ marginBottom: '1.5rem' }}>
            <label>Ngày chốt số liệu</label>
            <input type="date" className="form-input" value={reportDate} onChange={e => setReportDate(e.target.value)} />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <button className="btn btn-primary" onClick={() => handleGenerate('excel')} disabled={generating}>
              {generating ? '⏳ Đang tạo...' : '📊 Xuất Excel (.xlsx)'}
            </button>
            <button className="btn btn-outline" onClick={() => handleGenerate('xml')} disabled={generating}>
              {generating ? '⏳ Đang tạo...' : '📄 Xuất XML (NHNN)'}
            </button>
          </div>
        </div>

        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1rem' }}>Lịch sử kết xuất</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Ngày tạo</th>
                  <th>Loại báo cáo</th>
                  <th>Kỳ báo cáo</th>
                  <th>Định dạng</th>
                  <th>Người tạo</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>{new Date().toLocaleDateString('vi-VN')}</td>
                  <td>Biểu 1B - Phân loại nợ</td>
                  <td>{reportDate}</td>
                  <td><span className="code-badge">XML</span></td>
                  <td>System Admin</td>
                  <td><button className="btn btn-ghost btn-sm">Tải xuống</button></td>
                </tr>
                <tr>
                  <td>{new Date(Date.now() - 86400000).toLocaleDateString('vi-VN')}</td>
                  <td>Biểu 1A - Dư nợ</td>
                  <td>2024-03-31</td>
                  <td><span className="code-badge">EXCEL</span></td>
                  <td>System Admin</td>
                  <td><button className="btn btn-ghost btn-sm">Tải xuống</button></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
