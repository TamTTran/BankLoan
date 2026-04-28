import { httpClient } from '../../../shared/api/httpClient';

export const dashboardApi = {
  summary:           () => httpClient('GET', '/dashboard/summary'),
  capitalAdequacy:   () => httpClient('GET', '/capital-adequacy/latest'),
  overdueBuckets:    () => httpClient('GET', '/reports/overdue-buckets'),
  monthlyTrend:      (months = 6) => httpClient('GET', `/dashboard/monthly-trend?months=${months}`),
  // Fetches maker-checker queue so sidebar badge stays accurate before visiting /approvals.
  approvalsPending:  () => httpClient('GET', '/approval-requests'),
};
