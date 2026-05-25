# LoanFlow Module Documentation / Tài liệu module LoanFlow

> Scope: this document maps the current codebase modules to their business purpose, main UI entry points, API controllers, application services, and domain data.  
> Phạm vi: tài liệu này mô tả từng module hiện có trong codebase theo mục đích nghiệp vụ, màn hình UI, API controller, service ứng dụng và dữ liệu domain chính.

## Codebase layout / Cấu trúc codebase

| Layer | Path | English | Tiếng Việt |
|---|---|---|---|
| API | `src/LoanFlow.API` | ASP.NET Core HTTP API, authentication, middleware, SignalR hubs, background jobs, Swagger, health checks. | API ASP.NET Core, xác thực, middleware, SignalR, job nền, Swagger và health check. |
| Application | `src/LoanFlow.Application` | Business services, DTOs, interfaces, validation and orchestration between API and domain. | Service nghiệp vụ, DTO, interface, kiểm tra dữ liệu và điều phối giữa API với domain. |
| Domain | `src/LoanFlow.Domain` | Core entities and enums such as customers, loans, payments, approvals, accounting, audit logs. | Entity và enum lõi như khách hàng, khoản vay, thanh toán, phê duyệt, kế toán, audit log. |
| Infrastructure | `src/LoanFlow.Infrastructure` | EF Core `LoanFlowDbContext`, migrations, repository/persistence concerns, database seeding. | EF Core `LoanFlowDbContext`, migration, lưu trữ dữ liệu và seed dữ liệu mẫu. |
| Frontend | `frontend/src` | React SPA routes, feature APIs, pages, contexts, i18n and shared UI components. | SPA React gồm route, API theo feature, màn hình, context, i18n và component dùng chung. |
| Operations | root, `grafana`, `alertmanager`, `scripts` | Docker Compose, Nginx, Prometheus/Grafana, Alertmanager and deployment scripts. | Docker Compose, Nginx, Prometheus/Grafana, Alertmanager và script vận hành. |
| Tests | `tests`, `frontend/tests` | Backend and frontend tests for key application behavior. | Test backend và frontend cho các hành vi chính. |

## Module index / Danh mục module

