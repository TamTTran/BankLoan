import { httpClient } from '../../../shared/api/httpClient';

export const reportsApi = {
  getPortfolio: (asOfDate) =>
    httpClient('GET', `/reports/portfolio${asOfDate ? `?asOfDate=${asOfDate}` : ''}`),
  getOverdueMovement: (from, to) =>
    httpClient('GET', `/reports/overdue-movement?from=${from}&to=${to}`),
  getCollectionProductivity: (from, to) =>
    httpClient('GET', `/reports/collection-productivity?from=${from}&to=${to}`),
  getApprovalQueue: () =>
    httpClient('GET', '/reports/approval-queue'),
  getReconciliationExceptions: (status = '', page = 1) =>
    httpClient('GET', `/reconciliation/exceptions?status=${status}&page=${page}`),
  resolveReconciliationException: (id, payload) =>
    httpClient('POST', `/reconciliation/exceptions/${id}/resolve`, payload),
};
