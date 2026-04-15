# Frontend Refactor Task Breakdown

Date: 2026-04-16
Project: LoanFlow Lite Frontend
Purpose: Phân rã task chi tiết để refactor frontend từ MVP structure sang maintainable production-oriented structure.

## 1. Objective

Mục tiêu của kế hoạch này:

- Fix các điểm gãy kiến trúc hiện tại.
- Chuẩn hóa structure frontend theo feature-based modules.
- Giảm coupling giữa page, API, permissions, và UI rendering.
- Tạo nền tảng để scale thêm module mà không làm codebase rối hơn.

## 2. Refactor Scope

Phạm vi ưu tiên:

- `frontend/src/api`
- `frontend/src/context`
- `frontend/src/pages`
- `frontend/src/components`
- `frontend/src/App.jsx`
- `frontend/src/main.jsx`
- cấu trúc thư mục mới trong `frontend/src/app`, `frontend/src/shared`, `frontend/src/features`

Không nằm trong scope bắt buộc của đợt này:

- redesign UI toàn bộ
- thay đổi business flow backend
- thay đổi API contract backend trừ khi cần fix mismatch nghiêm trọng

## 3. Delivery Strategy

Refactor nên chia thành 4 phase:

- Phase 1: Stabilize contract and API structure
- Phase 2: Break down large pages
- Phase 3: Standardize app shell and permissions
- Phase 4: Add quality gates and test baseline

## 4. Phase 1 - Stabilize Contract and API Structure

### 4.1 Create shared HTTP client

Task:

- Tạo thư mục `frontend/src/shared/api`
- Tạo file:
  - `httpClient.js`
  - `errorMapper.js`
  - `interceptors.js` nếu cần

Expected result:

- Mọi request HTTP đi qua một HTTP client chung.
- Xử lý lỗi, auth header, unauthorized behavior được quản lý tập trung.

Done criteria:

- Không còn logic fetch generic nằm trong domain API file.

### 4.2 Split API by feature domain

Task:

- Tạo các API file riêng:
  - `features/auth/api/authApi.js`
  - `features/customers/api/customerApi.js`
  - `features/applications/api/applicationApi.js`
  - `features/loans/api/loanApi.js`
  - `features/dashboard/api/dashboardApi.js`
  - `features/overdue/api/overdueApi.js`

Expected result:

- Mỗi module chỉ import API của chính nó.
- API ownership rõ theo feature.

Done criteria:

- Không còn file `frontend/src/api/index.js` làm điểm tập trung cho mọi domain API.

### 4.3 Create auth storage module

Task:

- Tạo `features/auth/model/authStorage.js`
- Chuyển toàn bộ localStorage logic từ API layer sang auth module.

Expected result:

- Token và user persistence không còn nằm trong transport layer.

Done criteria:

- HTTP client chỉ đọc access token qua auth storage hoặc auth session helper.

### 4.4 Fix API response contract mismatch

Task:

- Review toàn bộ page đang đọc raw DTO.
- Fix các mismatch giữa backend DTO flat fields và frontend nested access.
- Ưu tiên:
  - applications
  - loans

Expected result:

- UI render đúng field backend trả về.

Done criteria:

- Không còn chỗ nào đọc:
  - `application.customer.fullName`
  - `application.product.productName`
  - `loan.customer.fullName`
  - `loan.product.productName`
  nếu backend không trả nested object tương ứng.

### 4.5 Create mapper layer for unstable DTOs

Task:

- Tạo:
  - `features/applications/mappers/applicationMapper.js`
  - `features/loans/mappers/loanMapper.js`

Expected result:

- UI không render trực tiếp raw API payload.
- Có lớp chuẩn hóa dữ liệu cho component.

Done criteria:

- Table/list/detail components nhận view model đã map.

## 5. Phase 2 - Break Down Large Pages

### 5.1 Refactor Applications module

Task:

- Tách từ `Applications.jsx` thành:
  - `pages/ApplicationListPage.jsx`
  - `components/ApplicationTable.jsx`
  - `components/ApplicationFormModal.jsx`
  - `components/RejectApplicationModal.jsx`
  - `hooks/useApplications.js`
  - `hooks/useApplicationActions.js`

Expected result:

- Page chỉ compose UI và workflow.
- Modal và business actions có thể maintain riêng.

Done criteria:

- `Applications.jsx` cũ không còn là file lớn chứa nhiều component inline.

### 5.2 Refactor Loans module

Task:

- Tách từ `Loans.jsx` thành:
  - `pages/LoanListPage.jsx`
  - `components/LoanTable.jsx`
  - `components/LoanDetailModal.jsx`
  - `components/DisburseModal.jsx`
  - `components/PaymentModal.jsx`
  - `hooks/useLoans.js`
  - `hooks/useLoanDetail.js`
  - `hooks/useLoanActions.js`

Expected result:

- Loan workflows được phân tách rõ theo read/write responsibility.

Done criteria:

- Không còn modal detail/disburse/payment nằm inline trong page file.

### 5.3 Refactor Customers module

Task:

- Tách `Customers.jsx` thành:
  - `pages/CustomerListPage.jsx`
  - `components/CustomerTable.jsx`
  - `components/CustomerFormModal.jsx`
  - `hooks/useCustomers.js`
  - `hooks/useCustomerCreditCheck.js`

Expected result:

- Customer module theo cùng pattern với applications và loans.

Done criteria:

- Customer page chỉ còn phần compose/filter/navigation state.

### 5.4 Refactor Dashboard module

Task:

- Tách `Dashboard.jsx` thành:
  - `pages/DashboardPage.jsx`
  - `components/KpiCard.jsx`
  - `components/OverdueBuckets.jsx`
  - `hooks/useDashboardSummary.js`

