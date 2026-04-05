/* app.js — LoanFlow Lite Main Application */

let currentPage = 'dashboard';
let customerPage = 1, appPage = 1, loanPage = 1;
let customerList = [], productList = [];

/* ====== INIT ====== */
window.addEventListener('DOMContentLoaded', () => {
  const user = getUser();
  if (user && getToken()) {
    showMainApp(user);
  } else {
    showLogin();
  }
});

/* ====== AUTH ====== */
function showLogin() {
  document.getElementById('screen-login').classList.add('active');
  document.getElementById('screen-main').classList.add('hidden');
}

function showMainApp(user) {
  document.getElementById('screen-login').classList.remove('active');
  document.getElementById('screen-main').classList.remove('hidden');

  document.getElementById('userName').textContent = user.fullName || user.username;
  document.getElementById('userRole').textContent = user.role;
  document.getElementById('userAvatar').textContent = (user.fullName || user.username)[0].toUpperCase();

  applyRolePermissions(user.role);
  navigateTo('dashboard');
  loadDashboard();

  document.getElementById('dashDate').textContent = new Date().toLocaleDateString('vi-VN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
}

function applyRolePermissions(role) {
  // Hide write actions for Collector
  if (role === 'Collector') {
    document.getElementById('btnNewCustomer')?.classList.add('hidden');
    document.getElementById('btnNewApplication')?.classList.add('hidden');
    document.getElementById('btnNewProduct')?.classList.add('hidden');
  }
  // Hide products/approval queue if Collector
}

document.getElementById('loginForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const errEl = document.getElementById('loginError');
  errEl.classList.add('hidden');
  const btn = document.getElementById('loginBtn');
  btn.disabled = true;
  btn.textContent = 'Đang đăng nhập...';

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
    btn.disabled = false;
    btn.textContent = 'Đăng nhập';
  }
});

document.getElementById('logoutBtn').addEventListener('click', () => {
  clearToken();
  showLogin();
});

/* ====== NAVIGATION ====== */
document.querySelectorAll('.nav-item').forEach(item => {
  item.addEventListener('click', async (e) => {
    e.preventDefault();
    const page = item.dataset.page;
    currentPage = page;
    navigateTo(page);
    await loadPage(page);
  });
});

async function loadPage(page) {
  if (page === 'dashboard') await loadDashboard();
  else if (page === 'customers') await loadCustomers();
  else if (page === 'products') await loadProducts();
  else if (page === 'applications') await loadApplications();
  else if (page === 'loans') await loadLoans();
  else if (page === 'overdue') await loadOverdue();
}

/* ====== DASHBOARD ====== */
async function loadDashboard() {
  try {
    const summary = await api.dashboard.summary();

    document.getElementById('kpi-outstanding').textContent = formatCurrency(summary.totalOutstanding);
    document.getElementById('kpi-active').textContent = summary.totalActiveLoans;
    document.getElementById('kpi-pending').textContent = summary.pendingApprovals;
    document.getElementById('kpi-overdue').textContent = summary.overdueAccounts;
    document.getElementById('kpi-disbursed').textContent = formatCurrency(summary.totalDisbursedThisMonth);
    document.getElementById('kpi-collected').textContent = formatCurrency(summary.collectedThisMonth);

    // Update badge on nav
    const badge = document.getElementById('badge-pending');
    badge.textContent = summary.pendingApprovals > 0 ? summary.pendingApprovals : '';

    // Overdue buckets
    const bucketsEl = document.getElementById('overdue-buckets');
    const total = summary.overdueBuckets.reduce((s, b) => s + b.count, 0) || 1;
    const bucketColors = { Current: '#10B981', DPD1_7: '#F59E0B', DPD8_30: '#F97316', DPD31_90: '#EF4444', DPD90Plus: '#991B1B' };
    bucketsEl.innerHTML = (summary.overdueBuckets || []).map(b => `
      <div class="bucket-item">
        <span style="font-size:0.8rem;font-weight:600;min-width:120px;color:var(--text-secondary)">${b.label}</span>
        <div class="bucket-bar-wrap">
          <div class="bucket-bar" style="width:${Math.round(b.count/total*100)}%;background:${bucketColors[b.bucket]||'#3B82F6'}"></div>
        </div>
        <span class="bucket-count">${b.count}</span>
        <span class="bucket-amount">${formatCurrency(b.totalAmount)}</span>
      </div>
    `).join('') || '<p style="padding:1rem;color:var(--text-muted)">Không có dữ liệu</p>';

    // Status breakdown
    const statusEl = document.getElementById('loan-status-breakdown');
    statusEl.innerHTML = (summary.loanStatusBreakdown || []).map(s => `
      <div class="bucket-item">
        <span style="flex:1;font-size:0.875rem">${s.status === 'Active' ? '✅ Hoạt động' : s.status === 'Closed' ? '🔒 Đã đóng' : s.status === 'Defaulted' ? '🔴 Nợ xấu' : s.status}</span>
        <span class="bucket-count">${s.count}</span>
        <span class="bucket-amount">${formatCurrency(s.totalAmount)}</span>
      </div>
    `).join('') || '<p style="padding:1rem;color:var(--text-muted)">Không có dữ liệu</p>';
  } catch (err) {
    toast(err.message, 'error');
  }
}

