# Frontend Architecture Review

Date: 2026-04-16
Role: Solution Architect Review
Project: LoanFlow Lite Frontend

## 1. Review Objective

Mục tiêu của tài liệu này là:

- Review codebase frontend hiện tại theo góc nhìn kiến trúc.
- Đánh giá mức độ phù hợp để maintain và scale.
- Chỉ ra các vấn đề cấu trúc nếu codebase không còn ở trạng thái "normal" cho production growth.
- Đề xuất structure base phù hợp để refactor.

## 2. Executive Summary

Kết luận ngắn:

- Frontend hiện tại phù hợp cho MVP hoặc demo nội bộ.
- Frontend chưa phù hợp để mở rộng lâu dài cho production-scale application.
- Cấu trúc hiện tại mang tính page-centric monolith:
  - page chứa quá nhiều trách nhiệm
  - API layer chưa tách domain rõ ràng
  - permission logic bị phân tán
  - response mapping giữa backend và frontend chưa ổn định
  - chưa có test structure

Nhận định kiến trúc:

- Đây không phải là codebase "bất thường" theo nghĩa hỏng hoàn toàn.
- Nhưng đây không còn là structure "normal" cho giai đoạn scale tiếp theo.
- Nếu tiếp tục thêm module mới trên nền hiện tại, chi phí maintain sẽ tăng nhanh và nguy cơ bug contract sẽ cao.

## 3. Current Frontend Structure

Hiện trạng thư mục frontend:

```text
frontend/src/
  api/
  components/
  context/
  pages/
  utils/
  App.jsx
  main.jsx
  index.css
```

Đặc điểm hiện tại:

- `pages/` đang chứa cả page logic, modal, data loading, command handling.
- `api/index.js` chứa toàn bộ API của tất cả domain.
- `context/` đang gánh auth state và permission logic.
- `components/` chỉ là shared UI cơ bản.
- `index.css` là global stylesheet lớn cho toàn app.

## 4. Key Findings

### 4.1 API Contract Mismatch Risk

Mô tả:

- Frontend đang dùng nhiều field kiểu nested object như:
  - `a.customer.fullName`
  - `a.product.productName`
  - `l.customer.fullName`
- Trong khi backend DTO hiện tại trả dữ liệu dạng flat:
  - `customerName`
  - `customerPhone`
  - `productName`

Impact:

- Có nguy cơ page render sai dữ liệu hoặc hiện dấu `-` dù backend đã trả dữ liệu đúng.
- Đây là dấu hiệu frontend chưa có mapping layer rõ ràng giữa API response và UI model.

Architectural implication:

- Khi frontend đọc trực tiếp raw DTO từ backend mà không qua mapper, contract change sẽ gây lỗi lan rộng.

### 4.2 Page Files Contain Too Many Responsibilities

Mô tả:

- Một file page hiện đang đồng thời chứa:
  - page component
  - modal component
  - form handling
  - loading state
  - API orchestration
  - command confirmation
  - permission-based action rendering

Ví dụ rõ nhất:

- `Applications.jsx`
- `Loans.jsx`

Impact:

- File quá dài và khó đọc.
- Khó test từng phần độc lập.
- Khó tái sử dụng modal hoặc business action ở màn hình khác.
- Khi thêm tính năng mới, page sẽ tiếp tục phình to.

### 4.3 API Layer Is Too Centralized

Mô tả:

- `frontend/src/api/index.js` đang là một file tập trung toàn bộ fetch wrapper và domain API.
- Auth side effect như redirect khi `401` cũng đang đặt trực tiếp trong HTTP request wrapper.

Impact:

- API layer bị coupling với browser behavior.
- Khó mở rộng retry, refresh token, correlation id, telemetry, request tracing.
- Khó ownership theo domain.

Architectural implication:

- Với hệ thống nhiều module nghiệp vụ, API nên tách theo feature domain.

### 4.4 Permission Logic Is Duplicated

Mô tả:

- `AuthContext` có object `can`.
- `Layout` lại có thêm logic riêng để ẩn/hiện menu theo role.

Impact:

- Permission rule có thể bị lệch giữa navigation, button action, và route access.
- Khi role matrix thay đổi, phải sửa nhiều nơi.

Architectural implication:

- Permission phải có source of truth duy nhất.