| Module | Frontend route/page | Main API controllers | Main services |
|---|---|---|---|
| Authentication, MFA and profile / Xác thực, MFA và hồ sơ | `/`, `/profile` (`Login.jsx`, `Profile.jsx`) | `AuthController`, `OtpController` | `AuthService`, `TotpService`, `SmsService`, `EmailService` |
| Dashboard / Bảng điều khiển | `/dashboard` (`Dashboard.jsx`) | `DashboardController` | `DashboardService` |
| Customers, eKYC, CIC and documents / Khách hàng, eKYC, CIC và hồ sơ | `/customers` (`Customers.jsx`) | `CustomersController`, `EkycController`, `CicController`, `DocumentsController` | `CustomerService`, `CustomerLeadService`, `EkycService`, `CicService`, `DocumentService` |
| Loan products / Sản phẩm vay | `/products` (`LoanProducts.jsx`) | `LoanProductsController` | `LoanProductService` |
| Loan applications and approvals / Hồ sơ vay và phê duyệt | `/applications`, `/approvals` | `ApplicationsController`, `ApprovalRulesController`, `ApprovalFlowController`, `BodController` | `LoanApplicationService`, `ApprovalRuleService`, `ApprovalFlowService`, `BODService` |
| Loan servicing and lifecycle / Quản lý vòng đời khoản vay | `/loans` (`Loans.jsx`) | `LoansController`, `LoanLifecycleController`, `LoanAdjustmentsController`, `LoanTopUpController`, `ReversalsController` | `LoanQueryService`, `LoanDisbursementService`, `LoanPaymentService`, `LoanLifecycleService`, `LoanAdjustmentService`, `LoanTopUpService`, `ReversalService`, `EContractService` |
| Collateral / Tài sản bảo đảm | Loan/customer details | `CollateralsController`, `CollateralLifecycleController` | `CollateralService`, `CollateralLifecycleService` |
| Overdue, collections and dunning / Quá hạn, thu hồi nợ và nhắc nợ | `/overdue`, `/collections` | `CollectionsController`, `DunningController`, `JobsController` | `CollectionService`, `DunningService`, `OverdueJobService`, `LateFeeService`, `ClassificationJobService` |
| NAPAS, interbank and reconciliation / NAPAS, liên ngân hàng và đối soát | `/napas`, `/interbank-sync` | `NapasCoreController`, `NapasCallbackController`, `InterbankController`, `ReconciliationController` | `PaymentGatewayService`, `NapasCoreService`, `ReconciliationService`, `CoreBankingIntegrationService`, `VirtualAccountMatchingService` |
| E-wallet / Ví điện tử | `/ewallet-flow` | `EWalletController`, `WebhookController` | `EWalletService`, `MoMoEWalletProvider`, `VNPayEWalletProvider` |
| Accounting and periods / Kế toán và kỳ kế toán | System/reporting flows | `AccountingPeriodController`, `AccountingReportsController` | `AccountingService`, `AccountingPeriodService`, `AccrualJobService`, `EndOfDayService` |
| Reports and regulatory / Báo cáo và pháp quy | `/reports`, `/regulatory` | `ReportsController`, `NplReportsController`, `RegulatoryReportsController`, `DataExportController` | `ReportingService`, `NplReportingService`, `RegulatoryReportingService`, `DataExportService`, `LiquidityReportService`, `InterestRateReportService` |
| Risk, AML and exposure / Rủi ro, AML và giới hạn dư nợ | `/risk`, `/aml` | `RiskManagementController`, `AmlController`, `CapitalAdequacyController`, `ExposureLimitController` | `RiskModelService`, `StressTestService`, `AmlScreeningService`, `TransactionMonitoringService`, `CapitalAdequacyService`, `ExposureLimitService`, `ConcentrationLimitService` |
| Notifications / Thông báo | `/notifications`, notification bell | `NotificationsController`, SignalR `/api/hubs/notification` | `NotificationService`, `RealTimeNotificationService` |
| Branches, users, audit and system ops / Chi nhánh, người dùng, audit và vận hành | `/system-ops` | `BranchesController`, `AdminUsersController`, `AuditController`, `JobsController` | `BranchService`, `AdminUserService`, `AuditService`, background job services |
| Observability and deployment / Giám sát và triển khai | Docker/Nginx/Grafana | health `/health`, metrics endpoint | Docker Compose, Nginx, Prometheus, Grafana, Alertmanager |

---

## 1. Authentication, MFA and profile / Xác thực, MFA và hồ sơ

**English**
- Purpose: signs users in, issues JWT access tokens, handles refresh/session behavior, blocks MFA challenge tokens from normal API use, and supports TOTP/OTP flows.
- UI: `Login.jsx` starts authentication; `Profile.jsx` is the user-facing account area.
- Backend: `AuthController` and `OtpController` call `AuthService`, `TotpService`, `SmsService`, and `EmailService`.
- Data: `User`, `RefreshToken`, `PasswordHistory`, `EmailOtp`, permissions and roles.
- Notes: JWT authentication is registered in `Program.cs`; permission checks drive protected frontend routes through `AuthContext` and `permissions.js`.

**Tiếng Việt**
- Mục đích: đăng nhập người dùng, cấp JWT, xử lý refresh/session, chặn MFA challenge token khi gọi API thường, hỗ trợ TOTP/OTP.
- UI: `Login.jsx` xử lý đăng nhập; `Profile.jsx` là khu vực tài khoản cá nhân.
- Backend: `AuthController` và `OtpController` gọi `AuthService`, `TotpService`, `SmsService`, `EmailService`.
- Dữ liệu: `User`, `RefreshToken`, `PasswordHistory`, `EmailOtp`, quyền và vai trò.
- Ghi chú: JWT được cấu hình trong `Program.cs`; quyền truy cập route frontend dựa trên `AuthContext` và `permissions.js`.

