import { httpClient } from '../../../shared/api/httpClient';

export const EWALLET_PROVIDERS = ['MoMo', 'VNPay'];

export const ewalletApi = {
  listByCustomer: (customerId) => httpClient('GET', `/customers/${customerId}/ewallets`),
  link: (customerId, body) => httpClient('POST', `/customers/${customerId}/ewallets/link`, body),
  verifyOtp: (customerId, body) => httpClient('POST', `/customers/${customerId}/ewallets/verify-otp`, body),
  unlink: (customerId, walletId) => httpClient('DELETE', `/customers/${customerId}/ewallets/${walletId}`),
  payLoan: (loanId, body) => httpClient('POST', `/loans/${loanId}/pay-from-ewallet`, body),
  disburseLoan: (loanId, body) => httpClient('POST', `/loans/${loanId}/disburse-to-ewallet`, body),
  queryTransaction: (provider, transactionId) =>
    httpClient('GET', `/ewallet/transactions/${encodeURIComponent(transactionId)}?provider=${encodeURIComponent(provider)}`),
};
