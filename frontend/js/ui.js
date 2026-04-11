/* ui.js — LoanFlow Lite UI Helpers */

/* ── Toast ── */
function toast(message, type = 'success') {
  const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.innerHTML = `<span class="toast-icon">${icons[type] || '✅'}</span><span>${message}</span>`;
  const container = document.getElementById('toast-container');
  container.appendChild(el);
  // Animate out before remove
  setTimeout(() => el.classList.add('toast-out'), 3000);
  setTimeout(() => el.remove(), 3400);
}

/* ── Button loading state ── */
function setLoading(btn, loading, text = 'Đang xử lý...') {
  if (typeof btn === 'string') btn = document.getElementById(btn);
  if (!btn) return;
  if (loading) {
    btn.dataset.origText = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> ${text}`;
  } else {
    btn.disabled = false;
    btn.innerHTML = btn.dataset.origText || btn.innerHTML;
  }
}

/* ── Formatters ── */
function formatCurrency(n) {
  if (n == null || n === '') return '—';
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);
}
function formatNumber(n) {
  if (n == null) return '—';
  return new Intl.NumberFormat('vi-VN').format(n);
}
function formatDate(s) {
  if (!s) return '—';
  const d = new Date(s);
  if (isNaN(d)) return s; // ISO date string like "2026-03-05"
  return d.toLocaleDateString('vi-VN');
}
function formatDateTime(s) {
  if (!s) return '—';
  return new Date(s).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
}
function formatPercent(n) {
  if (n == null) return '—';
  return `${(n * 100).toFixed(2)}%`;
}

/* ── Status Badge ── */
const statusMap = {
  // Application statuses
  Draft:       ['Nháp',          'draft'],
  Submitted:   ['Chờ duyệt',    'submitted'],
  Approved:    ['Đã duyệt',     'approved'],
  Rejected:    ['Từ chối',      'rejected'],
  // Loan statuses
  PendingDisbursement: ['Chờ giải ngân', 'pending'],
  Active:      ['Hoạt động',    'active'],
  Overdue:     ['Quá hạn',      'overdue'],
  Closed:      ['Đã đóng',      'closed'],
  // Installment
  Unpaid:      ['Chưa trả',     'pending'],
  Partial:     ['Trả một phần', 'partial'],
  Paid:        ['Đã trả',       'approved'],
  // Customer
  Inactive:    ['Không hoạt động', 'cancelled'],
};
function statusBadge(s) {
  const [label, cls] = statusMap[s] || [s, 'draft'];
  return `<span class="badge badge-${cls}">${label}</span>`;
}

/* ── DPD Badge ── */
const dpdMap = {
  Current:   ['Hiện hành', 'dpd-current'],
  DPD1_7:    ['DPD 1–7',   'dpd-1-7'],
  DPD8_30:   ['DPD 8–30',  'dpd-8-30'],
  DPD31Plus: ['DPD 31+',   'dpd-31plus'],
};
function dpdBadge(bucket) {
  const [label, cls] = dpdMap[bucket] || [bucket, 'dpd-current'];
  return `<span class="badge ${cls}">${label}</span>`;
}

/* ── Pagination ── */
function renderPagination(containerEl, paged, onPageChange) {
  if (!containerEl) return;
  containerEl.innerHTML = '';
  if (!paged || paged.totalPages <= 1) return;

  const mkBtn = (text, page, disabled = false, active = false) => {
    const b = document.createElement('button');
    b.innerHTML = text;
    b.disabled = disabled;
    if (active) b.classList.add('active');
    b.onclick = () => onPageChange(page);
    return b;
  };

  containerEl.appendChild(mkBtn('←', paged.page - 1, !paged.hasPreviousPage));

  // Show max 7 pages
  const start = Math.max(1, paged.page - 3);
  const end   = Math.min(paged.totalPages, paged.page + 3);
  for (let i = start; i <= end; i++) {
    containerEl.appendChild(mkBtn(i, i, false, i === paged.page));
  }

  containerEl.appendChild(mkBtn('→', paged.page + 1, !paged.hasNextPage));

  // Total info
  const info = document.createElement('span');
  info.className = 'page-info';
  info.textContent = `${paged.page}/${paged.totalPages} (${formatNumber(paged.totalCount || 0)} bản ghi)`;
  containerEl.appendChild(info);
}

/* ── Modal ── */
function openModal(id) {
  const el = document.getElementById(id);
  if (!el) return;
  el.classList.remove('hidden');
  el.classList.add('modal-open');
  document.body.style.overflow = 'hidden';
}
function closeModal(id) {
  const el = document.getElementById(id);
  if (!el) return;
  el.classList.add('modal-closing');
  setTimeout(() => {
    el.classList.add('hidden');
    el.classList.remove('modal-open', 'modal-closing');
    document.body.style.overflow = '';
  }, 180);
}

// Close on backdrop click or [data-close]
document.addEventListener('click', e => {
  const btn = e.target.closest('[data-close]');
  if (btn) { closeModal(btn.dataset.close); return; }
  if (e.target.classList.contains('modal-backdrop')) {
    const m = e.target.closest('.modal');
    if (m) closeModal(m.id);
  }
});

// Close on Escape
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') {
    const open = document.querySelector('.modal-open');
    if (open) closeModal(open.id);
  }
});

/* ── Page Navigation ── */
function navigateTo(page) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  const pg  = document.getElementById(`page-${page}`);
  const nav = document.getElementById(`nav-${page}`);
  if (pg)  pg.classList.add('active');
  if (nav) nav.classList.add('active');
  // Update URL hash for bookmarkability
  window.location.hash = page;
}

/* ── Empty state ── */
function emptyRow(cols, msg = 'Không có dữ liệu') {
  return `<tr><td colspan="${cols}" class="empty-row"><div class="empty-state"><span class="empty-icon">📭</span><p>${msg}</p></div></td></tr>`;
}
function loadingRow(cols) {
  return `<tr><td colspan="${cols}" class="empty-row"><div class="loading-shimmer"></div></td></tr>`;
}
function errorRow(cols, msg) {
  return `<tr><td colspan="${cols}" class="empty-row" style="color:var(--red)">${msg}</td></tr>`;
}

/* ── Confirm dialog ── */
function confirm2(message) {
  return new Promise(resolve => {
    const modal = document.getElementById('modal-confirm');
    document.getElementById('confirmMsg').textContent = message;
    openModal('modal-confirm');
    const yes = document.getElementById('confirmYes');
    const no  = document.getElementById('confirmNo');
    const cleanup = (val) => {
      closeModal('modal-confirm');
      yes.onclick = null;
      no.onclick  = null;
      resolve(val);
    };
    yes.onclick = () => cleanup(true);
    no.onclick  = () => cleanup(false);
  });
}
