import { httpClient } from '../../../shared/api/httpClient';

export const authApi = {
  login: (body) => httpClient('POST', '/auth/login', body),
  refresh: () => httpClient('POST', '/auth/refresh')
};