## 2. Dashboard / Bảng điều khiển

**English**
- Purpose: gives a portfolio overview, application/approval counts, loan KPIs, trend data and operational alerts.
- UI: `/dashboard` renders `Dashboard.jsx`; frontend API lives in `features/dashboard/api/dashboardApi.js` and mapper logic in `features/dashboard/mappers`.
- Backend: `DashboardController` delegates to `DashboardService`.
- Data: loans, applications, overdue snapshots, payments, portfolio and trend aggregates.
- Notes: this is the default landing page after login and also receives badge-count updates for navigation.

**Tiếng Việt**
- Mục đích: hiển thị tổng quan danh mục, số hồ sơ/chờ duyệt, KPI khoản vay, xu hướng và cảnh báo vận hành.
- UI: route `/dashboard` dùng `Dashboard.jsx`; API frontend ở `features/dashboard/api/dashboardApi.js`, mapper ở `features/dashboard/mappers`.
- Backend: `DashboardController` gọi `DashboardService`.
- Dữ liệu: khoản vay, hồ sơ vay, snapshot quá hạn, thanh toán, tổng hợp danh mục và xu hướng.
- Ghi chú: đây là màn hình mặc định sau đăng nhập và cập nhật badge cho menu.

## 3. Customers, eKYC, CIC and documents / Khách hàng, eKYC, CIC và hồ sơ

**English**
- Purpose: manages customer profiles, customer leads, identity verification, credit-bureau checks and supporting documents.
- UI: `/customers` uses `Customers.jsx` and `features/customers/api/customerApi.js`.
- Backend: `CustomersController`, `EkycController`, `CicController`, `DocumentsController`.
- Services: `CustomerService`, `CustomerLeadService`, `EkycService`, `CicService`, `DocumentService`.
- Data: `Customer`, `CustomerDocument`, `CustomerCareNote`, `EkycVerification`, `CicInquiry`, `CreditCheck`.
- External integrations: mock or real FPT eKYC and PCB/CIC provider are selected from configuration in `Program.cs`.

**Tiếng Việt**
- Mục đích: quản lý thông tin khách hàng, lead khách hàng, xác minh định danh, tra cứu CIC và tài liệu đính kèm.
- UI: route `/customers` dùng `Customers.jsx` và `features/customers/api/customerApi.js`.
- Backend: `CustomersController`, `EkycController`, `CicController`, `DocumentsController`.
- Service: `CustomerService`, `CustomerLeadService`, `EkycService`, `CicService`, `DocumentService`.
- Dữ liệu: `Customer`, `CustomerDocument`, `CustomerCareNote`, `EkycVerification`, `CicInquiry`, `CreditCheck`.
- Tích hợp ngoài: FPT eKYC và PCB/CIC có chế độ mock hoặc thật theo cấu hình trong `Program.cs`.

## 4. Loan products / Sản phẩm vay

**English**
- Purpose: defines available loan products, terms, interest parameters, fees and eligibility boundaries used when creating applications.
- UI: `/products` uses `LoanProducts.jsx` and `features/products/api/productApi.js`.
- Backend: `LoanProductsController` and `LoanProductService`.
- Data: `LoanProduct`, rate/fee fields and product status.
- Notes: route visibility is permission-controlled by `can.viewProductsNav`.

**Tiếng Việt**
- Mục đích: định nghĩa sản phẩm vay, kỳ hạn, lãi suất, phí và điều kiện dùng khi tạo hồ sơ vay.
- UI: route `/products` dùng `LoanProducts.jsx` và `features/products/api/productApi.js`.
- Backend: `LoanProductsController` và `LoanProductService`.
- Dữ liệu: `LoanProduct`, thông tin lãi/phí và trạng thái sản phẩm.
- Ghi chú: menu/route được kiểm soát bởi quyền `can.viewProductsNav`.

## 5. Loan applications and approvals / Hồ sơ vay và phê duyệt

