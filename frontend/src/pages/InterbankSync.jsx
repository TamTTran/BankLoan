import { useState, useEffect } from 'react';
import { httpClient } from '../shared/api/httpClient';
import { useToast } from '../context/ToastContext';

export default function InterbankSync() {
  const [journals, setJournals] = useState([]);
  const [loading, setLoading] = useState(true);
  const [syncingAll, setSyncingAll] = useState(false);
  const [syncingId, setSyncingId] = useState(null);
  
  const [statusFilter, setStatusFilter] = useState('All');
  const [dateFilter, setDateFilter] = useState(new Date().toISOString().split('T')[0]);
  const { toast } = useToast();

  const fetchJournals = async () => {
    try {
      setLoading(true);
      // httpClient builds query string automatically if we append it, but let's do it manually since it's simple
      const queryParams = new URLSearchParams();
      if (statusFilter !== 'All') queryParams.append('status', statusFilter);
      if (dateFilter) queryParams.append('businessDate', dateFilter);
      
      const res = await httpClient('GET', `/interbank/journals?${queryParams.toString()}`);
      setJournals(res);
    } catch (error) {
      toast('Failed to load journal entries', 'error');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchJournals();
  }, [statusFilter, dateFilter]);

  const handleSyncBatch = async () => {
    if (!dateFilter) {
      toast('Please select a business date to sync', 'warning');
      return;
    }
    try {
      setSyncingAll(true);
      await httpClient('POST', '/interbank/sync-journals', { businessDate: dateFilter });
      toast('Batch sync completed successfully', 'success');
      fetchJournals();
    } catch (error) {
      toast(error.message || 'Failed to sync batch', 'error');
    } finally {
      setSyncingAll(false);
    }
  };

  const handleRetrySingle = async (id) => {
    try {
      setSyncingId(id);
      await httpClient('POST', `/interbank/sync-journals/${id}`);
      toast('Journal synced successfully', 'success');
      fetchJournals();
    } catch (error) {
      toast(error.message || 'Failed to sync journal', 'error');
    } finally {
      setSyncingId(null);
    }
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'Synced':
        return <span className="badge status-approved">✅ Synced</span>;
      case 'Pending':
        return <span className="badge status-pending">⏳ Pending</span>;
      case 'Failed':
        return <span className="badge status-rejected">❌ Failed</span>;
      default:
        return <span className="badge">{status}</span>;
    }
  };

  return (
    <div className="fade-in">
      <div className="header-actions" style={{ marginBottom: '1.5rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 className="page-title">🏦 Interbank Sync Status</h1>
          <p className="page-subtitle" style={{ color: '#64748b', marginTop: '0.25rem' }}>
            Monitor and manage journal entry synchronization with the Core Banking system.
          </p>
        </div>
        <button
          onClick={handleSyncBatch}
          disabled={syncingAll}
          className="btn btn-primary"
        >
          {syncingAll ? '🔄 Syncing...' : '🔄 Sync All Pending'}
        </button>
      </div>

      <div className="card" style={{ padding: '1.5rem' }}>
        <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem' }}>
          <div style={{ flex: 1, maxWidth: '250px' }}>
            <label className="form-label">Business Date</label>
            <input
              type="date"
              value={dateFilter}
              onChange={(e) => setDateFilter(e.target.value)}
              className="form-input"
            />
          </div>
          <div style={{ flex: 1, maxWidth: '250px' }}>
            <label className="form-label">Status</label>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="form-input"
            >
              <option value="All">All Statuses</option>
              <option value="Pending">Pending</option>
              <option value="Synced">Synced</option>
              <option value="Failed">Failed</option>
            </select>
          </div>
        </div>

        <div className="table-responsive">
          <table className="table">
            <thead>
              <tr>
                <th>Reference No.</th>
                <th>Business Date</th>
                <th>Description</th>
                <th>Status</th>
                <th>Synced At</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="6" style={{ textAlign: 'center', padding: '2rem', color: '#64748b' }}>
                    Loading...
                  </td>
                </tr>
              ) : journals.length === 0 ? (
                <tr>
                  <td colSpan="6" style={{ textAlign: 'center', padding: '2rem', color: '#64748b' }}>
                    No journal entries found.
                  </td>
                </tr>
              ) : (
                journals.map((journal) => (
                  <tr key={journal.id}>
                    <td style={{ fontWeight: 500 }}>{journal.referenceNumber}</td>
                    <td>{journal.businessDate}</td>
                    <td style={{ maxWidth: '300px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {journal.description}
                    </td>
                    <td>{getStatusBadge(journal.syncStatus)}</td>
                    <td>{journal.syncedAt ? new Date(journal.syncedAt).toLocaleString() : '-'}</td>
                    <td style={{ textAlign: 'right' }}>
                      {(journal.syncStatus === 'Pending' || journal.syncStatus === 'Failed') && (
                        <button
                          onClick={() => handleRetrySingle(journal.id)}
                          disabled={syncingId === journal.id}
                          className="btn btn-secondary"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        >
                          {syncingId === journal.id ? '⏳' : '🔄 Retry'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