/* ====== CUSTOMERS ====== */
async function loadCustomers(page = 1) {
  customerPage = page;
  const search = document.getElementById('customerSearch').value.trim();
  const body = document.getElementById('customersBody');
  body.innerHTML = '<tr><td colspan="8" class="loading-row">Đang tải...</td></tr>';

  try {
    const res = await api.customers.list({ search, page, pageSize: 15 });
    customerList = res.items || [];
    renderCustomers(res);
  } catch (err) {
    body.innerHTML = `<tr><td colspan="8" class="loading-row" style="color:var(--red)">${err.message}</td></tr>`;
  }
}

function renderCustomers(res) {
  const body = document.getElementById('customersBody');
  if (!res.items?.length) {
    body.innerHTML = '<tr><td colspan="8" class="loading-row">Không có dữ liệu</td></tr>';
    return;
  }
  body.innerHTML = res.items.map(c => `
    <tr>
      <td><span style="color:var(--blue);font-weight:600">${c.customerCode}</span></td>
      <td><strong>${c.fullName}</strong></td>
      <td style="font-family:monospace;font-size:0.8rem">${c.nationalId}</td>
      <td>${c.phoneNumber}</td>
      <td>${c.branchName || '—'}</td>
      <td>${c.activeLoans}</td>
      <td>${formatCurrency(c.totalOutstanding)}</td>
      <td>
        <div style="display:flex;gap:0.4rem">
          <button class="btn btn-sm btn-outline" onclick="viewCustomer(${c.id})">Chi tiết</button>
          <button class="btn btn-sm btn-ghost" onclick="editCustomer(${c.id})">Sửa</button>
        </div>
      </td>
    </tr>
  `).join('');
  renderPagination(document.getElementById('customerPagination'), res, loadCustomers);
}

document.getElementById('btnSearchCustomer').addEventListener('click', () => loadCustomers(1));
document.getElementById('customerSearch').addEventListener('keydown', e => { if (e.key === 'Enter') loadCustomers(1); });

document.getElementById('btnNewCustomer').addEventListener('click', () => {
  document.getElementById('customerForm').reset();
  document.getElementById('modalCustomerTitle').textContent = 'Thêm khách hàng mới';
  document.getElementById('customerForm').dataset.customerId = '';
  document.getElementById('customerFormError').classList.add('hidden');
  openModal('modal-customer');
});

function editCustomer(id) {
  const c = customerList.find(x => x.id === id);
  if (!c) return;
  document.getElementById('modalCustomerTitle').textContent = 'Cập nhật khách hàng';
  document.getElementById('customerForm').dataset.customerId = id;
  document.getElementById('cFullName').value = c.fullName;
  document.getElementById('cNationalId').value = c.nationalId;
  document.getElementById('cNationalId').disabled = true;
  document.getElementById('cPhone').value = c.phoneNumber;
  document.getElementById('cEmail').value = c.email || '';
  document.getElementById('cDob').value = c.dateOfBirth ? c.dateOfBirth.substring(0, 10) : '';
  document.getElementById('cOccupation').value = c.occupation || '';
  document.getElementById('cIncome').value = c.monthlyIncome || '';
  document.getElementById('cAddress').value = c.address || '';
  document.getElementById('customerFormError').classList.add('hidden');
  openModal('modal-customer');
}

