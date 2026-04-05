# LoanFlow Lite — Functional Planning

## 1. Mục tiêu planning
Planning đi từ:

**Chức năng nghiệp vụ -> dữ liệu -> flow -> API -> UI -> sprint**

Mục tiêu của bản demo:
- tạo khoản vay
- thu tiền và theo dõi dư nợ
- theo dõi khoản vay đến hạn và quá hạn
- đủ rõ để demo, chia sprint, và triển khai MVP

---

## 2. Phạm vi sản phẩm
**LoanFlow Lite** là hệ thống quản lý khoản vay gọn cho:
- cửa hàng bán trả góp
- công ty tài chính nhỏ
- SME lender
- đại lý điện máy / xe máy / thiết bị

### Giá trị chính
- gom toàn bộ vòng đời khoản vay vào một hệ thống
- giảm quản lý thủ công bằng Excel hoặc chat
- kiểm soát phê duyệt
- theo dõi thu nợ và quá hạn

---

## 3. Nhóm chức năng

### Nhóm A — Nền tảng

#### A1. Authentication & RBAC
**Mục tiêu:** phân quyền người dùng.

**Vai trò:**
- Admin
- Loan Officer
- Approver
- Collector

**Chức năng:**
- login/logout
- role permission
- audit log cơ bản

**Output:**
- người dùng đăng nhập được
- mỗi role thấy đúng menu và đúng action

#### A2. Master Data
**Mục tiêu:** tạo dữ liệu chuẩn cho toàn hệ thống.

**Chức năng:**
- branch
- user
- payment method
- loan status
- overdue bucket

---

### Nhóm B — Chức năng lõi

#### B1. Customer Management
**Mục tiêu:** tạo và quản lý hồ sơ người vay.

**Chức năng:**
- tạo khách hàng
- cập nhật thông tin cá nhân
- lưu giấy tờ cơ bản
- xem danh sách khoản vay của khách

**Dữ liệu chính:**
- `customers`
- `customer_documents`

**Màn hình:**
- customer list
- customer detail
- create/edit customer

**Acceptance criteria:**
- tạo mới khách hàng thành công
- không trùng CCCD/số điện thoại
- xem được lịch sử khoản vay

#### B2. Loan Product Management
**Mục tiêu:** định nghĩa sản phẩm vay.

**Chức năng:**
- tên sản phẩm
- min/max amount
- tenor
- lãi suất
- phí xử lý
- repayment frequency

**Dữ liệu chính:**
- `loan_products`

**Màn hình:**
- product list
- create/edit product

**Acceptance criteria:**
- tạo sản phẩm thành công
- validate min/max/tenor/rate hợp lệ

#### B3. Loan Application
**Mục tiêu:** tiếp nhận nhu cầu vay.

**Chức năng:**
- chọn customer
- chọn product
- nhập số tiền vay
- nhập kỳ hạn
- tính installment dự kiến
- trạng thái: Draft / Submitted / Rejected / Approved

**Dữ liệu chính:**
- `loan_applications`

**Màn hình:**
- application form
- application list
- application detail

**Acceptance criteria:**
- chọn được customer + product
- tính preview installment
- submit hồ sơ thành công

#### B4. Approval Workflow
**Mục tiêu:** duyệt khoản vay.

**Chức năng:**
- submit
- approve
- reject
- remark
- lịch sử duyệt

**Dữ liệu chính:**
- `application_decisions`
- `audit_logs`

**Màn hình:**
- approval queue
- decision history

**Acceptance criteria:**
- chỉ approver mới duyệt được
- reject phải có reason
- lưu lịch sử quyết định

#### B5. Loan Account Creation + Disbursement
**Mục tiêu:** chuyển hồ sơ được duyệt thành khoản vay thật.

**Chức năng:**
- tạo loan account
- sinh mã khoản vay
- đánh dấu giải ngân
- ngày giải ngân
- số tiền giải ngân

**Dữ liệu chính:**
- `loans`
- `loan_disbursements`

**Màn hình:**
- loan detail
- disbursement action

**Acceptance criteria:**
- chỉ approved application mới tạo loan được
- loan account number duy nhất

#### B6. Repayment Schedule
**Mục tiêu:** sinh lịch trả nợ.

**Chức năng:**
- lịch trả theo tháng
- tách gốc/lãi
- ngày đến hạn
- dư nợ còn lại

**Dữ liệu chính:**
- `repayment_schedules`

**Màn hình:**
- repayment schedule tab trong loan detail

**Acceptance criteria:**
- sinh đủ số kỳ
- mỗi kỳ có due date, principal, interest, balance

#### B7. Payment Posting
**Mục tiêu:** ghi nhận khách trả tiền.

**Chức năng:**
- nhập số tiền trả
- payment date
- auto phân bổ gốc/lãi/phí
- cập nhật khoản vay
- lịch sử thanh toán

