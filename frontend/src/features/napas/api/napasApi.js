import { httpClient } from '../../../shared/api/httpClient';

function cleanPayload(payload) {
  return Object.fromEntries(
    Object.entries(payload).filter(([, value]) => value !== '' && value !== null && value !== undefined)
  );
}

export const napasApi = {
  searchTransactions: (filters) => httpClient('POST', '/napas/transactions/search', cleanPayload(filters)),
  getTransaction: (transactionId) => httpClient('GET', `/napas/transactions/${encodeURIComponent(transactionId)}`),
  getTransactionByReference: (referenceId) => httpClient('GET', `/napas/transactions/by-reference/${encodeURIComponent(referenceId)}`),
  getDisbursementStatus: (referenceId) => httpClient('GET', `/napas/disbursements/${encodeURIComponent(referenceId)}/status`),
  getMerchant: () => httpClient('GET', '/napas/merchant'),
  getMerchantSettings: () => httpClient('GET', '/napas/merchant/settings'),
  listRecoFiles: (date) => httpClient('GET', `/napas/reconciliation/files?date=${encodeURIComponent(date)}`),
  downloadRecoFile: (date, type = 'settlement') =>
    httpClient('GET', `/napas/reconciliation/files/${encodeURIComponent(date)}/download?type=${encodeURIComponent(type)}`),
  matchRecoFile: (date) => httpClient('POST', `/napas/reconciliation/files/${encodeURIComponent(date)}/match`),
  generateQr: (body) => httpClient('POST', '/napas/qr/generate', cleanPayload(body)),
  getLoanQr: (loanId) => httpClient('GET', `/napas/qr/loan/${encodeURIComponent(loanId)}`),
  decodeQr: (qrString) => httpClient('POST', '/napas/qr/decode', { qrString }),
};
