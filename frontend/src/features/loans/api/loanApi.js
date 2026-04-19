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
  overdue: () => httpClient('GET', '/overdue'),
  
  // Advanced Lending Controls
  restructure: (id, body) => httpClient('POST', `/advanced-loans/${id}/restructure`, body),
  reschedule: (id, body) => httpClient('POST', `/advanced-loans/${id}/reschedule`, body),
  writeOff: (id, body) => httpClient('POST', `/advanced-loans/${id}/write-off`, body),
  
  // Adjustments & Corrections (Maker-Checker)
  manualAdjustment: (body) => httpClient('POST', `/adjustments/manual-adjustment`, body),
  disbursementCorrection: (body) => httpClient('POST', `/adjustments/disbursement-correction`, body)
};