**Dữ liệu chính:**
- `loan_transactions`
- `payments`

**Màn hình:**
- collect payment
- payment history

**Acceptance criteria:**
- ghi nhận payment thành công
- cập nhật remaining balance
- lưu payment history

#### B8. Overdue & Collections Lite
**Mục tiêu:** nhìn thấy ai quá hạn.

**Chức năng:**
- xác định overdue
- số ngày quá hạn
- bucket 1–7, 8–30, 31+
- danh sách khách cần follow-up
- ghi chú thu hồi cơ bản

**Dữ liệu chính:**
- `delinquency_status`
- `collection_notes`

**Màn hình:**
- overdue dashboard
- overdue list
- collection note

**Acceptance criteria:**
- khoản chưa thanh toán sau due date phải chuyển overdue
- hiển thị đúng bucket quá hạn

---

### Nhóm C — Chức năng tăng giá trị

#### C1. Notifications
- SMS reminder
- due date reminder
- overdue reminder

#### C2. Reports
- total outstanding
- due today
- overdue summary
- disbursement report
- collection performance

#### C3. Document Management
- upload hồ sơ
- preview file
- checklist tài liệu

---

### Nhóm D — Chức năng nâng cấp

#### D1. Penalty Engine
- late fee
- overdue penalty
- auto calculation

#### D2. Restructure / Reschedule
- đổi kỳ hạn
- giảm installment
- kéo dài khoản vay

#### D3. Accounting Export
- export journal
- mapping sang phần mềm kế toán

#### D4. Customer Portal
- khách xem dư nợ
- lịch trả nợ
- trạng thái thanh toán

---

## 4. Thứ tự ưu tiên

### P0 — Bắt buộc
- Authentication
- Customer
- Loan Product
- Loan Application
- Approval
- Loan Account
- Repayment Schedule
- Payment Posting
- Overdue Dashboard

### P1 — Giúp dễ bán
- Reports
- Notifications
- Document upload
- Audit log nâng cao
- Role matrix đầy đủ

### P2 — Tăng giá trị hợp đồng
- Penalty automation
- Restructure
- Accounting export
- Multi-branch
- Mobile collector

---

## 5. Dependency giữa các chức năng
Thứ tự phụ thuộc:

**Auth -> Master Data -> Customer -> Product -> Application -> Approval -> Loan Account -> Schedule -> Payment -> Overdue -> Reports**

### Lý do
- chưa có product thì không test application
- chưa approved thì không sinh loan
- chưa có schedule thì không phân bổ payment
- chưa có payment thì overdue không có ý nghĩa

---

## 6. Planning theo sprint

### Sprint 1 — Foundation
**Mục tiêu:** có nền để nhập liệu.

**Phạm vi:**
- auth
- role
- user
- branch
- customer CRUD
- product CRUD

**Kết quả cuối sprint:**
- login được
- tạo customer được
- tạo product được

### Sprint 2 — Loan Application & Approval
**Mục tiêu:** đi được từ hồ sơ vay đến duyệt.

**Phạm vi:**
- application form
- application list
- submit application
- approval queue
- approve/reject
- audit log cơ bản

**Kết quả cuối sprint:**
- tạo 1 hồ sơ vay và duyệt được

### Sprint 3 — Loan Booking & Schedule
**Mục tiêu:** biến hồ sơ đã duyệt thành khoản vay.

**Phạm vi:**
- create loan account
- disbursement
- repayment schedule generator
- loan detail screen

**Kết quả cuối sprint:**
- có loan account active
- có lịch trả nợ

### Sprint 4 — Payment & Overdue
**Mục tiêu:** khách trả tiền, hệ thống cập nhật dư nợ.

**Phạm vi:**
- payment entry
- allocation logic
- payment history
- overdue calculation
- overdue dashboard

**Kết quả cuối sprint:**
- thu được 1 kỳ
- thấy khoản vay quá hạn

### Sprint 5 — Reports & Demo Polish
**Mục tiêu:** đủ đẹp để demo/bán.

**Phạm vi:**
- dashboard tổng quan
- due today
- overdue buckets
- report cơ bản
- seed demo data
- polish UI

**Kết quả cuối sprint:**
- demo flow 15 phút hoàn chỉnh

---

## 7. Deliverables

### Deliverable 1 — Functional Spec
Mỗi chức năng cần mô tả:
- mục tiêu
- actor
- input/output
- business rule
- trạng thái
- exception

### Deliverable 2 — Data Model
Các bảng chính:
- `users`
- `customers`
- `customer_documents`
- `loan_products`
- `loan_applications`
- `application_decisions`
- `loans`
- `loan_disbursements`
- `repayment_schedules`
- `payments`
- `loan_transactions`
- `collection_notes`
- `audit_logs`

