import { httpClient } from '../../../shared/api/httpClient';

export const systemApi = {
  // EOD & Overdue Jobs
  runEod: (dateStr) => httpClient('POST', `/jobs/eod${dateStr ? `?date=${dateStr}` : ''}`),
  runOverdue: (dateStr) => httpClient('POST', `/jobs/overdue/run${dateStr ? `?date=${dateStr}` : ''}`),

  // Approvals (Maker-Checker)
  getApprovals: () => httpClient('GET', `/approvals`), // Assuming an endpoint exists or we mock it
  applyApproval: (id) => httpClient('POST', `/adjustments/${id}/apply`),
  decideApproval: (id, decision) => httpClient('POST', `/approvals/${id}/decide`, decision)
};
