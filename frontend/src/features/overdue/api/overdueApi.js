import { httpClient } from '../../../shared/api/httpClient';

export const overdueApi = {
  list: () => httpClient('GET', '/overdue')
};
