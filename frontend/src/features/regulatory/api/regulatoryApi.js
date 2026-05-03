import { httpClient } from '../../../shared/api/httpClient';
import { getToken } from '../../../shared/utils/authStorage';

const API_BASE = '/api';

function periodQuery(date) {
  const value = date ? new Date(date) : new Date();
  return `year=${value.getFullYear()}&month=${value.getMonth() + 1}`;
}

const REPORT_ENDPOINTS = {
  BIEU_1A: 'nhnn-classification',
  BIEU_1B: 'nhnn-provision',
  CAR: 'car',
  CIC: 'cic-periodic',
  LCR: 'liquidity',
  INTEREST_RATE: 'interest-rate',
};

function endpointFor(type) {
  return REPORT_ENDPOINTS[type] || REPORT_ENDPOINTS.BIEU_1A;
}

export const regulatoryApi = {
  getReport: (type, date) =>
    httpClient('GET', `/reports/regulatory/${endpointFor(type)}?${periodQuery(date)}`),

  downloadReport: async (type, date, format) => {
    const accept = format === 'xml'
      ? 'application/xml'
      : format === 'csv'
        ? 'text/csv'
        : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
    const token = getToken();
    const headers = { Accept: accept };
    if (token) headers.Authorization = `Bearer ${token}`;

    const res = await fetch(`${API_BASE}/reports/regulatory/${endpointFor(type)}?${periodQuery(date)}`, {
      method: 'GET',
      headers,
      credentials: 'include',
    });

    if (!res.ok) {
      const json = await res.json().catch(() => null);
      throw new Error(json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`);
    }

    return res.blob();
  },
};