### 4.5 Shared State Strategy Is Weak

Mô tả:

- `Dashboard` đẩy `pendingApprovals` lên `App`, rồi `App` truyền tiếp xuống `Layout`.

Impact:

- Đây là prop drilling ở mức app shell.
- Khi số lượng shared state tăng, app shell sẽ nhanh chóng rối.

Architectural implication:

- App cần state boundary rõ hơn ở level shell/provider/query cache.

### 4.6 Styling Is Fully Global

Mô tả:

- App đang dùng một file `index.css` global cho hầu hết UI.

Impact:

- Dễ xảy ra side effect CSS khi thêm màn hình mới.
- Rất khó quản lý khi nhiều người cùng sửa giao diện.
- Không có boundary theo feature.

### 4.7 No Frontend Test Structure

Mô tả:

- Chưa thấy test/spec structure trong frontend.

Impact:

- Không có regression safety cho auth, routing guard, command flow, và form behavior.

Architectural implication:

- Với domain loan/payment, frontend cũng cần test tối thiểu cho critical paths.

## 5. Architecture Assessment

### 5.1 What Is Good

- Cấu trúc hiện tại đơn giản, dễ khởi động nhanh.
- Dễ onboard cho người mới ở giai đoạn MVP.
- Số lượng abstraction còn ít nên chưa quá khó debug.

### 5.2 What Is Not Good

- Separation of concerns chưa rõ.
- Domain boundary chưa rõ.
- View model và API contract chưa có lớp chống lệch.
- Reusability thấp.
- Maintainability giảm nhanh khi số lượng màn hình và workflow tăng.

### 5.3 SA Conclusion

Kết luận theo góc nhìn Solution Architect:

- Codebase frontend hiện tại không nên tiếp tục mở rộng theo cùng một pattern.
- Cần refactor sang structure base theo feature/module.
- Không nhất thiết rewrite toàn bộ ngay, nhưng nên refactor có lộ trình.

## 6. Recommended Structure Base

Đề xuất structure base:

```text
frontend/src/
  app/
    App.jsx
    main.jsx
    router.jsx
    providers/
      AuthProvider.jsx
      ToastProvider.jsx
      QueryProvider.jsx
    layout/
      AppLayout.jsx
      Sidebar.jsx
      ProtectedRoute.jsx

  shared/
    api/
      httpClient.js
      interceptors.js
      errorMapper.js
    components/
      ui/
        Button.jsx
        Modal.jsx
        Pagination.jsx
        Badge.jsx
      feedback/
        EmptyState.jsx
        LoadingBlock.jsx
    hooks/
      useDisclosure.js
      useDebounce.js
    utils/
      format.js
      date.js
      permissions.js
    constants/
      roles.js
      routes.js

  features/
    auth/
      api/
        authApi.js
      hooks/
        useAuthSession.js
      model/
        authStorage.js
        authPermissions.js
      pages/
        LoginPage.jsx

    dashboard/
      api/
        dashboardApi.js
      hooks/
        useDashboardSummary.js
      components/
        KpiCard.jsx
        OverdueBuckets.jsx
      pages/
        DashboardPage.jsx

    customers/
      api/
        customerApi.js
      hooks/
        useCustomers.js
        useCustomerCreditCheck.js
      components/
        CustomerTable.jsx
        CustomerFormModal.jsx
      pages/
        CustomerListPage.jsx

    applications/
      api/
        applicationApi.js
      hooks/
        useApplications.js
        useApplicationActions.js
      components/
        ApplicationTable.jsx
        ApplicationFormModal.jsx
        RejectApplicationModal.jsx
      pages/
        ApplicationListPage.jsx
      mappers/
        applicationMapper.js

    loans/
      api/
        loanApi.js
      hooks/
        useLoans.js
        useLoanDetail.js
        useLoanActions.js
      components/
        LoanTable.jsx
        LoanDetailModal.jsx
        DisburseModal.jsx
        PaymentModal.jsx
      pages/
        LoanListPage.jsx
      mappers/
        loanMapper.js

    overdue/
      api/
        overdueApi.js
      hooks/
        useOverdueList.js
      components/
        OverdueTable.jsx
      pages/
        OverduePage.jsx
```

## 7. Design Principles for the New Structure

### 7.1 Pages Only Compose

Rule:

