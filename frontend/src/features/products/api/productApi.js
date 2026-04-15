import { httpClient } from '../../../shared/api/httpClient';

export const productApi = {
  list: () => httpClient('GET', '/loan-products'),
  get: (id) => httpClient('GET', `/loan-products/${id}`),
  create: (body) => httpClient('POST', '/loan-products', body),
  update: (id, body) => httpClient('PATCH', `/loan-products/${id}`, body)
};
