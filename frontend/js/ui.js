/* ui.js — LoanFlow Lite UI Helpers */

/* ── Toast ── */
function toast(message, type = 'success') {
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.textContent = message;
  document.getElementById('toast-container').appendChild(el);
  setTimeout(() => el.remove(), 3500);
}

/* ── Loading ── */
function setLoading(btnId, loading) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.disabled = loading;
  btn.textContent = loading ? 'Đang xử lý...' : btn.dataset.label || btn.textContent;
}

/* ── Formatters ── */
function formatCurrency(n) {
  if (n == null) return '—';
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);
}
function formatDate(s) {
  if (!s) return '—';
  return new Date(s).toLocaleDateString('vi-VN');
}
function formatDateTime(s) {
  if (!s) return '—';
  return new Date(s).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
}

/* ── Status Badge ── */
const statusMap = {
  Draft: ['Nháp', 'draft'],
  Submitted: ['Chờ duyệt', 'submitted'],
  UnderReview: ['Đang xét', 'underreview'],
  Approved: ['Đã duyệt', 'approved'],
  Rejected: ['Từ chối', 'rejected'],
  Cancelled: ['Đã hủy', 'cancelled'],
  Disbursed: ['Đã giải ngân', 'disbursed'],
  Active: ['Hoạt động', 'active'],
  Closed: ['Đã đóng', 'closed'],
  Defaulted: ['Nợ xấu', 'defaulted'],
  Pending: ['Chờ thanh toán', 'submitted'],
  Paid: ['Đã thanh toán', 'approved'],
  PartiallyPaid: ['Thanh toán một phần', 'underreview'],
  Overdue: ['Quá hạn', 'rejected'],
};
function statusBadge(s) {
  const [label, cls] = statusMap[s] || [s, 'draft'];
  return `<span class="status-badge status-${cls}">${label}</span>`;
}

/* ── DPD Badge ── */
const dpdClassMap = {
  Current: 'dpd-current',
  DPD1_7: 'dpd-1-7',
  DPD8_30: 'dpd-8-30',
  DPD31_90: 'dpd-31-90',
  DPD90Plus: 'dpd-90plus',
};
const dpdLabelMap = {
  Current: 'Hiện hành',
  DPD1_7: 'DPD 1-7',
  DPD8_30: 'DPD 8-30',
  DPD31_90: 'DPD 31-90',
  DPD90Plus: 'DPD 90+',
};
function dpdBadge(bucket) {
  const cls = dpdClassMap[bucket] || '';
  const label = dpdLabelMap[bucket] || bucket;
  return `<span class="status-badge ${cls}">${label}</span>`;
}

/* ── Pagination ── */
function renderPagination(containerEl, paged, onPageChange) {
  containerEl.innerHTML = '';
  if (paged.totalPages <= 1) return;

  const prev = document.createElement('button');
  prev.textContent = '←';
  prev.disabled = !paged.hasPreviousPage;
  prev.onclick = () => onPageChange(paged.page - 1);
  containerEl.appendChild(prev);

  for (let i = 1; i <= paged.totalPages; i++) {
    const btn = document.createElement('button');
    btn.textContent = i;
    btn.className = i === paged.page ? 'active' : '';
    btn.onclick = () => onPageChange(i);
    containerEl.appendChild(btn);
  }

  const next = document.createElement('button');
  next.textContent = '→';
  next.disabled = !paged.hasNextPage;
  next.onclick = () => onPageChange(paged.page + 1);
  containerEl.appendChild(next);
}

/* ── Modal ── */
function openModal(id) { document.getElementById(id).classList.remove('hidden'); }
function closeModal(id) { document.getElementById(id).classList.add('hidden'); }

document.addEventListener('click', e => {
  const btn = e.target.closest('[data-close]');
  if (btn) closeModal(btn.dataset.close);
  if (e.target.classList.contains('modal-backdrop')) {
    const modalId = e.target.closest('.modal')?.id;
    if (modalId) closeModal(modalId);
  }
});

/* ── Page Navigation ── */
function navigateTo(page) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  const pg = document.getElementById(`page-${page}`);
  const nav = document.getElementById(`nav-${page}`);
  if (pg) pg.classList.add('active');
  if (nav) nav.classList.add('active');
}
