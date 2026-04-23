import { httpClient } from '../../../shared/api/httpClient';

const withDateQuery = (path, dateStr) => {
  if (!dateStr) return path;
  return `${path}?date=${encodeURIComponent(dateStr)}`;
};

export const systemApi = {
  runEod: (dateStr) => httpClient('POST', withDateQuery('/jobs/eod', dateStr)),
  runOverdue: (dateStr) => httpClient('POST', withDateQuery('/jobs/overdue/run', dateStr)),

  getApprovals: () => httpClient('GET', '/approval-requests'),
  approveApproval: (id) => httpClient('POST', `/approval-requests/${id}/approve`),
  rejectApproval: (id, reason) =>
    httpClient('POST', `/approval-requests/${id}/reject`, { reason: reason || 'Rejected from approval queue' }),
  decideApproval: (id, decision, reason) => {
    if (decision === 'Approve') {
      return systemApi.approveApproval(id);
    }
    return systemApi.rejectApproval(id, reason);
  },
};
