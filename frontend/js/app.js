/* app.js — LoanFlow Lite Main Application */

let currentPage = 'dashboard';
let customerPage = 1, appPage = 1, loanPage = 1;
let productList = [], customerCache = [];
let currentLoanId = null; // for detail modal

/* ================================================================
   INIT
================================================================ */
window.addEventListener('DOMContentLoaded', () => {
  const user = getUser();
  if (user && getToken()) {
    showMainApp(user);
  } else {
    showLogin();
  }
});

/* ================================================================
   AUTH
================================================================ */
function showLogin() {
  document.getElementById('screen-login').classList.remove('hidden');
  document.getElementById('screen-main').classList.add('hidden');
}

function showMainApp(user) {
  document.getElementById('screen-login').classList.add('hidden');
  document.getElementById('screen-main').classList.remove('hidden');

  document.getElementById('userName').textContent    = user.fullName || user.username;
  document.getElementById('userRole').textContent    = roleLabel(user.role);
  document.getElementById('userAvatar').textContent  = (user.fullName || user.username)[0].toUpperCase();
  document.getElementById('dashDate').textContent    =
    new Date().toLocaleDateString('vi-VN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });

  applyRolePermissions(user.role);

  // Navigate to hash or dashboard
  const hash = window.location.hash.replace('#', '');
  const validPages = ['dashboard','customers','products','applications','loans','overdue'];
  const startPage = validPages.includes(hash) ? hash : 'dashboard';
  navigateTo(startPage);
  loadPage(startPage);
}

function roleLabel(role) {
  const map = {
    Admin: 'Quản trị', LoanOfficer: 'Nhân viên tín dụng',
    Approver: 'Phê duyệt', Collector: 'Thu hồi nợ', Manager: 'Quản lý'
  };
  return map[role] || role;
}

function applyRolePermissions(role) {
  const hide = (...ids) => ids.forEach(id => document.getElementById(id)?.classList.add('hidden'));
  if (role === 'Collector') {
    hide('btnNewCustomer', 'btnNewApplication', 'btnNewProduct');
    hide('nav-products');
  }
  if (role === 'LoanOfficer') {
    hide('btnNewProduct');
  }
  if (role === 'Approver') {
    hide('btnNewCustomer', 'btnNewApplication', 'btnNewProduct');
  }
  if (role === 'Manager') {
    hide('btnNewCustomer', 'btnNewApplication', 'btnNewProduct');
  }
}

document.getElementById('loginForm').addEventListener('submit', async e => {
  e.preventDefault();
  const errEl = document.getElementById('loginError');
  errEl.classList.add('hidden');
  const btn = document.getElementById('loginBtn');
  setLoading(btn, true, 'Đang đăng nhập...');

  try {
    const res = await api.auth.login({
      username: document.getElementById('loginUsername').value.trim(),
      password: document.getElementById('loginPassword').value
    });
    setToken(res.token);
    setUser(res);
    showMainApp(res);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

// Quick-login buttons
document.querySelectorAll('.quick-login-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.getElementById('loginUsername').value = btn.dataset.user;
    document.getElementById('loginPassword').value = btn.dataset.pass;
    document.getElementById('loginForm').dispatchEvent(new Event('submit'));
  });
});

document.getElementById('logoutBtn').addEventListener('click', () => {
  clearToken();
  showLogin();
  window.location.hash = '';
});

/* ================================================================
   NAVIGATION
================================================================ */
document.querySelectorAll('.nav-item').forEach(item => {
  item.addEventListener('click', async e => {
    e.preventDefault();
    const page = item.dataset.page;
    navigateTo(page);
    await loadPage(page);
  });
});

async function loadPage(page) {
  switch (page) {
    case 'dashboard':    await loadDashboard(); break;
    case 'customers':    await loadCustomers(); break;
    case 'products':     await loadProducts();  break;
    case 'applications': await loadApplications(); break;
    case 'loans':        await loadLoans();     break;
    case 'overdue':      await loadOverdue();   break;
  }
}

/* ================================================================
   DASHBOARD
================================================================ */
async function loadDashboard() {
  try {
    const sum = await api.dashboard.summary();

    setValue('kpi-outstanding', formatCurrency(sum.totalOutstanding));
    setValue('kpi-active',      sum.totalActiveLoans ?? '—');
    setValue('kpi-pending',     sum.pendingApprovals ?? '—');
    setValue('kpi-overdue',     sum.overdueAccounts  ?? '—');
    setValue('kpi-disbursed',   formatCurrency(sum.totalDisbursedThisMonth));
    setValue('kpi-collected',   formatCurrency(sum.collectedThisMonth));

    // Badge on nav
    const badge = document.getElementById('badge-pending');
    const cnt = sum.pendingApprovals;
    badge.textContent = cnt > 0 ? cnt : '';

    // Overdue buckets bar chart
    const bucketsEl = document.getElementById('overdue-buckets');
    const buckets = sum.overdueBuckets || [];
    const total = buckets.reduce((s, b) => s + b.count, 0) || 1;
    const colors = { Current:'#10B981', DPD1_7:'#F59E0B', DPD8_30:'#F97316', DPD31Plus:'#EF4444' };
    bucketsEl.innerHTML = buckets.length ? buckets.map(b => `
      <div class="bucket-row">
        <span class="bucket-label">${dpdLabel(b.bucket)}</span>
        <div class="bucket-bar-wrap">
          <div class="bucket-bar" style="width:${Math.round(b.count/total*100)}%;background:${colors[b.bucket]||'#3B82F6'}"></div>
        </div>
        <span class="bucket-count">${b.count}</span>
        <span class="bucket-amount">${formatCurrency(b.totalAmount)}</span>
      </div>
    `).join('') : '<p class="no-data">Không có dữ liệu quá hạn 🎉</p>';

    // Loan status breakdown
    const statusEl = document.getElementById('loan-status-breakdown');
    const breakdown = sum.loanStatusBreakdown || [];
    const icn = { Active:'✅', PendingDisbursement:'⏳', Overdue:'🔴', Closed:'🔒' };
    statusEl.innerHTML = breakdown.length ? breakdown.map(s => `
      <div class="bucket-row">
        <span class="bucket-label">${icn[s.status]||'📄'} ${statusMap[s.status]?.[0]||s.status}</span>
        <span class="bucket-count">${s.count}</span>
        <span class="bucket-amount">${formatCurrency(s.totalAmount)}</span>
      </div>
    `).join('') : '<p class="no-data">Không có dữ liệu</p>';

  } catch (err) {
    toast(`Dashboard: ${err.message}`, 'error');
  }
}

function dpdLabel(b) { return dpdMap[b]?.[0] || b; }

function setValue(id, val) {
  const el = document.getElementById(id);
  if (el) el.textContent = val;
}

/* ================================================================
   CUSTOMERS
================================================================ */
async function loadCustomers(page = 1) {
  customerPage = page;
  const search = document.getElementById('customerSearch').value.trim();
  const body = document.getElementById('customersBody');
  body.innerHTML = loadingRow(8);

  try {
    const res = await api.customers.list({ search, page, pageSize: 15 });
    // Customers returns paged object directly (no {success,data} wrapper)
    const paged = res.items ? res : { items: Array.isArray(res) ? res : [], totalPages: 1, page: 1, hasPreviousPage: false, hasNextPage: false };
    customerCache = paged.items || [];
    renderCustomers(paged);
  } catch (err) {
    body.innerHTML = errorRow(8, err.message);
  }
}

function renderCustomers(paged) {
  const body = document.getElementById('customersBody');
  if (!paged.items?.length) {
    body.innerHTML = emptyRow(8);
    document.getElementById('customerPagination').innerHTML = '';
    return;
  }
  body.innerHTML = paged.items.map(c => `
    <tr>
      <td><span class="code-badge">#${c.id}</span></td>
      <td>
        <div class="cell-main">${c.fullName}</div>
        <div class="cell-sub">${c.address || ''}</div>
      </td>
      <td><span class="mono">${c.idNumber}</span></td>
      <td>${c.phone}</td>
      <td><span class="cell-sub">${c.dateOfBirth ? formatDate(c.dateOfBirth) : '—'}</span></td>
      <td>${statusBadge(c.status || 'Active')}</td>
      <td>
        <div class="action-btns">
          <button class="btn btn-sm btn-outline" onclick="editCustomer(${c.id})">✏️ Sửa</button>
          <button class="btn btn-sm btn-ghost" onclick="viewCustomerLoans(${c.id}, '${c.fullName}')">👁 Xem khoản vay</button>
        </div>
      </td>
    </tr>
  `).join('');
  renderPagination(document.getElementById('customerPagination'), paged, loadCustomers);
}

document.getElementById('btnSearchCustomer').addEventListener('click', () => loadCustomers(1));
document.getElementById('customerSearch').addEventListener('keydown', e => { if (e.key === 'Enter') loadCustomers(1); });

document.getElementById('btnNewCustomer').addEventListener('click', () => openCustomerModal());

function openCustomerModal(c = null) {
  const form = document.getElementById('customerForm');
  form.reset();
  document.getElementById('cNationalId').disabled = false;
  document.getElementById('customerFormError').classList.add('hidden');

  if (c) {
    document.getElementById('modalCustomerTitle').textContent = 'Cập nhật khách hàng';
    form.dataset.customerId = c.id;
    document.getElementById('cFullName').value   = c.fullName || '';
    document.getElementById('cIdNumber').value   = c.idNumber || '';
    document.getElementById('cIdNumber').disabled = true;
    document.getElementById('cPhone').value      = c.phone || '';
    document.getElementById('cDob').value        = c.dateOfBirth?.substring(0, 10) || '';
    document.getElementById('cAddress').value    = c.address || '';
  } else {
    document.getElementById('modalCustomerTitle').textContent = 'Thêm khách hàng mới';
    form.dataset.customerId = '';
  }
  openModal('modal-customer');
}

function editCustomer(id) {
  const c = customerCache.find(x => x.id === id);
  if (c) openCustomerModal(c);
  else api.customers.get(id).then(d => openCustomerModal(d.data || d)).catch(e => toast(e.message, 'error'));
}

function viewCustomerLoans(id, name) {
  navigateTo('loans');
  document.getElementById('loanSearch').value = '';
  loadLoans(1, id);
}

document.getElementById('customerForm').addEventListener('submit', async e => {
  e.preventDefault();
  const errEl = document.getElementById('customerFormError');
  errEl.classList.add('hidden');
  const btn = e.target.querySelector('[type=submit]');
  setLoading(btn, true);

  const customerId = document.getElementById('customerForm').dataset.customerId;
  const body = {
    fullName:    document.getElementById('cFullName').value.trim(),
    idNumber:    document.getElementById('cIdNumber').value.trim(),
    phone:       document.getElementById('cPhone').value.trim(),
    dateOfBirth: document.getElementById('cDob').value || null,
    address:     document.getElementById('cAddress').value.trim() || null,
  };

  try {
    if (customerId) {
      await api.customers.update(customerId, body);
      toast('Cập nhật khách hàng thành công!');
    } else {
      await api.customers.create(body);
      toast('Tạo khách hàng thành công!');
    }
    closeModal('modal-customer');
    await loadCustomers(customerPage);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

/* ================================================================
   LOAN PRODUCTS
================================================================ */
async function loadProducts() {
  const grid = document.getElementById('productsGrid');
  grid.innerHTML = '<div class="loading-shimmer" style="height:200px;border-radius:12px"></div>';
  try {
    productList = await api.products.list();
    if (!Array.isArray(productList) || !productList.length) {
      grid.innerHTML = '<div class="no-data-card">Chưa có sản phẩm vay nào</div>';
      return;
    }
    const freq_colors = ['#3B82F6', '#8B5CF6', '#10B981'];
    grid.innerHTML = productList.map((p, i) => `
      <div class="product-card">
        <div class="product-header" style="background:linear-gradient(135deg,${freq_colors[i%3]}22,${freq_colors[(i+1)%3]}11)">
          <div class="product-name">${p.productName}</div>
          <span class="badge badge-active">${p.status}</span>
        </div>
        <div class="product-body">
          <div class="product-stat-grid">
            <div class="product-stat">
              <div class="stat-label">Hạn mức tối thiểu</div>
              <div class="stat-value">${formatCurrency(p.minAmount)}</div>
            </div>
            <div class="product-stat">
              <div class="stat-label">Hạn mức tối đa</div>
              <div class="stat-value">${formatCurrency(p.maxAmount)}</div>
            </div>
            <div class="product-stat">
              <div class="stat-label">Lãi suất / tháng</div>
              <div class="stat-value highlight">${formatPercent(p.monthlyInterestRate)}</div>
            </div>
            <div class="product-stat">
              <div class="stat-label">Phí xử lý</div>
              <div class="stat-value">${formatCurrency(p.processingFee)}</div>
            </div>
          </div>
          <div class="product-tenors">
            <span class="stat-label">Kỳ hạn cho phép:</span>
            ${parseTenors(p.allowedTenorsJson).map(t => `<span class="tenor-chip">${t}M</span>`).join('')}
          </div>
        </div>
      </div>
    `).join('');
  } catch (err) {
    grid.innerHTML = `<div class="no-data-card" style="color:var(--red)">${err.message}</div>`;
  }
}

function parseTenors(json) {
  try { return JSON.parse(json); } catch { return []; }
}

/* ================================================================
   APPLICATIONS
================================================================ */
async function loadApplications(page = 1) {
  appPage = page;
  const search = document.getElementById('appSearch').value.trim();
  const status = document.getElementById('appStatusFilter').value;
  const body = document.getElementById('applicationsBody');
  body.innerHTML = loadingRow(8);

  try {
    const params = { page, pageSize: 15 };
    if (search) params.search = search;
    if (status) params.status = status;
    const res = await api.applications.list(params);
    renderApplications(res);
  } catch (err) {
    body.innerHTML = errorRow(8, err.message);
  }
}

function renderApplications(res) {
  const body = document.getElementById('applicationsBody');
  const user = getUser();
  const isApprover = user?.role === 'Approver' || user?.role === 'Admin';
  const isOfficer  = user?.role === 'LoanOfficer' || user?.role === 'Admin';

  if (!res?.items?.length) {
    body.innerHTML = emptyRow(8, 'Chưa có hồ sơ vay nào');
    document.getElementById('appPagination').innerHTML = '';
    return;
  }

  body.innerHTML = res.items.map(a => {
    const actions = [];
    if (a.status === 'Draft' && isOfficer)
      actions.push(`<button class="btn btn-sm btn-outline" onclick="submitApp(${a.id})">📤 Nộp hồ sơ</button>`);
    if (a.status === 'Submitted' && isApprover) {
      actions.push(`<button class="btn btn-sm btn-success" onclick="approveApp(${a.id})">✅ Duyệt</button>`);
      actions.push(`<button class="btn btn-sm btn-danger" onclick="rejectApp(${a.id})">❌ Từ chối</button>`);
    }
    if (a.status === 'Approved' && isOfficer)
      actions.push(`<button class="btn btn-sm btn-primary" onclick="createLoan(${a.id})">💰 Tạo khoản vay</button>`);

    return `
      <tr>
        <td><span class="code-badge">APP-${String(a.id).padStart(4,'0')}</span></td>
        <td>
          <div class="cell-main">${a.customer?.fullName || '—'}</div>
          <div class="cell-sub">${a.customer?.phone || ''}</div>
        </td>
        <td><span class="cell-sub">${a.product?.productName || '—'}</span></td>
        <td><strong>${formatCurrency(a.requestedAmount)}</strong></td>
        <td>${a.requestedTenorMonths} tháng</td>
        <td>${statusBadge(a.status)}</td>
        <td><span class="cell-sub">${formatDate(a.createdAt)}</span></td>
        <td><div class="action-btns">${actions.join('')}</div></td>
      </tr>
    `;
  }).join('');
  renderPagination(document.getElementById('appPagination'), res, loadApplications);
}

document.getElementById('btnSearchApp').addEventListener('click', () => loadApplications(1));
document.getElementById('appSearch').addEventListener('keydown', e => { if (e.key === 'Enter') loadApplications(1); });
document.getElementById('appStatusFilter').addEventListener('change', () => loadApplications(1));

document.getElementById('btnNewApplication').addEventListener('click', async () => {
  await Promise.all([loadCustomersForDropdown(), loadProductsForDropdown()]);
  document.getElementById('applicationForm').reset();
  document.getElementById('installmentPreview').classList.add('hidden');
  document.getElementById('appFormError').classList.add('hidden');
  openModal('modal-application');
});

async function loadCustomersForDropdown() {
  try {
    const res = await api.customers.list({ pageSize: 500 });
    const items = res.items || (Array.isArray(res) ? res : []);
    const sel = document.getElementById('appCustomerId');
    sel.innerHTML = '<option value="">-- Chọn khách hàng --</option>';
    items.forEach(c => {
      sel.insertAdjacentHTML('beforeend', `<option value="${c.id}">${c.fullName} — ${c.phone}</option>`);
    });
  } catch {}
}

async function loadProductsForDropdown() {
  if (!productList.length) {
    try { productList = await api.products.list(); } catch {}
  }
  const sel = document.getElementById('appProductId');
  sel.innerHTML = '<option value="">-- Chọn sản phẩm --</option>';
  productList.forEach(p => {
    sel.insertAdjacentHTML('beforeend',
      `<option value="${p.id}" data-tenors='${p.allowedTenorsJson}' data-max="${p.maxAmount}" data-min="${p.minAmount}">${p.productName} — ${formatPercent(p.monthlyInterestRate)}/tháng</option>`
    );
  });
  // Update tenor options when product changes
  sel.onchange = () => {
    const opt = sel.selectedOptions[0];
    if (!opt?.value) return;
    const tenors = parseTenors(opt.dataset.tenors || '[]');
    const tenorSel = document.getElementById('appTenor');
    tenorSel.innerHTML = tenors.map(t => `<option value="${t}">${t} tháng</option>`).join('');
  };
}

document.getElementById('applicationForm').addEventListener('submit', async e => {
  e.preventDefault();
  const errEl = document.getElementById('appFormError');
  errEl.classList.add('hidden');
  const btn = e.target.querySelector('[type=submit]');
  setLoading(btn, true);

  try {
    await api.applications.create({
      customerId:          +document.getElementById('appCustomerId').value,
      productId:           +document.getElementById('appProductId').value,
      requestedAmount:     +document.getElementById('appAmount').value,
      requestedTenorMonths:+document.getElementById('appTenor').value,
      purpose:              document.getElementById('appPurpose').value.trim(),
      remarks:              document.getElementById('appRemarks').value.trim() || null,
    });
    toast('Tạo hồ sơ vay thành công!');
    closeModal('modal-application');
    await loadApplications(1);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

// Preview installment
document.getElementById('btnPreviewInstallment').addEventListener('click', async () => {
  const productId = +document.getElementById('appProductId').value;
  const amount    = +document.getElementById('appAmount').value;
  const tenor     = +document.getElementById('appTenor').value;
  if (!productId || !amount || !tenor) {
    toast('Vui lòng chọn sản phẩm, nhập số tiền và kỳ hạn', 'warning'); return;
  }
  try {
    const prev = await api.applications.preview({ productId, amount, tenorMonths: tenor });
    document.getElementById('previewMonthly').textContent   = formatCurrency(prev.monthlyInstallment);
    document.getElementById('previewTotal').textContent     = formatCurrency(prev.totalRepayment);
    document.getElementById('previewInterest').textContent  = formatCurrency(prev.totalInterest);
    document.getElementById('previewFee').textContent       = formatCurrency(prev.processingFee);
    document.getElementById('installmentPreview').classList.remove('hidden');
  } catch (err) {
    toast(err.message, 'error');
  }
});

async function submitApp(id) {
  if (!await confirm2('Nộp hồ sơ để gửi phê duyệt?')) return;
  try {
    await api.applications.submit(id);
    toast('Hồ sơ đã được nộp!');
    await loadApplications(appPage);
  } catch (err) { toast(err.message, 'error'); }
}

async function approveApp(id) {
  if (!await confirm2('Xác nhận phê duyệt hồ sơ vay này?')) return;
  try {
    await api.applications.approve(id);
    toast('Hồ sơ đã được phê duyệt!');
    await loadApplications(appPage);
    await loadDashboard();
  } catch (err) { toast(err.message, 'error'); }
}

async function rejectApp(id) {
  const reason = prompt('Lý do từ chối:');
  if (reason === null) return; // cancelled
  try {
    await api.applications.reject(id, { reason });
    toast('Đã từ chối hồ sơ.', 'warning');
    await loadApplications(appPage);
    await loadDashboard();
  } catch (err) { toast(err.message, 'error'); }
}

async function createLoan(applicationId) {
  if (!await confirm2('Tạo khoản vay từ hồ sơ đã duyệt?')) return;
  try {
    const loan = await api.loans.createFromApp(applicationId);
    toast(`Khoản vay ${loan.loanAccountNo} đã được tạo! Tiến hành giải ngân.`);
    navigateTo('loans');
    await loadLoans();
  } catch (err) { toast(err.message, 'error'); }
}

/* ================================================================
   LOANS
================================================================ */
async function loadLoans(page = 1, customerId = null) {
  loanPage = page;
  const search = document.getElementById('loanSearch').value.trim();
  const status = document.getElementById('loanStatusFilter').value;
  const body   = document.getElementById('loansBody');
  body.innerHTML = loadingRow(9);

  try {
    const params = { page, pageSize: 15 };
    if (search)     params.search     = search;
    if (status)     params.status     = status;
    if (customerId) params.customerId = customerId;
    const res = await api.loans.list(params);
    renderLoans(res);
  } catch (err) {
    body.innerHTML = errorRow(9, err.message);
  }
}

function renderLoans(res) {
  const body = document.getElementById('loansBody');
  const user = getUser();
  const isOfficer = user?.role === 'LoanOfficer' || user?.role === 'Admin';
  const isCollector = user?.role === 'Collector' || user?.role === 'Admin';

  if (!res?.items?.length) {
    body.innerHTML = emptyRow(9, 'Chưa có khoản vay nào');
    document.getElementById('loanPagination').innerHTML = '';
    return;
  }

  body.innerHTML = res.items.map(l => {
    const actions = [`<button class="btn btn-sm btn-outline" onclick="openLoanDetail(${l.id})">📋 Chi tiết</button>`];
    if (l.status === 'PendingDisbursement' && isOfficer)
      actions.push(`<button class="btn btn-sm btn-primary" onclick="openDisburseModal(${l.id})">💵 Giải ngân</button>`);
    if (l.status === 'Active' && isOfficer)
      actions.push(`<button class="btn btn-sm btn-success" onclick="openPaymentModal(${l.id})">💰 Thu tiền</button>`);
    if ((l.status === 'Active' || l.status === 'Overdue') && isCollector)
      actions.push(`<button class="btn btn-sm btn-ghost" onclick="openCollectionNote(${l.id})">📝 Ghi chú</button>`);

    const outstanding = l.outstandingAmount ?? l.approvedAmount;
    const progress = l.approvedAmount ? Math.round((1 - outstanding / l.approvedAmount) * 100) : 0;

    return `
      <tr>
        <td><span class="code-badge">${l.loanAccountNo}</span></td>
        <td>
          <div class="cell-main">${l.customer?.fullName || '—'}</div>
          <div class="cell-sub">${l.customer?.phone || ''}</div>
        </td>
        <td><span class="cell-sub">${l.product?.productName || '—'}</span></td>
        <td>${formatCurrency(l.approvedAmount)}</td>
        <td>
          <strong style="color:${l.status==='Overdue'?'var(--red)':l.status==='Active'?'var(--amber)':'var(--text-secondary)'}">${formatCurrency(outstanding)}</strong>
          <div class="progress-bar"><div class="progress-fill" style="width:${progress}%"></div></div>
        </td>
        <td><span class="cell-sub">${formatPercent(l.monthlyInterestRate)}/T</span></td>
        <td><span class="cell-sub">${formatDate(l.maturityDate)}</span></td>
        <td>${statusBadge(l.status)}</td>
        <td><div class="action-btns">${actions.join('')}</div></td>
      </tr>
    `;
  }).join('');
  renderPagination(document.getElementById('loanPagination'), res, loadLoans);
}

document.getElementById('btnSearchLoan').addEventListener('click', () => loadLoans(1));
document.getElementById('loanSearch').addEventListener('keydown', e => { if (e.key === 'Enter') loadLoans(1); });
document.getElementById('loanStatusFilter').addEventListener('change', () => loadLoans(1));

/* ── Loan Detail Modal ── */
async function openLoanDetail(id) {
  currentLoanId = id;
  openModal('modal-loan-detail');

  ['detail-account','detail-customer','detail-product','detail-amount','detail-outstanding',
   'detail-rate','detail-tenor','detail-disbDate','detail-matDate','detail-status'].forEach(k => {
    const el = document.getElementById(k);
    if (el) el.textContent = '—';
  });
  document.getElementById('scheduleBody').innerHTML = loadingRow(7);
  document.getElementById('paymentsBody').innerHTML = loadingRow(5);

  try {
    const [loan, sched, payments] = await Promise.all([
      api.loans.get(id),
      api.loans.schedule(id),
      api.loans.payments(id),
    ]);

    setValue('detail-account',     loan.loanAccountNo);
    setValue('detail-customer',    loan.customer?.fullName || '—');
    setValue('detail-product',     loan.product?.productName || '—');
    setValue('detail-amount',      formatCurrency(loan.approvedAmount));
    setValue('detail-outstanding', formatCurrency(loan.outstandingAmount));
    setValue('detail-rate',        formatPercent(loan.monthlyInterestRate) + '/tháng');
    setValue('detail-tenor',       loan.tenorMonths + ' tháng');
    setValue('detail-disbDate',    formatDate(loan.disbursementDate));
    setValue('detail-matDate',     formatDate(loan.maturityDate));
    document.getElementById('detail-status').innerHTML = statusBadge(loan.status);

    // Repayment Schedule
    const sList = Array.isArray(sched) ? sched : sched?.schedules || [];
    document.getElementById('scheduleBody').innerHTML = sList.length ?
      sList.map(s => `
        <tr class="${s.status === 'Overdue' ? 'row-overdue' : s.status === 'Paid' ? 'row-paid' : ''}">
          <td class="text-center">${s.installmentNo}</td>
          <td>${formatDate(s.dueDate)}</td>
          <td>${formatCurrency(s.principalDue)}</td>
          <td>${formatCurrency(s.interestDue)}</td>
          <td><strong>${formatCurrency(s.principalDue + s.interestDue)}</strong></td>
          <td>${s.status === 'Paid' ? formatCurrency(s.principalPaid + s.interestPaid) : formatCurrency(s.remainingDueAmount)}</td>
          <td>${statusBadge(s.status)}</td>
        </tr>
      `).join('') : emptyRow(7, 'Chưa có lịch trả nợ');

    // Payments history
    const pList = Array.isArray(payments) ? payments : payments?.payments || [];
    document.getElementById('paymentsBody').innerHTML = pList.length ?
      pList.map(p => `
        <tr>
          <td>${formatDate(p.paymentDate)}</td>
          <td><strong>${formatCurrency(p.amount)}</strong></td>
          <td>${p.paymentMethod === 'Cash' ? 'Tiền mặt' : p.paymentMethod === 'BankTransfer' ? 'Chuyển khoản' : p.paymentMethod}</td>
          <td class="cell-sub">${p.remarks || '—'}</td>
          <td>${statusBadge('Paid')}</td>
        </tr>
      `).join('') : emptyRow(5, 'Chưa có lịch sử thanh toán');

  } catch (err) {
    toast(err.message, 'error');
    document.getElementById('scheduleBody').innerHTML = errorRow(7, err.message);
  }
}

/* ── Disburse Modal ── */
function openDisburseModal(loanId) {
  currentLoanId = loanId;
  document.getElementById('disburseDate').value = new Date().toISOString().substring(0, 10);
  document.getElementById('disburseError').classList.add('hidden');
  openModal('modal-disburse');
}

document.getElementById('btnSubmitDisburse').addEventListener('click', async () => {
  const errEl = document.getElementById('disburseError');
  errEl.classList.add('hidden');
  const btn = document.getElementById('btnSubmitDisburse');
  setLoading(btn, true);
  try {
    await api.loans.disburse(currentLoanId, {
      disbursementDate: document.getElementById('disburseDate').value
    });
    toast('Giải ngân thành công! Khoản vay đã kích hoạt.');
    closeModal('modal-disburse');
    await loadLoans(loanPage);
    await loadDashboard();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

/* ── Payment Modal ── */
function openPaymentModal(loanId) {
  currentLoanId = loanId;
  document.getElementById('payAmount').value  = '';
  document.getElementById('payDate').value    = new Date().toISOString().substring(0, 10);
  document.getElementById('payRemarks').value = '';
  document.getElementById('paymentError').classList.add('hidden');
  openModal('modal-payment');
}

document.getElementById('btnSubmitPayment').addEventListener('click', async () => {
  const errEl = document.getElementById('paymentError');
  errEl.classList.add('hidden');
  const btn = document.getElementById('btnSubmitPayment');
  setLoading(btn, true);
  try {
    await api.loans.pay(currentLoanId, {
      amount:        +document.getElementById('payAmount').value,
      paymentDate:   document.getElementById('payDate').value,
      paymentMethod: document.getElementById('payMethod').value,
      remarks:       document.getElementById('payRemarks').value || null,
    });
    toast('Ghi nhận thanh toán thành công!');
    closeModal('modal-payment');
    await loadLoans(loanPage);
    await loadDashboard();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

/* ── Collection Note Modal ── */
function openCollectionNote(loanId) {
  currentLoanId = loanId;
  document.getElementById('noteText').value   = '';
  document.getElementById('noteDate').value   = new Date().toISOString().substring(0, 10);
  document.getElementById('noteError').classList.add('hidden');
  openModal('modal-collection-note');
}

document.getElementById('btnSubmitNote').addEventListener('click', async () => {
  const errEl = document.getElementById('noteError');
  errEl.classList.add('hidden');
  const btn = document.getElementById('btnSubmitNote');
  const noteText = document.getElementById('noteText').value.trim();
  if (!noteText) { errEl.textContent = 'Vui lòng nhập nội dung ghi chú'; errEl.classList.remove('hidden'); return; }
  setLoading(btn, true);
  try {
    await api.loans.addCollectionNote(currentLoanId, {
      noteText,
      noteDate: document.getElementById('noteDate').value
    });
    toast('Đã thêm ghi chú thu hồi!');
    closeModal('modal-collection-note');
    if (currentPage === 'overdue') await loadOverdue();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    setLoading(btn, false);
  }
});

/* ================================================================
   OVERDUE
================================================================ */
async function loadOverdue() {
  const body = document.getElementById('overdueBody');
  body.innerHTML = loadingRow(8);

  try {
    const list = await api.loans.overdue();
    const items = Array.isArray(list) ? list : (list?.items || []);

    if (!items.length) {
      body.innerHTML = emptyRow(8, 'Không có khoản vay quá hạn 🎉');
      document.getElementById('overdueSummary').innerHTML = '';
      return;
    }

    // Bucket summary tags
    const bucketCounts = {};
    items.forEach(item => { bucketCounts[item.bucket] = (bucketCounts[item.bucket] || 0) + 1; });
    document.getElementById('overdueSummary').innerHTML =
      Object.entries(bucketCounts).map(([k, v]) =>
        `<div class="summary-tag">${dpdBadge(k)} <strong>${v} khoản</strong></div>`
      ).join('');

    const user = getUser();
    const isCollector = user?.role === 'Collector' || user?.role === 'Admin';

    body.innerHTML = items.map(item => `
      <tr>
        <td><span class="code-badge">${item.loanAccountNo}</span></td>
        <td>
          <div class="cell-main">${item.customerName || item.customer?.fullName || '—'}</div>
          <div class="cell-sub">${item.customerPhone || item.customer?.phone || '—'}</div>
        </td>
        <td>${formatCurrency(item.outstandingAmount ?? item.outstandingBalance)}</td>
        <td style="color:var(--red);font-weight:600">${formatCurrency(item.overdueAmount)}</td>
        <td style="color:var(--red);font-weight:700">${item.daysOverdue ?? '—'} ngày</td>
        <td>${dpdBadge(item.bucket)}</td>
        <td class="cell-sub">${item.lastCollectionNote || '—'}</td>
        <td>
          ${isCollector ? `<button class="btn btn-sm btn-ghost" onclick="openCollectionNote(${item.loanId || item.id})">📝 Ghi chú</button>` : ''}
          <button class="btn btn-sm btn-outline" onclick="openLoanDetail(${item.loanId || item.id})">👁 Chi tiết</button>
        </td>
      </tr>
    `).join('');
  } catch (err) {
    body.innerHTML = errorRow(8, err.message);
  }
}

/* ================================================================
   HELPERS
================================================================ */
// Expose statusMap to ui.js context for dpdLabel
const statusMap = {
  Draft:'Nháp', Submitted:'Chờ duyệt', Approved:'Đã duyệt', Rejected:'Từ chối',
  PendingDisbursement:'Chờ giải ngân', Active:'Hoạt động', Overdue:'Quá hạn',
  Closed:'Đã đóng', Unpaid:'Chưa trả', Partial:'Trả một phần', Paid:'Đã trả',
  Inactive:'Không HĐ',
};
