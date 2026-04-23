import { useState } from 'react';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { useToast } from '../context/ToastContext';
import { systemApi } from '../features/system/api/systemApi';

const getProcessedCount = (type, result) => {
  if (type === 'eod') {
    return result?.overdueResult?.totalLoansProcessed ?? 0;
  }

  return result?.totalLoansProcessed ?? 0;
};

export default function SystemOps() {
  const [dateStr, setDateStr] = useState('');
  const [runningJob, setRunningJob] = useState(null);
  const { toast } = useToast();

  const handleRunJobs = async (type) => {
    setRunningJob(type);

    try {
      const result = type === 'eod'
        ? await systemApi.runEod(dateStr)
        : await systemApi.runOverdue(dateStr);

      const processed = getProcessedCount(type, result);
      const label = type === 'eod' ? 'EOD' : 'overdue update';
      toast(`Ran ${label} successfully. Loans processed: ${processed}`, 'success');
    } catch (e) {
      toast(e.message || 'System operation failed', 'error');
    } finally {
      setRunningJob(null);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>System Operations</h2>
      </div>

      <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem', flexWrap: 'wrap' }}>
        <Card style={{ flex: '1 1 420px' }}>
          <h3>Batch Jobs</h3>
          <p style={{ color: 'var(--text-muted)' }}>
            Run end-of-day processing or re-run overdue aging for the selected business date.
          </p>
          <div style={{ marginTop: '1rem', display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <div style={{ width: '200px' }}>
              <Input type="date" label="Business Date" value={dateStr} onChange={(e) => setDateStr(e.target.value)} />
            </div>
            <Button
              variant="primary"
              onClick={() => handleRunJobs('eod')}
              disabled={runningJob !== null}
            >
              {runningJob === 'eod' ? 'Running EOD...' : 'Run End-Of-Day'}
            </Button>
            <Button
              variant="outline"
              onClick={() => handleRunJobs('overdue')}
              disabled={runningJob !== null}
            >
              {runningJob === 'overdue' ? 'Running overdue...' : 'Run Overdue Update'}
            </Button>
          </div>
        </Card>

        <Card style={{ flex: '1 1 320px' }}>
          <h3>Backup & Data</h3>
          <p style={{ color: 'var(--text-muted)' }}>
            Database backup and restore operations are handled from the production terminal.
          </p>
          <div style={{ marginTop: '1rem' }}>
            <Button variant="outline" disabled>Run Backup Now</Button>
            <p style={{ fontSize: '0.8rem', marginTop: '0.5rem' }}>
              Run .\scripts\backup.ps1 from the production terminal.
            </p>
          </div>
        </Card>
      </div>
    </div>
  );
}