Expected result:

- Dashboard không còn tự vừa fetch vừa vừa manage layout-side state theo cách ad-hoc.

Done criteria:

- `pendingApprovals` không còn phải được đẩy lên qua prop callback nếu có solution tốt hơn.

## 6. Phase 3 - Standardize App Shell and Permissions

### 6.1 Introduce app layer

Task:

- Tạo:
  - `app/App.jsx`
  - `app/router.jsx`
  - `app/layout/AppLayout.jsx`
  - `app/layout/ProtectedRoute.jsx`

Expected result:

- Routing và app shell có boundary rõ ràng.

Done criteria:

- `main.jsx` chỉ mount providers và router.

### 6.2 Normalize route definitions

Task:

- Tạo `shared/constants/routes.js`
- Khai báo route path tập trung.

Expected result:

- Route path không bị hard-code rải rác.

Done criteria:

- Navigation và router đều dùng chung route constants.

### 6.3 Centralize permission rules

Task:

- Tạo:
  - `shared/utils/permissions.js`
  hoặc
  - `features/auth/model/authPermissions.js`

- Chuyển toàn bộ capability matrix vào một nơi.

Expected result:

- Menu visibility
- action button visibility
- route protection

đều dùng cùng một source of truth.

Done criteria:

- `AuthContext` không còn hardcode toàn bộ permission object.
- `Layout` không còn tự viết thêm rule riêng lệch với context.

### 6.4 Clean auth/session responsibilities

Task:

- Tách rõ:
  - auth provider
  - auth storage
  - auth API
  - session invalidation behavior

Expected result:

- Unauthorized flow không còn bị nhúng trực tiếp kiểu side effect trong fetch wrapper một cách cứng.

Done criteria:

- Redirect/logout on unauthorized được điều phối từ auth/session boundary hợp lý.

## 7. Phase 4 - Quality Gates and Test Baseline

### 7.1 Add frontend test setup

Task:

- Cài test stack phù hợp, ví dụ:
  - Vitest
  - Testing Library

Expected result:

- Có thể viết test component/hook/page cơ bản.

Done criteria:

- Có script test trong `package.json`.

### 7.2 Add critical path tests

Task:

- Viết test tối thiểu cho:
  - login flow
  - protected route redirect
  - application list render
  - application approve/reject action
  - loan disbursement form
  - payment form validation

Expected result:

- Có baseline regression protection cho flow quan trọng.

Done criteria:

- CI hoặc local test run pass ổn định.

### 7.3 Introduce CSS boundary strategy

Task:

- Đánh giá chọn 1 trong 3 hướng:
  - CSS modules
  - feature CSS files
  - scoped component stylesheet convention

Expected result:

- Không để toàn bộ UI phụ thuộc lâu dài vào một global stylesheet duy nhất.

Done criteria:

- Các feature mới không bắt buộc sửa `index.css` để thêm style riêng.

## 8. Suggested Backlog by Priority

### Priority P1

- Tạo shared HTTP client
- Tách API theo domain
- Tách auth storage khỏi API layer
- Fix DTO mismatch applications
- Fix DTO mismatch loans
- Tạo mapper cho applications và loans

### Priority P2

- Tách `Applications.jsx`
- Tách `Loans.jsx`
- Gom permission rule về một nơi
- Chuẩn hóa app shell và protected routes

### Priority P3

- Tách `Customers.jsx`
- Tách `Dashboard.jsx`
- Chuẩn hóa route constants
- Làm sạch auth/session responsibilities

### Priority P4

- Test setup
- Critical path tests
- CSS boundary strategy

## 9. Suggested Sprint Plan

### Sprint 1

- Shared HTTP client
- Domain API split
- Auth storage cleanup
- DTO mismatch fix
- Mapper layer for applications and loans

### Sprint 2

- Refactor Applications module
- Refactor Loans module
- Centralize permissions

### Sprint 3

- App shell normalization
- Refactor Customers
- Refactor Dashboard

### Sprint 4

- Test setup
- Critical path tests
- CSS structure cleanup

## 10. Risks During Refactor

Các rủi ro chính:

- Gãy contract tạm thời giữa frontend cũ và mapper mới
- Vỡ routing nếu đổi app shell không kiểm soát
- Permission hiển thị sai nếu chưa centralize hoàn chỉnh
- Modal state behavior thay đổi sau khi tách component
- CSS side effect khi bóc tách khỏi global style

Biện pháp giảm rủi ro:

- Refactor từng module, không rewrite all-at-once
- Giữ UI output ổn định trong mỗi phase
- Test thủ công từng flow sau mỗi phase
- Ưu tiên fix applications và loans trước vì đây là flow nghiệp vụ trọng yếu

## 11. Definition of Success

Refactor được xem là thành công khi:

- Page files không còn ôm quá nhiều trách nhiệm
- API được tách theo feature domain
- Permission rule có một source of truth
- UI không còn phụ thuộc trực tiếp vào raw DTO không ổn định
- App shell rõ ràng
- Có test baseline cho critical flows

## 12. Final Recommendation

Khuyến nghị thực thi:

- Không nên tiếp tục phát triển thêm nhiều module mới trên structure frontend hiện tại.
- Nên bắt đầu refactor từ Phase 1 ngay.
- Không cần rewrite toàn bộ, nhưng bắt buộc phải có boundary mới trước khi scale thêm feature production.

Task quan trọng nhất để bắt đầu:

- dựng shared HTTP client
- split domain APIs
- fix DTO mismatch
- bóc `Applications.jsx` và `Loans.jsx`

Đây là nhóm task cho tỷ lệ hiệu quả/rủi ro tốt nhất trong ngắn hạn.