### Deliverable 3 — API Spec
Ví dụ endpoint:
- `POST /customers`
- `POST /loan-products`
- `POST /applications`
- `POST /applications/{id}/submit`
- `POST /applications/{id}/approve`
- `POST /loans/{id}/disburse`
- `GET /loans/{id}/schedule`
- `POST /loans/{id}/payments`
- `GET /overdue`

### Deliverable 4 — UI Screens
- login
- dashboard
- customer
- product
- application
- approval
- loan detail
- payment
- overdue dashboard

---

## 8. Acceptance criteria theo chức năng

### Customer
- tạo khách hàng thành công
- không trùng định danh chính
- xem được lịch sử khoản vay

### Product
- tạo sản phẩm với rule cơ bản
- validate dữ liệu đầu vào

### Application
- chọn được customer và product
- tính preview installment
- submit thành công

### Approval
- approver mới duyệt được
- reject phải có reason

### Loan Account
- approved application mới tạo loan được
- sinh mã khoản vay duy nhất

### Schedule
- sinh đúng số kỳ
- mỗi kỳ có due date, principal, interest, balance

### Payment
- ghi nhận payment
- cập nhật remaining balance
- lưu payment history

### Overdue
- khoản vay chưa thanh toán sau due date phải overdue
- hiển thị đúng bucket

---

## 9. Demo flow map theo chức năng
Demo bán hàng nên bám sequence:

**Customer -> Application -> Approval -> Disbursement -> Schedule -> Payment -> Overdue Dashboard**

Đây là flow dễ hiểu nhất với khách hàng nghiệp vụ.

---

## 10. Planning nhân sự tối thiểu

### Nếu 1 người làm
Ưu tiên:
- backend logic
- 1 admin UI đơn giản
- bỏ notification/document preview giai đoạn đầu

### Nếu 2–3 người
- 1 backend
- 1 frontend
- 1 BA/QA kiêm PM

### Nếu 4–5 người
- 1 backend lead
- 1 backend
- 1 frontend
- 1 QA
- 1 PM/BA

---

## 11. Phiên bản nên chốt trước

### Demo version v1
Chỉ cần:
- customer
- product
- application
- approval
- disbursement
- schedule
- payment
- overdue dashboard

**Đây là bản đủ để demo và chào bán.**

### Version v2
Thêm:
- penalty
- notifications
- report export
- document management

### Version v3
Thêm:
- restructure
- accounting export
- mobile collector
- customer portal

---

## 12. Functional planning table

| Function | Priority | Actor | Input | Output | Rule | API | Screen | Sprint |
|---|---|---|---|---|---|---|---|---|
| Authentication | P0 | All Users | username, password | access token, role | user active mới login được | `/auth/login` | Login | 1 |
| Customer Management | P0 | Loan Officer | customer info, ID doc | customer profile | không trùng CCCD/sđt | `/customers` | Customer List/Detail | 1 |
| Loan Product | P0 | Admin | amount range, tenor, rate | product config | min <= max | `/loan-products` | Product Screen | 1 |
| Loan Application | P0 | Loan Officer | customer, product, amount, tenor | application | phải có customer + product hợp lệ | `/applications` | Application Form | 2 |
| Approval Workflow | P0 | Approver | application id, decision, remark | approved/rejected status | reject phải có reason | `/applications/{id}/approve` | Approval Queue | 2 |
| Loan Account Creation | P0 | System/Officer | approved application | loan account | chỉ application approved mới tạo loan | `/loans` | Loan Detail | 3 |
| Disbursement | P0 | Loan Officer | loan id, amount, date | disbursement record | không vượt approved amount | `/loans/{id}/disburse` | Loan Detail | 3 |
| Repayment Schedule | P0 | System | amount, tenor, rate | installment list | đủ số kỳ, có gốc/lãi | `/loans/{id}/schedule` | Schedule Tab | 3 |
| Payment Posting | P0 | Collector | loan id, payment amount, date | payment record, balance update | phân bổ theo waterfall | `/loans/{id}/payments` | Payment Screen | 4 |
| Overdue Dashboard | P0 | Collector/Manager | due schedules, payments | overdue list, bucket | quá due date chưa trả thì overdue | `/overdue` | Overdue Dashboard | 4 |
| Reports | P1 | Manager | portfolio data | dashboard/report | số liệu tổng hợp theo ngày | `/reports/*` | Dashboard | 5 |
| Notifications | P1 | System | due dates, overdue events | SMS/email reminder | template theo event | `/notifications/*` | Config / Logs | 5+ |

---

## 13. Kết luận
Trục sống của sản phẩm là:

**Customer -> Product -> Application -> Approval -> Loan -> Schedule -> Payment -> Overdue**

Làm xong trục này thì sản phẩm:
- demo được
- bán được
- mở rộng được

