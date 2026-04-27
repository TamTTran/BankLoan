import { httpClient } from '../../../shared/api/httpClient';

const withDateQuery = (path, dateStr) => {
  if (!dateStr) return path;
  return `${path}?date=${encodeURIComponent(dateStr)}`;
};

export const systemApi = {
  // Batch jobs
  runEod:     (dateStr) => httpClient('POST', withDateQuery('/jobs/eod', dateStr)),
  runOverdue: (dateStr) => httpClient('POST', withDateQuery('/jobs/overdue/run', dateStr)),

  // Approval requests
  getApprovals:    ()             => httpClient('GET',  '/approval-requests'),
  approveApproval: (id)           => httpClient('POST', `/approval-requests/${id}/approve`),
  rejectApproval:  (id, reason)   =>
    httpClient('POST', `/approval-requests/${id}/reject`, { reason: reason || 'Rejected from approval queue' }),
  decideApproval: (id, decision, reason) => {
    if (decision === 'Approve') return systemApi.approveApproval(id);
    return systemApi.rejectApproval(id, reason);
  },

  // BOD (Beginning of Day)
  getBodStatus: ()     => httpClient('GET',  '/bod/status'),
  openBod:      (date) => httpClient('POST', `/bod/open${date ? `?date=${date}` : ''}`),

  // Accounting Period
  getAccountingPeriod: ()           => httpClient('GET',  '/accounting-periods/current'),
  openPeriod:          (year, month)=> httpClient('POST', '/accounting-periods/open', { year, month }),
  closePeriod:         (year, month)=> httpClient('POST', '/accounting-periods/close', { year, month }),

  // NPL Reports
  getNplSummary:    (asOfDate) => httpClient('GET', `/reports/npl/summary${asOfDate ? `?asOfDate=${asOfDate}` : ''}`),
  getNplByProduct:  (asOfDate) => httpClient('GET', `/reports/npl/by-product${asOfDate ? `?asOfDate=${asOfDate}` : ''}`),
  getNplByOfficer:  (asOfDate) => httpClient('GET', `/reports/npl/by-officer${asOfDate ? `?asOfDate=${asOfDate}` : ''}`),
};