function viewCustomer(id) {
  // Navigate to loans page filtered by customer
  navigateTo('loans');
  loadLoans(1, id);
}

document.getElementById('customerForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const errEl = document.getElementById('customerFormError');
  errEl.classList.add('hidden');
  const customerId = document.getElementById('customerForm').dataset.customerId;
  const body = {
    fullName: document.getElementById('cFullName').value,
    nationalId: document.getElementById('cNationalId').value,
    phoneNumber: document.getElementById('cPhone').value,
    email: document.getElementById('cEmail').value || null,
    dateOfBirth: document.getElementById('cDob').value,
    occupation: document.getElementById('cOccupation').value || null,
    monthlyIncome: document.getElementById('cIncome').value || null,
    address: document.getElementById('cAddress').value || null,
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
    document.getElementById('cNationalId').disabled = false;
    await loadCustomers(customerPage);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  }
});

/* ====== LOAN PRODUCTS ====== */
async function loadProducts() {
  const grid = document.getElementById('productsGrid');
  grid.innerHTML = '<div class="loading-row">Đang tải...</div>';
  try {
    productList = await api.products.list();
    if (!productList.length) { grid.innerHTML = '<div class="loading-row">Không có sản phẩm</div>'; return; }
    grid.innerHTML = productList.map(p => `
      <div class="product-card">
        <div class="product-code">${p.code}</div>
        <div class="product-name">${p.name}</div>
        <div class="product-desc">${p.description || 'Không có mô tả'}</div>
        <div class="product-stats">
          <div class="product-stat">
            <div class="product-stat-label">Số tiền tối thiểu</div>
            <div class="product-stat-value">${formatCurrency(p.minAmount)}</div>
          </div>
          <div class="product-stat">
            <div class="product-stat-label">Số tiền tối đa</div>
            <div class="product-stat-value">${formatCurrency(p.maxAmount)}</div>
          </div>
          <div class="product-stat">
            <div class="product-stat-label">Lãi suất / tháng</div>
            <div class="product-stat-value" style="color:var(--amber)">${p.interestRatePerMonth}%</div>
          </div>
          <div class="product-stat">
            <div class="product-stat-label">Kỳ hạn (tháng)</div>
            <div class="product-stat-value">${p.minTenorMonths} – ${p.maxTenorMonths}</div>
          </div>
        </div>
        ${p.processingFeeRate ? `<div style="margin-top:0.75rem;font-size:0.75rem;color:var(--text-muted)">Phí xử lý: ${p.processingFeeRate}%</div>` : ''}
        <div style="margin-top:1rem">${p.isActive ? statusBadge('Active') : statusBadge('Closed')}</div>
      </div>
    `).join('');
  } catch (err) {
    grid.innerHTML = `<div class="loading-row" style="color:var(--red)">${err.message}</div>`;
  }
}

/* ====== APPLICATIONS ====== */
async function loadApplications(page = 1) {
  appPage = page;
  const search = document.getElementById('appSearch').value.trim();
  const status = document.getElementById('appStatusFilter').value;
  const body = document.getElementById('applicationsBody');
  body.innerHTML = '<tr><td colspan="8" class="loading-row">Đang tải...</td></tr>';
  try {
    const params = { page, pageSize: 15 };
    if (search) params.search = search;
    if (status) params.status = status;
    const res = await api.applications.list(params);
    renderApplications(res);
  } catch (err) {
    body.innerHTML = `<tr><td colspan="8" class="loading-row" style="color:var(--red)">${err.message}</td></tr>`;
  }
}

