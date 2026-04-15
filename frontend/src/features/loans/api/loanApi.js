import { httpClient } from '../../../shared/api/httpClient';

export const loanApi = {
  list: (p = {}) => httpClient('GET', `/loans?${new URLSearchParams(p)}`),
  get: (id) => httpClient('GET', `/loans/${id}`),
  createFromApp: (appId) => httpClient('POST', `/applications/${appId}/create-loan`),
  disburse: (id, body) => httpClient('POST', `/loans/${id}/disburse`, body),
  schedule: (id) => httpClient('GET', `/loans/${id}/schedule`),
  payments: (id) => httpClient('GET', `/loans/${id}/payments`),
  pay: (id, body) => httpClient('POST', `/loans/${id}/payments`, body),
  addNote: (id, body) => httpClient('POST', `/loans/${id}/collection-notes`, body),
  overdue: () => httpClient('GET', '/overdue')
};
