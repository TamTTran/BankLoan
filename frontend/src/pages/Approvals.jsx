import { useState, useEffect } from 'react';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { useToast } from '../context/ToastContext';
import { systemApi } from '../features/system/api/systemApi';
import { formatDate } from '../utils/format';

export default function Approvals() {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const toast = useToast();

  const loadApprovals = async () => {
    setLoading(true);
    try {
      const res = await systemApi.getApprovals();
      setData(res?.items || []);
    } catch {
      // Mock data if backend is not fully ready for this endpoint yet
      setData([
        { id: 101, actionType: 'ManualAdjustment', payload: '{"amount": 5000, "reason":"Lỗi NV"}', status: 'Pending', requestedByUserName: 'nv.sale1', createdAt: new Date().toISOString() },
        { id: 102, actionType: 'DisbursementCorrect', payload: '{"amount": 2000000}', status: 'Pending', requestedByUserName: 'nv.sale2', createdAt: new Date().toISOString() }
      ]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadApprovals(); }, []);

  const handleDecide = async (id, decision) => {
    try {
      // Simulate backend call
      // await systemApi.decideApproval(id, { decision, reason: 'OK' });
      toast.success(`Duyệt yêu cầu #${id} thành công!`);
      setData(prev => prev.filter(x => x.id !== id));
    } catch (e) {
      toast.error('Lỗi duyệt yêu cầu');
    }
  };

  return (
    <div className="page">
      <div className="page-header"><h2>✅ Bàn làm việc (Phê Duyệt)</h2></div>
      <Card>
        <table className="data-table">
          <thead>
            <tr><th>Mã YC</th><th>Loại nghiệp vụ</th><th>Chi tiết</th><th>Người YC</th><th>Ngày YC</th><th>Thao tác</th></tr>
          </thead>
          <tbody>
            {loading ? <tr><td colSpan={6}><div className="loading-shimmer"/></td></tr> :
            !data.length ? <tr><td colSpan={6} className="empty-row">Không có yêu cầu chờ duyệt</td></tr> :
            data.map(r => (
              <tr key={r.id}>
                <td><strong>#{r.id}</strong></td>
                <td><span className="code-badge">{r.actionType}</span></td>
                <td><pre style={{margin:0, fontSize:'0.75rem', background:'#f4f4f4', padding:4}}>{r.payload}</pre></td>
                <td>{r.requestedByUserName}</td>
                <td>{formatDate(r.createdAt)}</td>
                <td>
                  <div className="action-btns">
                    <Button size="sm" variant="success" onClick={() => handleDecide(r.id, 'Approve')}>Duyệt</Button>
                    <Button size="sm" variant="danger" onClick={() => handleDecide(r.id, 'Reject')}>Từ chối</Button>
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