function renderApplications(res) {
  const body = document.getElementById('applicationsBody');
  const user = getUser();
  if (!res.items?.length) { body.innerHTML = '<tr><td colspan="8" class="loading-row">Không có dữ liệu</td></tr>'; return; }
  body.innerHTML = res.items.map(a => `
    <tr>
      <td><span style="color:var(--blue);font-weight:600;font-size:0.8rem">${a.applicationNumber}</span></td>
      <td><strong>${a.customerName}</strong><br/><span style="color:var(--text-muted);font-size:0.75rem">${a.customerPhone}</span></td>
      <td style="font-size:0.8rem">${a.productName}</td>
      <td><strong>${formatCurrency(a.requestedAmount)}</strong></td>
      <td>${a.tenorMonths} tháng</td>
      <td>${statusBadge(a.status)}</td>
      <td style="font-size:0.8rem">${formatDate(a.createdAt)}</td>
      <td>
        <div style="display:flex;gap:0.4rem;flex-wrap:wrap">
          ${a.status === 'Draft' ? `<button class="btn btn-sm btn-outline" onclick="submitApplication(${a.id})">Nộp</button>` : ''}
          ${(a.status === 'Submitted' || a.status === 'UnderReview') && (user?.role === 'Approver' || user?.role === 'Admin') ? `<button class="btn btn-sm btn-primary" onclick="openDecision(${a.id})">Duyệt</button>` : ''}
          ${a.status === 'Approved' && (user?.role === 'LoanOfficer' || user?.role === 'Admin') ? `<button class="btn btn-sm btn-success" onclick="disburse(${a.id})">Giải ngân</button>` : ''}
        </div>
      </td>
    </tr>
  `).join('');
  renderPagination(document.getElementById('appPagination'), res, loadApplications);
}

document.getElementById('btnSearchApp').addEventListener('click', () => loadApplications(1));
document.getElementById('btnNewApplication').addEventListener('click', async () => {
  await Promise.all([loadCustomersForDropdown(), loadProductsForDropdown()]);
  document.getElementById('applicationForm').reset();
  document.getElementById('installmentPreview').classList.add('hidden');
  document.getElementById('appFormError').classList.add('hidden');
  openModal('modal-application');
});

async function loadCustomersForDropdown() {
  const res = await api.customers.list({ pageSize: 500 });
  const sel = document.getElementById('appCustomerId');
  sel.innerHTML = '<option value="">-- Chọn khách hàng --</option>';
  (res.items || []).forEach(c => {
    const opt = document.createElement('option');
    opt.value = c.id;
    opt.textContent = `${c.customerCode} — ${c.fullName} (${c.phoneNumber})`;
    sel.appendChild(opt);
  });
}

async function loadProductsForDropdown() {
  if (!productList.length) productList = await api.products.list();
  const sel = document.getElementById('appProductId');
  sel.innerHTML = '<option value="">-- Chọn sản phẩm --</option>';
  productList.forEach(p => {
    const opt = document.createElement('option');
    opt.value = p.id;
    opt.textContent = `${p.code} — ${p.name} (${p.interestRatePerMonth}%/tháng)`;
    sel.appendChild(opt);
  });
}

document.getElementById('btnPreviewInstallment').addEventListener('click', async () => {
  const productId = document.getElementById('appProductId').value;
  const amount = document.getElementById('appAmount').value;
  const tenor = document.getElementById('appTenor').value;
  if (!productId || !amount || !tenor) { toast('Vui lòng chọn sản phẩm, nhập số tiền và kỳ hạn', 'warning'); return; }
  try {
    const prev = await api.products.preview({ loanProductId: +productId, amount: +amount, tenorMonths: +tenor });
    document.getElementById('previewMonthly').textContent = formatCurrency(prev.monthlyInstallment);
    document.getElementById('previewTotal').textContent = formatCurrency(prev.totalRepayment);
    document.getElementById('previewInterest').textContent = formatCurrency(prev.totalInterest);
    document.getElementById('previewFee').textContent = formatCurrency(prev.processingFee);
    document.getElementById('installmentPreview').classList.remove('hidden');
  } catch (err) {
    toast(err.message, 'error');
  }
});

document.getElementById('applicationForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const errEl = document.getElementById('appFormError');
  errEl.classList.add('hidden');
  try {
    await api.applications.create({
      customerId: +document.getElementById('appCustomerId').value,
      loanProductId: +document.getElementById('appProductId').value,
      requestedAmount: +document.getElementById('appAmount').value,
      tenorMonths: +document.getElementById('appTenor').value,
      purpose: document.getElementById('appPurpose').value,
    });
    toast('Tạo hồ sơ vay thành công!');
    closeModal('modal-application');
    await loadApplications(1);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  }
});

