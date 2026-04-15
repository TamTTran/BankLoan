# Production Readiness Requirements

Date: 2026-04-15
Project: LoanFlow Lite
Purpose: Tổng hợp các requirement còn thiếu để hệ thống có thể triển khai production an toàn và vận hành ổn định.

## 1. Mục tiêu

Tài liệu này xác định các function và capability cần bổ sung trước khi đưa hệ thống lên production. Trọng tâm là:

- Đảm bảo deploy được thực tế trong môi trường production.
- Giảm rủi ro sai lệch dữ liệu ở các flow tài chính.
- Tăng khả năng giám sát, vận hành, và điều tra sự cố.
- Hoàn thiện các control cần thiết cho hệ thống quản lý khoản vay.

## 2. Phạm vi đánh giá

Phạm vi dựa trên review codebase hiện tại gồm:

- Backend ASP.NET Core 8 Web API
- EF Core + MySQL
- Frontend React/Vite
- Docker Compose deployment
- Security review và runtime configuration hiện có

## 3. Current State Summary

Hệ thống hiện đã có phần lớn nghiệp vụ cốt lõi:

- Authentication và role-based authorization
- Customer management
- Loan product management
- Loan application và approval workflow
- Loan creation, disbursement, repayment schedule
- Payment posting
- Overdue dashboard
- Audit log cơ bản

Tuy nhiên, vẫn còn thiếu một số requirement quan trọng ở lớp production operations, resiliency, governance, và financial safety.

## 4. Must-Have Requirements Before Production

### 4.1 Health Check Endpoint

Requirement:

- Hệ thống phải cung cấp endpoint health check như `GET /health`.
- Health check phải phản ánh tối thiểu:
  - API process đang hoạt động
  - Database connectivity
  - Trạng thái dependency quan trọng

Business reason:

- Docker Compose hiện đang dùng health check cho API.
- Nếu không có endpoint health check, container orchestration và monitoring sẽ đánh giá sai trạng thái service.

Expected outcome:

- DevOps có thể dùng health check để restart, alert, và xác nhận service readiness/liveness.

### 4.2 Global Exception Handling and Standard Error Response

Requirement:

- Hệ thống phải có global exception handler.
- Mọi lỗi chưa xử lý phải trả về format thống nhất, ví dụ:
  - error code
  - message
  - traceId
  - timestamp
- Không được expose stack trace hoặc internal exception detail ra client trong production.

Business reason:

- Tránh lỗi trả về không nhất quán giữa các API.
- Giảm rủi ro lộ thông tin nội bộ hệ thống.
- Hỗ trợ troubleshooting nhanh hơn khi có incident.

### 4.3 Startup Configuration Validation

Requirement:

- Ứng dụng phải validate toàn bộ config bắt buộc ngay lúc startup.
- Các biến cần kiểm tra tối thiểu:
  - Connection string
  - JWT signing key
  - JWT issuer/audience
  - CORS allowed origins
- Nếu config thiếu hoặc không hợp lệ, application phải fail fast.

Business reason:

- Production không được phép chạy với config thiếu hoặc secret yếu.
- Tránh tình trạng deploy thành công nhưng runtime lỗi ngầm.

### 4.4 Transaction Management for Financial Commands

Requirement:

- Các flow nghiệp vụ tài chính phải chạy trong transaction database.
- Áp dụng tối thiểu cho:
  - Approve application
  - Create loan
  - Disburse loan
  - Record payment
  - Early settlement
- Nếu một bước thất bại, toàn bộ command phải rollback.

Business reason:

- Các flow hiện tại có nhiều lần `SaveChangesAsync()` trong cùng một nghiệp vụ.
- Với production, cách này tạo rủi ro ghi dữ liệu dở dang, lệch trạng thái, hoặc mất đồng nhất giữa loan, payment, transaction, audit.

### 4.5 Idempotency Control for Payment and Disbursement

Requirement:

- Các API nhạy cảm phải hỗ trợ idempotency key hoặc request reference duy nhất.
- Bắt buộc áp dụng cho:
  - Loan disbursement
  - Payment posting
  - Early settlement
- Nếu client retry cùng request, hệ thống không được tạo giao dịch trùng.

Business reason:

- Production thường gặp retry do timeout, mạng chập chờn, user bấm submit nhiều lần.
- Double posting là rủi ro nghiêm trọng với hệ thống tài chính.

### 4.6 Audit Log Query and Investigation Function

Requirement:

- Phải có API hoặc module để tra cứu audit log.
- Cho phép filter theo:
  - actor user
  - action code
  - entity type
  - entity id
  - date range
- Nên có phân quyền chỉ cho vai trò phù hợp truy cập.

Business reason:

- Hiện hệ thống có ghi audit nhưng chưa có chức năng khai thác log phục vụ vận hành và kiểm soát.
- Khi xảy ra khiếu nại hoặc incident, đội vận hành cần truy vết được ai làm gì, lúc nào.

## 5. High-Priority Requirements After Go-Live Preparation

### 5.1 Background Job for Overdue and Late Fee Processing

Requirement:

- Cần có scheduled job chạy theo ngày để:
  - cập nhật trạng thái overdue
  - tính late fee
  - snapshot DPD/bucket quá hạn
  - tạo dữ liệu phục vụ báo cáo

Business reason:

- Hiện logic overdue và late fee phụ thuộc nhiều vào query-time calculation.
- Production nên có xử lý batch chủ động để dữ liệu ổn định và nhất quán hơn.

### 5.2 Notification and Reminder Function

Requirement:

- Hệ thống cần hỗ trợ gửi nhắc nợ và thông báo workflow.
- Tối thiểu gồm:
  - payment due reminder
  - overdue reminder
  - approval pending notification
  - settlement/disbursement notification

Business reason:

- Đây là chức năng trực tiếp giúp giảm delinquency và cải thiện vận hành.

### 5.3 Reconciliation Function

Requirement:

- Cần có chức năng đối soát giữa dữ liệu hệ thống và nguồn bên ngoài.
- Tối thiểu hỗ trợ:
  - đối soát payment
  - đối soát disbursement
  - phát hiện transaction lệch, trùng, thiếu

Business reason:

- Hệ thống tài chính production bắt buộc cần đối soát để kiểm soát sai lệch tiền.

### 5.4 Session Administration

Requirement:

- Cần có function cho admin hoặc security operator:
  - xem session active của user
  - revoke session
  - force logout
  - lock/unlock account
  - reset credential theo policy

Business reason:

- Đây là control tối thiểu để xử lý sự cố bảo mật và support user.

### 5.5 Observability

Requirement:

- Cần bổ sung observability stack gồm:
  - structured logging
  - correlation id
  - request tracing
  - metrics cơ bản
  - alerting hook

Business reason:

- Khi production lỗi, đội vận hành phải nhanh chóng biết request nào lỗi, lỗi ở đâu, ảnh hưởng tới ai.

## 6. Release Governance Requirements

### 6.1 Automated Test Coverage

Requirement:

- Cần có test project cho business-critical flows.
- Tối thiểu phải cover:
  - auth
  - application approval
  - loan creation
  - disbursement
  - payment allocation
  - overdue/late fee logic

Business reason:

- Đây là hệ thống có logic tài chính.
- Không có automated test thì mỗi lần sửa sẽ có nguy cơ regression cao.

### 6.2 Optimistic Concurrency Control

Requirement:

- Các entity nhạy cảm cần có concurrency token như `RowVersion` hoặc cơ chế tương đương.
- Tối thiểu áp dụng cho:
  - LoanApplication
  - Loan
  - RepaymentSchedule
  - Payment-related records

Business reason:

- Tránh tình trạng hai user thao tác đồng thời gây approve/disburse/pay trùng hoặc ghi đè dữ liệu.

### 6.3 Backup, Restore, and Retention Policy

Requirement:

- Cần có quy trình và function vận hành cho:
  - backup database
  - restore test định kỳ
  - retention audit/session/log data
  - purge dữ liệu hết hạn theo policy

Business reason:

- Production không chỉ cần chạy được mà còn phải khôi phục được khi có sự cố.

### 6.4 User and Role Administration

Requirement:

- Cần có module quản trị user/role hoàn chỉnh:
  - create user
  - deactivate user
  - assign/change role
  - branch/ownership assignment
  - password reset policy

Business reason:

- README mô tả quyền Admin nhưng code hiện chưa thể hiện đầy đủ module quản trị production-ready.

## 7. Recommended Priority Order

### Phase 1 - Mandatory Before Production

- Health check endpoint
- Global exception handling
- Startup configuration validation
- Transaction management
- Idempotency for payment/disbursement
- Audit log query

### Phase 2 - Strongly Recommended

- Background overdue/late fee job
- Notification/reminder
- Session administration
- Observability
- Automated tests
- Concurrency control

### Phase 3 - Operational Maturity

- Reconciliation
- Backup/restore and retention
- Full user-role administration

## 8. Final Recommendation

Kết luận:

- Project hiện phù hợp mức demo hoặc pilot nội bộ.
- Để đạt mức production thực tế, cần ưu tiên hoàn thiện lớp vận hành và kiểm soát thay vì chỉ thêm nghiệp vụ mới.
- Sáu requirement cần hoàn tất trước go-live là:
  - Health check
  - Global exception handling
  - Startup config validation
  - Transaction management
  - Idempotency control
  - Audit log query

Sau khi hoàn thành các mục trên, hệ thống mới đủ nền tảng để deploy production với mức rủi ro chấp nhận được.