**English**
- Purpose: handles loan origination from draft application to submission, rule evaluation, maker-checker review, approval or rejection.
- UI: `/applications` uses `Applications.jsx`; `/approvals` uses `Approvals.jsx`; modals live under `features/applications/components`.
- Backend: `ApplicationsController`, `ApprovalRulesController`, `ApprovalFlowController`, `BodController`.
- Services: `LoanApplicationService`, `ApprovalRuleService`, `ApprovalFlowService`, `BODService`.
- Data: `LoanApplication`, `ApplicationDecision`, `ApprovalRule`, `PendingApprovalRequest`.
- Notes: approval navigation is permission-controlled and badge counts are surfaced to the layout.

**Tiếng Việt**
- Mục đích: xử lý khởi tạo hồ sơ vay từ bản nháp, submit, chạy rule, maker-checker, duyệt hoặc từ chối.
- UI: `/applications` dùng `Applications.jsx`; `/approvals` dùng `Approvals.jsx`; modal nằm trong `features/applications/components`.
- Backend: `ApplicationsController`, `ApprovalRulesController`, `ApprovalFlowController`, `BodController`.
- Service: `LoanApplicationService`, `ApprovalRuleService`, `ApprovalFlowService`, `BODService`.
- Dữ liệu: `LoanApplication`, `ApplicationDecision`, `ApprovalRule`, `PendingApprovalRequest`.
- Ghi chú: menu phê duyệt theo quyền và số lượng chờ xử lý được hiển thị trên layout.

## 6. Loan servicing and lifecycle / Quản lý vòng đời khoản vay

**English**
- Purpose: manages active loans after approval, including disbursement, repayment, early settlement, e-contracts, adjustments, restructure/reschedule, top-up, write-off/recovery and reversals.
- UI: `/loans` uses `Loans.jsx`; modals such as `DisburseModal`, `PaymentModal`, `EarlySettleModal`, `EContractModal`, and `LoanDetailModal` live in `features/loans/components`.
- Backend: `LoansController`, `LoanLifecycleController`, `LoanAdjustmentsController`, `LoanTopUpController`, `ReversalsController`.
- Services: `LoanQueryService`, `LoanDisbursementService`, `LoanPaymentService`, `LoanLifecycleService`, `LoanAdjustmentService`, `LoanTopUpService`, `ReversalService`, `EContractService`.
- Data: `Loan`, `RepaymentSchedule`, `LoanTransaction`, `LoanDisbursement`, `Payment`, `PaymentAllocation`, `LoanAdjustment`, `LoanRestructure`, `LoanReschedule`, `LoanWriteOff`, `LoanRecovery`, `TransactionReversal`, `EContract`.

**Tiếng Việt**
- Mục đích: quản lý khoản vay sau duyệt, gồm giải ngân, thu nợ, tất toán sớm, hợp đồng điện tử, điều chỉnh, cơ cấu/dời lịch, top-up, xóa/thu hồi nợ và reversal.
- UI: route `/loans` dùng `Loans.jsx`; các modal như `DisburseModal`, `PaymentModal`, `EarlySettleModal`, `EContractModal`, `LoanDetailModal` nằm trong `features/loans/components`.
- Backend: `LoansController`, `LoanLifecycleController`, `LoanAdjustmentsController`, `LoanTopUpController`, `ReversalsController`.
- Service: `LoanQueryService`, `LoanDisbursementService`, `LoanPaymentService`, `LoanLifecycleService`, `LoanAdjustmentService`, `LoanTopUpService`, `ReversalService`, `EContractService`.
- Dữ liệu: `Loan`, `RepaymentSchedule`, `LoanTransaction`, `LoanDisbursement`, `Payment`, `PaymentAllocation`, `LoanAdjustment`, `LoanRestructure`, `LoanReschedule`, `LoanWriteOff`, `LoanRecovery`, `TransactionReversal`, `EContract`.

## 7. Collateral / Tài sản bảo đảm

