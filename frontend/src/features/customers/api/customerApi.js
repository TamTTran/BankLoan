import { httpClient } from '../../../shared/api/httpClient';

export const customerApi = {
  list: (p = {}) => httpClient('GET', `/customers?${new URLSearchParams(p)}`),
  get: (id) => httpClient('GET', `/customers/${id}`),
  create: (body) => httpClient('POST', '/customers', body),
  update: (id, body) => httpClient('PATCH', `/customers/${id}`, body),
  checkCredit: (id) => httpClient('POST', `/customers/${id}/check-credit`),
  verifyEkyc: (id) => httpClient('POST', `/ekyc/verify/${id}`, { idType: 'CCCD' }),
  wallets: (id) => httpClient('GET', `/customers/${id}/ewallets`),
  linkWallet: (id, body) => httpClient('POST', `/customers/${id}/ewallets/link`, body),
  verifyWalletOtp: (id, body) => httpClient('POST', `/customers/${id}/ewallets/verify-otp`, body),
  unlinkWallet: (id, walletId) => httpClient('DELETE', `/customers/${id}/ewallets/${walletId}`)
};
