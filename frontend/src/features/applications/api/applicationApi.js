import { httpClient } from '../../../shared/api/httpClient';

export const applicationApi = {
  list: (p = {}) => httpClient('GET', `/applications?${new URLSearchParams(p)}`),
  get: (id) => httpClient('GET', `/applications/${id}`),
  create: (body) => httpClient('POST', '/applications', body),
  submit: (id) => httpClient('POST', `/applications/${id}/submit`),
  approve: (id) => httpClient('POST', `/applications/${id}/approve`),
  reject: (id, body) => httpClient('POST', `/applications/${id}/reject`, body),
  preview: (body) => httpClient('POST', '/applications/preview', body)
};