**English**
- Purpose: records secured assets, links collateral to loans, supports valuation/revaluation and collateral lifecycle actions.
- UI: collateral is usually accessed through customer or loan-detail workflows rather than a standalone top-level route.
- Backend: `CollateralsController`, `CollateralLifecycleController`.
- Services: `CollateralService`, `CollateralLifecycleService`.
- Data: `Collateral`, `LoanCollateral`, `CollateralRevaluation`.

**Tiếng Việt**
- Mục đích: ghi nhận tài sản bảo đảm, liên kết tài sản với khoản vay, hỗ trợ định giá/tái định giá và vòng đời tài sản.
- UI: thường đi qua luồng khách hàng hoặc chi tiết khoản vay, không phải route độc lập trên menu chính.
- Backend: `CollateralsController`, `CollateralLifecycleController`.
- Service: `CollateralService`, `CollateralLifecycleService`.
- Dữ liệu: `Collateral`, `LoanCollateral`, `CollateralRevaluation`.

## 8. Overdue, collections and dunning / Quá hạn, thu hồi nợ và nhắc nợ

**English**
- Purpose: detects overdue loans, calculates late fees, assigns collectors, records collection actions and runs dunning/escalation strategies.
- UI: `/overdue` uses `Overdue.jsx`; `/collections` uses `Collections.jsx`; collection note modal lives in `features/overdue/components`.
- Backend: `CollectionsController`, `DunningController`, `JobsController`.
- Services: `CollectionService`, `DunningService`, `OverdueJobService`, `LateFeeService`, `ClassificationJobService`.
- Data: `DelinquencyStatus`, `OverdueDailySnapshot`, `CollectionAction`, `CollectionNote`, `CollectorAssignment`, `DunningStrategy`, `EscalationRule`, `LateFee`, `ClassificationSnapshot`.
- Background: `DailyOverdueBackgroundService` runs scheduled overdue work.

**Tiếng Việt**
- Mục đích: phát hiện khoản vay quá hạn, tính phí trễ hạn, phân công collector, ghi nhận hành động thu hồi nợ và chạy chiến lược nhắc nợ/escalation.
- UI: `/overdue` dùng `Overdue.jsx`; `/collections` dùng `Collections.jsx`; modal ghi chú nằm trong `features/overdue/components`.
- Backend: `CollectionsController`, `DunningController`, `JobsController`.
- Service: `CollectionService`, `DunningService`, `OverdueJobService`, `LateFeeService`, `ClassificationJobService`.
- Dữ liệu: `DelinquencyStatus`, `OverdueDailySnapshot`, `CollectionAction`, `CollectionNote`, `CollectorAssignment`, `DunningStrategy`, `EscalationRule`, `LateFee`, `ClassificationSnapshot`.
- Job nền: `DailyOverdueBackgroundService` chạy xử lý quá hạn theo lịch.

## 9. NAPAS, interbank and reconciliation / NAPAS, liên ngân hàng và đối soát

**English**
- Purpose: integrates disbursement/payment rails, NAPAS transaction query, callback handling, interbank synchronization, virtual-account matching and reconciliation exceptions.
- UI: `/napas` uses `Napas.jsx`; `/interbank-sync` uses `InterbankSync.jsx`.
- Backend: `NapasCoreController`, `NapasCallbackController`, `InterbankController`, `ReconciliationController`.
- Services: `PaymentGatewayService`, `NapasCoreService`, `ReconciliationService`, `CoreBankingIntegrationService`, `VirtualAccountMatchingService`.
- Data: `Payment`, `SuspenseTransaction`, `ReconciliationException`, `IdempotencyRecord`, `LoanDisbursement`.
- External integrations: NAPAS has mock and real HTTP-provider modes configured in `Program.cs`.

