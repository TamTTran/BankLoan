import { httpClient } from '../../../shared/api/httpClient';

export const dashboardApi = {
  summary:           () => httpClient('GET', '/dashboard/summary'),
  capitalAdequacy:   () => httpClient('GET', '/capital-adequacy/latest'),
  // Fetches the maker-checker pending queue so the Layout badge stays accurate
  // even before the user visits /approvals.
  approvalsPending:  () => httpClient('GET', '/approval-requests'),
};
