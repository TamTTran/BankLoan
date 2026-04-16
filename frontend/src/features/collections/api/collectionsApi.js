import { httpClient } from '../../../shared/api/httpClient';

export const collectionsApi = {
  getMyQueue: () => httpClient('GET', '/collections/queue'),
  getAllQueue: () => httpClient('GET', '/collections/queue/all'),
  getSummary: () => httpClient('GET', '/collections/summary'),
  assignCollector: (loanId, collectorUserId) =>
    httpClient('POST', `/loans/${loanId}/assign-collector`, { collectorUserId }),
  addAction: (loanId, data) =>
    httpClient('POST', `/loans/${loanId}/collection-actions`, data),
  getHistory: (loanId) =>
    httpClient('GET', `/loans/${loanId}/collection-actions`),
};