**Tiếng Việt**
- Mục đích: tích hợp kênh thanh toán/giải ngân, tra cứu giao dịch NAPAS, nhận callback, đồng bộ liên ngân hàng, matching tài khoản ảo và xử lý lệch đối soát.
- UI: `/napas` dùng `Napas.jsx`; `/interbank-sync` dùng `InterbankSync.jsx`.
- Backend: `NapasCoreController`, `NapasCallbackController`, `InterbankController`, `ReconciliationController`.
- Service: `PaymentGatewayService`, `NapasCoreService`, `ReconciliationService`, `CoreBankingIntegrationService`, `VirtualAccountMatchingService`.
- Dữ liệu: `Payment`, `SuspenseTransaction`, `ReconciliationException`, `IdempotencyRecord`, `LoanDisbursement`.
- Tích hợp ngoài: NAPAS có chế độ mock hoặc HTTP provider thật theo cấu hình trong `Program.cs`.

## 10. E-wallet / Ví điện tử

**English**
- Purpose: models wallet payment/provider flow, provider callbacks/webhooks and wallet-linked customer/payment scenarios.
- UI: `/ewallet-flow` uses `EWalletFlowMap.jsx` and `features/ewallet/api/ewalletApi.js`.
- Backend: `EWalletController`, `WebhookController`.
- Services: `EWalletService`, configured `IEWalletProvider` implementations for MoMo and VNPay.
- Data: `CustomerEWallet`, `Payment`, `IdempotencyRecord`.
- External integrations: mock providers are used by default unless MoMo/VNPay real-provider config is enabled.

**Tiếng Việt**
- Mục đích: mô phỏng và xử lý luồng ví điện tử, callback/webhook nhà cung cấp và các tình huống thanh toán/liên kết ví của khách hàng.
- UI: `/ewallet-flow` dùng `EWalletFlowMap.jsx` và `features/ewallet/api/ewalletApi.js`.
- Backend: `EWalletController`, `WebhookController`.
- Service: `EWalletService`, các implementation `IEWalletProvider` cho MoMo và VNPay.
- Dữ liệu: `CustomerEWallet`, `Payment`, `IdempotencyRecord`.
- Tích hợp ngoài: mặc định dùng mock provider, chỉ dùng provider thật khi bật cấu hình MoMo/VNPay.

## 11. Accounting and periods / Kế toán và kỳ kế toán

**English**
- Purpose: records double-entry accounting, general-ledger mappings, journal entries, accrual jobs, EOD processing and open/close accounting periods.
- UI: exposed through reporting/system flows rather than a single route.
- Backend: `AccountingPeriodController`, `AccountingReportsController`.
- Services: `AccountingService`, `AccountingPeriodService`, `AccrualJobService`, `EndOfDayService`.
- Data: `GLAccount`, `GLAccountMapping`, `JournalEntry`, `JournalEntryLine`, `AccountingPeriod`, `EodBatchRecord`, `ExchangeRate`, `InterestRateHistory`.

**Tiếng Việt**
- Mục đích: ghi nhận bút toán kép, mapping tài khoản kế toán, journal entry, accrual job, xử lý cuối ngày và đóng/mở kỳ kế toán.
- UI: xuất hiện qua luồng báo cáo/vận hành thay vì một route riêng.
- Backend: `AccountingPeriodController`, `AccountingReportsController`.
- Service: `AccountingService`, `AccountingPeriodService`, `AccrualJobService`, `EndOfDayService`.
- Dữ liệu: `GLAccount`, `GLAccountMapping`, `JournalEntry`, `JournalEntryLine`, `AccountingPeriod`, `EodBatchRecord`, `ExchangeRate`, `InterestRateHistory`.

## 12. Reports and regulatory / Báo cáo và pháp quy

**English**
- Purpose: produces operational reports, NPL reports, regulatory reports, exports and finance/liquidity/interest-rate reporting.
- UI: `/reports` uses `Reports.jsx`; `/regulatory` uses `RegulatoryReports.jsx`.
- Backend: `ReportsController`, `NplReportsController`, `RegulatoryReportsController`, `DataExportController`.
- Services: `ReportingService`, `NplReportingService`, `RegulatoryReportingService`, `DataExportService`, `LiquidityReportService`, `InterestRateReportService`.
- Data: loan portfolio, repayment schedules, classification snapshots, accounting data and exported report DTOs.

