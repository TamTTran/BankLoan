import { httpClient } from '../../../shared/api/httpClient';

export const loanApi = {
  list: (p = {}) => httpClient('GET', `/loans?${new URLSearchParams(p)}`),
  get: (id) => httpClient('GET', `/loans/${id}`),
  createFromApp: (appId) => httpClient('POST', `/applications/${appId}/create-loan`),
  disburse: (id, body) => httpClient('POST', `/loans/${id}/disburse`, body),
  schedule: (id) => httpClient('GET', `/loans/${id}/schedule`),
  payments: (id) => httpClient('GET', `/loans/${id}/payments`),
  pay: (id, body) => httpClient('POST', `/loans/${id}/payments`, body),
  previewEarlySettle: (id, date) => httpClient('GET', `/loans/${id}/early-settle-preview?settlementDate=${date}`),
  earlySettle: (id, body) => httpClient('POST', `/loans/${id}/early-settle`, body),
  addNote: (id, body) => httpClient('POST', `/loans/${id}/collection-notes`, body),
  overdue: () => httpClient('GET', '/overdue'),

  // Advanced Lending Controls (all go via Maker-Checker approval)
  restructure:  (id, body) => httpClient('POST', `/loans/${id}/restructure`, body),
  reschedule:   (id, body) => httpClient('POST', `/loans/${id}/reschedule`, body),
  writeOff:     (id, body) => httpClient('POST', `/loans/${id}/write-off`, body),

  // Manual Adjustment (Maker-Checker)
  manualAdjustment: (id, body) => httpClient('POST', `/adjustments/manual-adjustment`, { loanId: id, ...body }),
  disbursementCorrection: (body) => httpClient('POST', `/adjustments/disbursement-correction`, body),

  // Payment Reversal
  requestReversal: (id, body) => httpClient('POST', `/loans/${id}/request-reversal`, body),
  getReversals:    (id)       => httpClient('GET',  `/loans/${id}/reversals`),
};

