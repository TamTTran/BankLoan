import { httpClient } from '../../../shared/api/httpClient';

export const amlApi = {
  screenCustomer: (customerId) => httpClient('POST', `/aml/screen/${customerId}`),
  getAlerts: (page = 1, pageSize = 20) => httpClient('GET', `/aml/alerts?page=${page}&pageSize=${pageSize}`),
  getStrList: (page = 1, pageSize = 20) => httpClient('GET', `/aml/str?page=${page}&pageSize=${pageSize}`),
  submitStr: (body) => httpClient('POST', '/aml/str', body),
};
