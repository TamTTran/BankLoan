import { getCurrentLocale } from '../i18n/locales';

export function formatCurrency(n) {
  if (n == null || n === '') return '—';
  return new Intl.NumberFormat(getCurrentLocale(), { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);
}
export function formatNumber(n) {
  if (n == null) return '—';
  return new Intl.NumberFormat(getCurrentLocale()).format(n);
}
export function formatDate(s) {
  if (!s) return '—';
  const d = new Date(s);
  if (isNaN(d)) return s;
  return d.toLocaleDateString(getCurrentLocale());
}
export function formatDateTime(s) {
  if (!s) return '—';
  return new Date(s).toLocaleString(getCurrentLocale(), { dateStyle: 'short', timeStyle: 'short' });
}
export function formatPercent(n) {
  if (n == null) return '—';
  return `${(n * 100).toFixed(2)}%`;
}
export function parseTenors(json) {
  try { return JSON.parse(json); } catch { return []; }
}
export function today() {
  return new Date().toISOString().substring(0, 10);
}

export const STATUS_LABELS = {
  Draft:'Nháp', Submitted:'Chờ duyệt', Approved:'Đã duyệt', Rejected:'Từ chối',
  PendingDisbursement:'Chờ giải ngân', Active:'Hoạt động', Overdue:'Quá hạn', Closed:'Đã đóng',
  Unpaid:'Chưa trả', Partial:'Trả một phần', Paid:'Đã trả', Inactive:'Không HĐ',
};

export const DPD_LABELS = {
  Current:'Hiện hành', DPD1_7:'DPD 1–7', DPD8_30:'DPD 8–30', DPD31Plus:'DPD 31+',
};

export const ROLE_LABELS = {
  Admin:'Quản trị', LoanOfficer:'Nhân viên tín dụng',
  Approver:'Phê duyệt', Collector:'Thu hồi nợ', Manager:'Quản lý',
};