async function submitApplication(id) {
  if (!confirm('Nộp hồ sơ để gửi phê duyệt?')) return;
  try {
    await api.applications.submit(id);
    toast('Hồ sơ đã được nộp!');
    await loadApplications(appPage);
  } catch (err) { toast(err.message, 'error'); }
}

/* ── Decision ── */
function openDecision(id) {
  document.getElementById('decisionAppId').value = id;
  document.getElementById('decisionValue').value = 'Approved';
  document.getElementById('decisionRemark').value = '';
  document.getElementById('decisionAmount').value = '';
  document.getElementById('decisionTenor').value = '';
  document.getElementById('decisionError').classList.add('hidden');
  document.querySelectorAll('.decision-btn').forEach(b => b.classList.remove('active'));
  document.querySelector('.decision-btn[data-value="Approved"]').classList.add('active');
  openModal('modal-decision');
}

document.querySelectorAll('.decision-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.decision-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('decisionValue').value = btn.dataset.value;
    document.getElementById('approvedAmountRow').style.display = btn.dataset.value === 'Approved' ? '' : 'none';
  });
});

document.getElementById('btnSubmitDecision').addEventListener('click', async () => {
  const errEl = document.getElementById('decisionError');
  errEl.classList.add('hidden');
  try {
    const id = document.getElementById('decisionAppId').value;
    await api.applications.decide(id, {
      decision: document.getElementById('decisionValue').value,
      remark: document.getElementById('decisionRemark').value || null,
      approvedAmount: document.getElementById('decisionAmount').value ? +document.getElementById('decisionAmount').value : null,
      approvedTenorMonths: document.getElementById('decisionTenor').value ? +document.getElementById('decisionTenor').value : null,
    });
    toast('Quyết định đã được lưu!');
    closeModal('modal-decision');
    await loadApplications(appPage);
    await loadDashboard();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  }
});

/* ── Disburse ── */
async function disburse(applicationId) {
  if (!confirm('Xác nhận giải ngân cho hồ sơ này?')) return;
  try {
    await api.loans.disburse({
      loanApplicationId: applicationId,
      disbursedAt: new Date().toISOString(),
      paymentMethod: 'BankTransfer',
    });
    toast('Giải ngân thành công! Khoản vay đã được tạo.');
    navigateTo('loans');
    await loadLoans();
  } catch (err) { toast(err.message, 'error'); }
}

/* ====== LOANS ====== */
async function loadLoans(page = 1, customerId = null) {
  loanPage = page;
  const search = document.getElementById('loanSearch').value.trim();
  const status = document.getElementById('loanStatusFilter').value;
  const body = document.getElementById('loansBody');
  body.innerHTML = '<tr><td colspan="9" class="loading-row">Đang tải...</td></tr>';
  try {
    const params = { page, pageSize: 15 };
    if (search) params.search = search;
    if (status) params.status = status;
    if (customerId) params.customerId = customerId;
    const res = await api.loans.list(params);
    renderLoans(res);
  } catch (err) {
    body.innerHTML = `<tr><td colspan="9" class="loading-row" style="color:var(--red)">${err.message}</td></tr>`;
  }
}

function renderLoans(res) {
  const body = document.getElementById('loansBody');
  if (!res.items?.length) { body.innerHTML = '<tr><td colspan="9" class="loading-row">Không có dữ liệu</td></tr>'; return; }
  body.innerHTML = res.items.map(l => `
    <tr>
      <td><span style="color:var(--blue);font-weight:600;font-size:0.8rem">${l.loanNumber}</span></td>
      <td><strong>${l.customerName}</strong></td>
      <td style="font-size:0.8rem">${l.productName}</td>
      <td>${formatCurrency(l.principalAmount)}</td>
      <td><strong style="color:${l.status === 'Active' ? 'var(--amber)' : 'var(--text-secondary)'}">${formatCurrency(l.outstandingBalance)}</strong></td>
      <td>${l.interestRatePerMonth}%/tháng</td>
      <td>${formatDate(l.maturityDate)}</td>
      <td>${statusBadge(l.status)}</td>
      <td>
        <div style="display:flex;gap:0.4rem">
          <button class="btn btn-sm btn-outline" onclick="viewLoan(${l.id})">Chi tiết</button>
          ${l.status === 'Active' ? `<button class="btn btn-sm btn-success" onclick="openPayment(${l.id})">Thu tiền</button>` : ''}
        </div>
      </td>
    </tr>
  `).join('');
  renderPagination(document.getElementById('loanPagination'), res, loadLoans);
}