- Page chỉ nên làm nhiệm vụ compose screen.
- Không nên chứa modal implementation lớn và business orchestration dài.

### 7.2 API by Feature Domain

Rule:

- Mỗi feature có API riêng.
- Shared HTTP client chỉ làm transport concern chung.

### 7.3 UI Must Not Depend Directly on Raw DTO

Rule:

- Response từ backend nên được map qua view model hoặc mapper.
- UI chỉ render model ổn định theo nhu cầu màn hình.

### 7.4 Permission Must Have Single Source of Truth

Rule:

- Tất cả quyền hiển thị menu, button, route, command phải dùng chung một permission definition.

### 7.5 Reusable UI and Feature-Specific UI Must Be Separated

Rule:

- Component generic để ở `shared/components`.
- Component chỉ dùng cho một module để trong `features/<module>/components`.

### 7.6 Shared State Must Be Intentional

Rule:

- Auth/session để ở provider riêng.
- Remote data nên dùng query abstraction hoặc custom hooks.
- Không truyền state vòng qua nhiều cấp chỉ để update layout.

## 8. Recommended Refactor Roadmap

### Phase 1 - Stabilize Current Frontend

Mục tiêu:

- Fix các lỗi contract và dọn điểm gãy kiến trúc lớn nhất.

Tasks:

- Tách `httpClient` khỏi `api/index.js`
- Tách API theo domain:
  - auth
  - customers
  - applications
  - loans
  - dashboard
  - overdue
- Tạo mapper cho:
  - applications
  - loans
- Sửa mismatch giữa frontend field access và backend DTO
- Tách permission rules thành module riêng

Expected outcome:

- Frontend chạy ổn định hơn.
- Giảm bug do API contract mismatch.

### Phase 2 - Decompose Large Pages

Mục tiêu:

- Giảm độ phình của page files.

Tasks:

- Tách modal khỏi `Applications.jsx`
- Tách modal khỏi `Loans.jsx`
- Tách table component ra khỏi page
- Tách load/mutation logic thành custom hooks

Expected outcome:

- Mỗi file có trách nhiệm rõ ràng hơn.
- Dễ test và dễ maintain hơn.

### Phase 3 - App Shell and Shared Patterns

Mục tiêu:

- Chuẩn hóa shell-level architecture.

Tasks:

- Tạo `app/router.jsx`
- Tạo `ProtectedRoute`
- Tổ chức lại `providers`
- Chuẩn hóa routes/constants
- Đưa shared app state về đúng boundary

Expected outcome:

- App shell rõ ràng.
- Dễ thêm route/module mới.

### Phase 4 - Quality and Scalability

Mục tiêu:

- Nâng độ tin cậy và khả năng scale.

Tasks:

- Thêm frontend test setup
- Viết test cho:
  - auth login flow
  - protected routes
  - applications actions
  - loan disbursement/payment form
- Tách CSS theo module hoặc component boundary

Expected outcome:

- Giảm regression risk.
- Cải thiện collaboration khi team lớn hơn.

## 9. Recommended Coding Rules

Đề xuất coding rules cho frontend:

- Không gọi API trực tiếp bên trong component lớn nếu logic có thể tái sử dụng.
- Không render raw API DTO trực tiếp ở table/detail screen nếu DTO chưa qua mapper.
- Không để một page file vượt quá phạm vi hợp lý khi đã chứa nhiều modal/workflow.
- Không định nghĩa permission rule ở nhiều nơi.
- Không nhét tất cả domain API vào một file chung.
- Không dùng app shell prop drilling cho shared operational state.

## 10. Final Recommendation

Kết luận cuối:

- Frontend hiện tại đủ dùng cho MVP.
- Frontend hiện tại chưa phải structure base tốt cho production growth.
- Nên chuyển sang architecture theo feature-based modules.
- Không cần rewrite toàn bộ ngay.
- Nên refactor theo 4 phase để giảm rủi ro và giữ tiến độ.

Ưu tiên cao nhất:

- Fix API contract mismatch
- Tách API layer theo domain
- Tách page lớn thành page + hooks + components + modals
- Gom permission rule về một nơi

Nếu thực hiện đúng lộ trình trên, frontend sẽ chuyển từ trạng thái MVP-oriented sang maintainable production-oriented architecture.