**Tiếng Việt**
- Mục đích: tạo báo cáo vận hành, báo cáo nợ xấu, báo cáo pháp quy, export dữ liệu và báo cáo thanh khoản/lãi suất.
- UI: `/reports` dùng `Reports.jsx`; `/regulatory` dùng `RegulatoryReports.jsx`.
- Backend: `ReportsController`, `NplReportsController`, `RegulatoryReportsController`, `DataExportController`.
- Service: `ReportingService`, `NplReportingService`, `RegulatoryReportingService`, `DataExportService`, `LiquidityReportService`, `InterestRateReportService`.
- Dữ liệu: danh mục khoản vay, lịch trả nợ, snapshot phân loại nợ, dữ liệu kế toán và DTO báo cáo/export.

## 13. Risk, AML and exposure / Rủi ro, AML và giới hạn dư nợ

**English**
- Purpose: supports AML screening, transaction monitoring, risk scoring, stress testing, capital adequacy, concentration limits and customer exposure limits.
- UI: `/aml` uses `AmlDashboard.jsx`; `/risk` uses `RiskDashboard.jsx`.
- Backend: `AmlController`, `RiskManagementController`, `CapitalAdequacyController`, `ExposureLimitController`.
- Services: `AmlScreeningService`, `TransactionMonitoringService`, `RiskModelService`, `StressTestService`, `CapitalAdequacyService`, `ConcentrationLimitService`, `ExposureLimitService`, `FinancialStatementService`.
- Data: `AmlRiskAssessment`, `SuspiciousTransactionReport`, `CustomerExposureLimit`, `Customer`, `Loan`, `Payment`, capital/risk DTOs.

**Tiếng Việt**
- Mục đích: hỗ trợ sàng lọc AML, giám sát giao dịch, chấm điểm rủi ro, stress test, an toàn vốn, giới hạn tập trung và giới hạn dư nợ khách hàng.
- UI: `/aml` dùng `AmlDashboard.jsx`; `/risk` dùng `RiskDashboard.jsx`.
- Backend: `AmlController`, `RiskManagementController`, `CapitalAdequacyController`, `ExposureLimitController`.
- Service: `AmlScreeningService`, `TransactionMonitoringService`, `RiskModelService`, `StressTestService`, `CapitalAdequacyService`, `ConcentrationLimitService`, `ExposureLimitService`, `FinancialStatementService`.
- Dữ liệu: `AmlRiskAssessment`, `SuspiciousTransactionReport`, `CustomerExposureLimit`, `Customer`, `Loan`, `Payment`, DTO vốn/rủi ro.

## 14. Notifications / Thông báo

**English**
- Purpose: creates user notifications and pushes real-time events for approvals, disbursements, payments, callbacks and operational events.
- UI: `/notifications` uses `Notifications.jsx`; `NotificationBell.jsx` displays live status in the layout.
- Backend: `NotificationsController` and SignalR hub `NotificationHub` mapped at `/api/hubs/notification`.
- Services: `NotificationService`, `RealTimeNotificationService`.
- Data: `Notification`, `NotificationTemplate`.

**Tiếng Việt**
- Mục đích: tạo thông báo cho người dùng và đẩy sự kiện realtime cho phê duyệt, giải ngân, thanh toán, callback và sự kiện vận hành.
- UI: `/notifications` dùng `Notifications.jsx`; `NotificationBell.jsx` hiển thị trạng thái realtime trên layout.
- Backend: `NotificationsController` và SignalR hub `NotificationHub` tại `/api/hubs/notification`.
- Service: `NotificationService`, `RealTimeNotificationService`.
- Dữ liệu: `Notification`, `NotificationTemplate`.

## 15. Branches, users, audit and system ops / Chi nhánh, người dùng, audit và vận hành