document.getElementById('btnSearchLoan').addEventListener('click', () => loadLoans(1));

async function viewLoan(id) {
  try {
    const loan = await api.loans.get(id);
    alert(`Khoản vay: ${loan.loanNumber}\nKhách hàng: ${loan.customerName}\nDư nợ: ${formatCurrency(loan.outstandingBalance)}\nSố kỳ: ${loan.tenorMonths}`);
  } catch (err) { toast(err.message, 'error'); }
}

/* ── Payment ── */
function openPayment(loanId) {
  document.getElementById('paymentLoanId').value = loanId;
  document.getElementById('payAmount').value = '';
  document.getElementById('payDate').value = new Date().toISOString().substring(0, 10);
  document.getElementById('payRef').value = '';
  document.getElementById('paymentError').classList.add('hidden');
  openModal('modal-payment');
}

document.getElementById('btnSubmitPayment').addEventListener('click', async () => {
  const errEl = document.getElementById('paymentError');
  errEl.classList.add('hidden');
  try {
    const loanId = document.getElementById('paymentLoanId').value;
    await api.loans.pay(loanId, {
      amount: +document.getElementById('payAmount').value,
      paymentDate: document.getElementById('payDate').value,
      paymentMethod: document.getElementById('payMethod').value,
      referenceNumber: document.getElementById('payRef').value || null,
    });
    toast('Ghi nhận thanh toán thành công!');
    closeModal('modal-payment');
    await loadLoans(loanPage);
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  }
});

/* ====== OVERDUE ====== */
async function loadOverdue() {
  const body = document.getElementById('overdueBody');
  body.innerHTML = '<tr><td colspan="8" class="loading-row">Đang tải...</td></tr>';
  try {
    const list = await api.dashboard.overdue();
    if (!list.length) { body.innerHTML = '<tr><td colspan="8" class="loading-row">Không có khoản vay quá hạn 🎉</td></tr>'; return; }

    // Summary
    const bucketCounts = {};
    list.forEach(item => { bucketCounts[item.bucket] = (bucketCounts[item.bucket] || 0) + 1; });
    document.getElementById('overdueSummary').innerHTML = Object.entries(bucketCounts).map(([k, v]) =>
      `<div class="overdue-bucket-tag">${dpdBadge(k)} <strong>${v}</strong></div>`
    ).join('');

    body.innerHTML = list.map(item => `
      <tr>
        <td><span style="color:var(--blue);font-weight:600;font-size:0.8rem">${item.loanNumber}</span></td>
        <td><strong>${item.customerName}</strong></td>
        <td>${item.customerPhone}</td>
        <td>${formatCurrency(item.outstandingBalance)}</td>
        <td style="color:var(--red);font-weight:600">${formatCurrency(item.overdueAmount)}</td>
        <td style="color:var(--red);font-weight:700">${item.daysOverdue} ngày</td>
        <td>${dpdBadge(item.bucket)}</td>
        <td style="font-size:0.8rem;color:var(--text-secondary)">${item.lastCollectionNote || '—'}</td>
      </tr>
    `).join('');
  } catch (err) {
    body.innerHTML = `<tr><td colspan="8" class="loading-row" style="color:var(--red)">${err.message}</td></tr>`;
  }
}

document.getElementById('btnUpdateOverdue').addEventListener('click', async () => {
  try {
    await api.dashboard.updateOverdue();
    toast('Đã cập nhật trạng thái quá hạn!');
    await loadOverdue();
  } catch (err) { toast(err.message, 'error'); }
});
