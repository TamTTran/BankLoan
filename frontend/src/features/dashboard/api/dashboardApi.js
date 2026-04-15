import { httpClient } from '../../../shared/api/httpClient';

export const dashboardApi = {
  summary: () => httpClient('GET', '/dashboard/summary')
};
