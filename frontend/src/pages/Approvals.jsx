import { useEffect, useState } from 'react';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { useToast } from '../context/ToastContext';
import { systemApi } from '../features/system/api/systemApi';
import { formatDate } from '../utils/format';

export default function Approvals() {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [processingId, setProcessingId] = useState(null);
  const { toast } = useToast();

  const loadApprovals = async () => {
    setLoading(true);

    try {
      const res = await systemApi.getApprovals();
      setData(res?.items || []);
    } catch (e) {
      toast(e.message || 'Could not load approval requests', 'error');
      setData([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApprovals();
  }, []);

  const handleDecide = async (id, decision) => {
    setProcessingId(id);

    try {
      await systemApi.decideApproval(id, decision, 'Processed from approval queue');
      toast(`${decision === 'Approve' ? 'Approved' : 'Rejected'} request #${id}.`, 'success');
      setData((prev) => prev.filter((x) => x.id !== id));
    } catch (e) {
      toast(e.message || 'Approval action failed', 'error');
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <h2>Approval Queue</h2>
      </div>
      <Card>
        <table className="data-table">
          <thead>
            <tr>
              <th>Request ID</th>
              <th>Action</th>
              <th>Payload</th>
              <th>Requested By</th>
              <th>Requested At</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={6}><div className="loading-shimmer" /></td>
              </tr>
            ) : !data.length ? (
              <tr>
                <td colSpan={6} className="empty-row">No pending approval requests</td>
              </tr>
            ) : data.map((request) => (
              <tr key={request.id}>
                <td><strong>#{request.id}</strong></td>
                <td><span className="code-badge">{request.actionType}</span></td>
                <td>
                  <pre style={{ margin: 0, fontSize: '0.75rem', background: '#f4f4f4', padding: 4 }}>
                    {request.payload}
                  </pre>
                </td>
                <td>{request.requestedByUserName}</td>
                <td>{formatDate(request.createdAt)}</td>
                <td>
                  <div className="action-btns">
                    <Button
                      size="sm"
                      variant="success"
                      onClick={() => handleDecide(request.id, 'Approve')}
                      disabled={processingId !== null}
                    >
                      {processingId === request.id ? 'Working...' : 'Approve'}
                    </Button>
                    <Button
                      size="sm"
                      variant="danger"
                      onClick={() => handleDecide(request.id, 'Reject')}
                      disabled={processingId !== null}
                    >
                      Reject
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}