**English**
- Purpose: provides admin operations for branches, users, permissions, audit trail, job triggers and operational controls.
- UI: `/system-ops` uses `SystemOps.jsx`; admin APIs live under `features/system/api`.
- Backend: `BranchesController`, `AdminUsersController`, `AuditController`, `JobsController`.
- Services: `BranchService`, `AdminUserService`, `AuditService`, plus job services such as `OverdueJobService`, `AccrualJobService`, `ClassificationJobService`.
- Data: `Branch`, `User`, `Permission`, `AuditLog`, job/batch records.
- Notes: `BranchFilterMiddleware` and audit interceptors are registered in `Program.cs` to scope data and record changes.

**Tiếng Việt**
- Mục đích: cung cấp chức năng quản trị chi nhánh, người dùng, phân quyền, audit trail, kích hoạt job và điều khiển vận hành.
- UI: `/system-ops` dùng `SystemOps.jsx`; API admin frontend nằm trong `features/system/api`.
- Backend: `BranchesController`, `AdminUsersController`, `AuditController`, `JobsController`.
- Service: `BranchService`, `AdminUserService`, `AuditService`, cùng các job service như `OverdueJobService`, `AccrualJobService`, `ClassificationJobService`.
- Dữ liệu: `Branch`, `User`, `Permission`, `AuditLog`, record job/batch.
- Ghi chú: `BranchFilterMiddleware` và audit interceptor được đăng ký trong `Program.cs` để lọc dữ liệu và ghi nhận thay đổi.

## 16. Observability and deployment / Giám sát và triển khai

**English**
- Purpose: runs the system locally or in production-like Docker Compose, terminates traffic through Nginx, exposes health/metrics and provisions monitoring/alerting.
- Entrypoints: `docker-compose.yml`, `docker-compose.override.yml`, `Dockerfile`, `nginx.conf`, `.env.example`.
- Monitoring: `prometheus.yml`, `grafana/`, `alertmanager/`; API maps `/health` and Prometheus scraping endpoint.
- Runtime: MySQL database, API container, frontend/Nginx, optional exporters and alerting services.
- Notes: production guardrails in `Program.cs` validate secrets, webhook actor, mock integrations and migration behavior.

**Tiếng Việt**
- Mục đích: chạy hệ thống bằng Docker Compose cho local/production-like, terminate traffic qua Nginx, expose health/metrics và cấu hình giám sát/cảnh báo.
- Điểm vào: `docker-compose.yml`, `docker-compose.override.yml`, `Dockerfile`, `nginx.conf`, `.env.example`.
- Giám sát: `prometheus.yml`, `grafana/`, `alertmanager/`; API có `/health` và endpoint Prometheus.
- Runtime: MySQL, API container, frontend/Nginx, exporter và alerting tùy cấu hình.
- Ghi chú: các guardrail production trong `Program.cs` kiểm tra secret, webhook actor, mock integration và hành vi migration.

---

## How to add or update a module / Cách thêm hoặc cập nhật module

**English**
1. Add or update the domain entity in `src/LoanFlow.Domain`.
2. Add DTOs, interfaces and business service logic in `src/LoanFlow.Application`.
3. Wire persistence in `src/LoanFlow.Infrastructure` and create an EF migration when schema changes.
4. Add API endpoints in `src/LoanFlow.API/Controllers` and register services in `Program.cs` if needed.
5. Add frontend API functions, page/components, route and permissions under `frontend/src`.
6. Add focused tests under `tests` or `frontend/tests`.
7. Update this document when the module boundary, UI route, controller or service ownership changes.

**Tiếng Việt**
1. Thêm hoặc cập nhật entity trong `src/LoanFlow.Domain`.
2. Thêm DTO, interface và logic service trong `src/LoanFlow.Application`.
3. Kết nối lưu trữ trong `src/LoanFlow.Infrastructure` và tạo EF migration khi đổi schema.
4. Thêm endpoint trong `src/LoanFlow.API/Controllers` và đăng ký service trong `Program.cs` nếu cần.
5. Thêm API frontend, page/component, route và quyền trong `frontend/src`.
6. Thêm test tập trung trong `tests` hoặc `frontend/tests`.
7. Cập nhật tài liệu này khi ranh giới module, route UI, controller hoặc service owner thay đổi.
