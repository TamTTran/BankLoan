import { useState } from 'react';
import { useToast } from '../context/ToastContext';
import { regulatoryApi } from '../features/regulatory/api/regulatoryApi';

const REPORT_TYPES = [
  { value: 'BIEU_1A', label: 'NHNN classification' },
  { value: 'BIEU_1B', label: 'NHNN provision' },
  { value: 'CAR', label: 'Capital adequacy' },
  { value: 'CIC', label: 'CIC periodic' },
  { value: 'LCR', label: 'Liquidity' },
  { value: 'INTEREST_RATE', label: 'Interest rate' },
];

export default function RegulatoryReports() {
  const { toast } = useToast();
  const [reportType, setReportType] = useState('BIEU_1A');
  const [reportDate, setReportDate] = useState(new Date().toISOString().split('T')[0]);
  const [generating, setGenerating] = useState(false);
  const [preview, setPreview] = useState(null);
  const [history, setHistory] = useState([]);

  const currentLabel = REPORT_TYPES.find(t => t.value === reportType)?.label || reportType;

  const handleGenerate = async (format) => {
    setGenerating(true);
    try {
      if (format === 'json') {
        const data = await regulatoryApi.getReport(reportType, reportDate);
        setPreview(data);
      } else {
        await regulatoryApi.downloadReport(reportType, reportDate, format);
      }

      setHistory(items => [{
        id: Date.now(),
        createdAt: new Date().toLocaleString('vi-VN'),
        reportType: currentLabel,
        reportDate,
        format: format.toUpperCase(),
      }, ...items].slice(0, 8));
      toast(`Generated ${currentLabel} as ${format.toUpperCase()}`, 'success');
    } catch (e) {
      toast('Report generation failed: ' + (e.message || ''), 'error');
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>Regulatory reports</h2>
          <p className="subtitle">Generate NHNN, CIC, CAR and liquidity reports from backend data.</p>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: '2rem' }}>
        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)', boxShadow: 'var(--shadow-sm)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1rem' }}>Report parameters</h3>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label>Report type</label>
            <select className="form-input" value={reportType} onChange={e => setReportType(e.target.value)}>
              {REPORT_TYPES.map(type => (
                <option key={type.value} value={type.value}>{type.label}</option>
              ))}
            </select>
          </div>

          <div className="form-group" style={{ marginBottom: '1.5rem' }}>
            <label>Reporting date</label>
            <input type="date" className="form-input" value={reportDate} onChange={e => setReportDate(e.target.value)} />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <button className="btn btn-primary" onClick={() => handleGenerate('excel')} disabled={generating}>
              {generating ? 'Generating...' : 'Generate Excel'}
            </button>
            <button className="btn btn-outline" onClick={() => handleGenerate('xml')} disabled={generating}>
              {generating ? 'Generating...' : 'Generate XML'}
            </button>
            <button className="btn btn-outline" onClick={() => handleGenerate('json')} disabled={generating}>
              Preview data
            </button>
          </div>
        </div>

        <div style={{ background: 'var(--bg-card)', padding: '1.5rem', borderRadius: 18, border: '1px solid var(--border)' }}>
          <h3 style={{ marginBottom: '1rem', color: 'var(--navy)', fontSize: '1rem' }}>Generation history</h3>
          {preview && (
            <pre style={{ maxHeight: 220, overflow: 'auto', background: 'var(--bg-muted)', padding: '0.75rem', borderRadius: 10, fontSize: '0.78rem', marginBottom: '1rem' }}>
              {JSON.stringify(preview, null, 2)}
            </pre>
          )}
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Created</th>
                  <th>Report</th>
                  <th>Period</th>
                  <th>Format</th>
                  <th>User</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {history.length === 0 ? (
                  <tr><td colSpan={6} className="empty-row"><div className="empty-state">No reports generated in this session.</div></td></tr>
                ) : history.map(item => (
                  <tr key={item.id}>
                    <td>{item.createdAt}</td>
                    <td>{item.reportType}</td>
                    <td>{item.reportDate}</td>
                    <td><span className="code-badge">{item.format}</span></td>
                    <td>Current user</td>
                    <td><span className="cell-sub">Generated</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
