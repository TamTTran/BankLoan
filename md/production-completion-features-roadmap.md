# Production Completion Features Roadmap

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Xác định nhóm tính năng nên triển khai sau khi đóng P0 release blocker, nhằm đưa hệ thống từ trạng thái “đủ release production” sang “vận hành production hoàn thiện hơn”.

## 1. Mục tiêu

Roadmap này tập trung vào:

- hoàn thiện workflow vận hành thực tế
- giảm phụ thuộc thao tác thủ công sau go-live
- mở rộng chiều sâu lending theo mức hợp lý

Không bao gồm:

- P0 blocker trước production release
- các module bank-grade/compliance sâu chưa cần ngay

## 2. Nguyên tắc ưu tiên

Ưu tiên theo thứ tự:

1. Tăng khả năng sửa sai giao dịch mà không can thiệp DB tay.
2. Tăng hiệu quả thu hồi nợ và vận hành hằng ngày.
3. Tăng khả năng theo dõi, báo cáo, và ra quyết định.
4. Chỉ mở rộng chiều sâu lending sau khi operational layer đã ổn.

## 3. Nhóm tính năng nên làm tiếp

## F1. Reversal / Adjustment Flow Completion

Priority: High
Suggested phase: Wave 1
Owner gợi ý: Backend + BA

### Hiện trạng

- Đã có payment reversal 2 bước request/approve/reject.
- Chưa thấy correction flow đầy đủ cho disbursement.
- Chưa có manual adjustment workflow rõ ràng cho phí, outstanding, hoặc correction đặc biệt.

### Cần bổ sung

- disbursement correction / reversal policy
- manual fee adjustment với approval
- chuẩn hóa audit trail cho correction action
- exception note / reason code chuẩn cho reversal và adjustment

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-001 | Review toàn bộ correction scenario | decision doc cho payment/disbursement/fee adjustment | H |
| PRD-COMP-002 | Bổ sung disbursement correction flow nếu business yêu cầu | API + service + audit | H |
| PRD-COMP-003 | Bổ sung manual adjustment flow có approval | API + audit + UI tối thiểu | H |
| PRD-COMP-004 | Chuẩn hóa reason code / note template cho reversal-adjustment | enum + validation + docs | M |

### Giá trị

- Giảm sửa DB tay.
- Giảm rủi ro khi transaction sai ngoài production.

## F2. Collections Workflow Completion

Priority: High
Suggested phase: Wave 1
Owner gợi ý: Backend + Frontend + BA

### Hiện trạng

- Đã có assign collector.
- Đã có promise-to-pay.
- Đã có collection action history.
- Đã có summary và productivity report cơ bản.

### Cần bổ sung

- overdue action timeline rõ hơn trên UI
- escalation path theo DPD/bucket
- dunning strategy theo phân khúc
- collector workload queue tốt hơn
- SLA follow-up rule

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-101 | Chốt collection states và escalation path theo bucket | BA rule doc | H |
| PRD-COMP-102 | Bổ sung overdue timeline view | UI hiển thị hành trình thu hồi | H |
| PRD-COMP-103 | Bổ sung collector queue theo priority/SLA | queue có sorting/filter/action | H |
| PRD-COMP-104 | Thêm follow-up SLA breach marker | API + UI cảnh báo follow-up trễ | M |
| PRD-COMP-105 | Xây dunning strategy tối thiểu theo bucket | config/rule doc + implementation | M |

### Giá trị

- Collector làm việc có quy trình hơn.
- Manager nhìn được hiệu quả và backlog xử lý.

## F3. Operational Reporting Expansion

Priority: High
Suggested phase: Wave 1-2
Owner gợi ý: Backend + Frontend + Manager/BA

### Hiện trạng

- Đã có portfolio report.
- Đã có overdue movement.
- Đã có collection productivity.
- Đã có approval queue report.

### Cần bổ sung

- transaction exception report
- reconciliation exception dashboard
- export CSV/Excel cho báo cáo chính
- filter theo officer / date / bucket / collector

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-201 | Xác định top báo cáo vận hành cần dùng thật | report priority list | H |
| PRD-COMP-202 | Bổ sung reconciliation exception report | API + UI report | H |
| PRD-COMP-203 | Bổ sung transaction exception report | API + UI report | H |
| PRD-COMP-204 | Thêm export CSV/Excel cho report chính | export action | M |
| PRD-COMP-205 | Bổ sung bộ filter vận hành cho report | filter contract + UI | M |

### Giá trị

- Hỗ trợ manager/ops theo dõi danh mục tốt hơn.
- Giảm phụ thuộc query DB thủ công.

## F4. Overpayment / Advance Payment Maturity

Priority: Medium
Suggested phase: Wave 2
Owner gợi ý: Backend + BA

### Hiện trạng

- Đã có suspense transaction cơ bản.
- Đã có apply suspense.

### Cần bổ sung

- rule rõ cho overpayment carry-forward
- advance payment handling
- unidentified payment bucket / unmatched payment flow
- refund flow nếu cần

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-301 | Chốt business rule cho overpayment vs advance payment | BA decision doc | H |
| PRD-COMP-302 | Bổ sung carry-forward logic nếu business yêu cầu | service + tests | M |
| PRD-COMP-303 | Thiết kế unmatched payment flow | design doc + API scope | M |
| PRD-COMP-304 | Xem xét refund workflow cho suspense dư | rule + API nếu cần | M |

### Giá trị

- Giảm lệch outstanding.
- Hạn chế thao tác thủ công với payment edge cases.

## F5. Notification / Reminder Completion

Priority: Medium
Suggested phase: Wave 2
Owner gợi ý: Backend + Frontend + Ops

### Hiện trạng

- Đã có notification base.
- Đã có nhắc due/overdue ở mức service cơ bản.

### Cần bổ sung

- approval pending reminder
- disbursement / settlement notification
- template management
- delivery status / retry / failure log

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-401 | Chốt event matrix cho notification | event-to-template matrix | H |
| PRD-COMP-402 | Bổ sung approval pending notification | API/job/template | M |
| PRD-COMP-403 | Bổ sung settlement/disbursement notification | API/job/template | M |
| PRD-COMP-404 | Thêm delivery log và retry policy | log + status + retry rule | M |
| PRD-COMP-405 | Thêm UI xem notification log hoặc failed items | UI tối thiểu | L |

### Giá trị

- Tăng hiệu quả vận hành.
- Giảm bỏ sót việc và giảm trễ hạn.

## F6. Customer Exposure / Limit Control

Priority: Medium
Suggested phase: Wave 2
Owner gợi ý: Backend + BA + Credit

### Hiện trạng

- Chưa thấy module exposure control rõ ràng theo customer.

### Cần bổ sung

- tổng dư nợ theo customer
- max exposure rule
- approval override nếu vượt ngưỡng
- cảnh báo ở application / approval stage

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-501 | Chốt exposure policy theo customer | BA/Credit rule doc | H |
| PRD-COMP-502 | Tính current customer exposure tại application/approval | service rule + API | M |
| PRD-COMP-503 | Thêm warning/override flow khi vượt limit | validation + approval log | M |
| PRD-COMP-504 | Bổ sung report exposure theo customer | report/API | L |

### Giá trị

- Kiểm soát rủi ro tập trung dư nợ.
- Tăng chiều sâu phê duyệt tín dụng.

## F7. Collateral Lifecycle Expansion

Priority: Medium-Low
Suggested phase: Wave 3
Owner gợi ý: Backend + BA

### Hiện trạng

- Đã có collateral create/update và allocate to loan.
- Chưa có lifecycle sau allocation.

### Cần bổ sung

- collateral revaluation
- lien/release tracking
- liquidation process
- recovery proceeds allocation

### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-COMP-601 | Chốt collateral lifecycle target scope | scope doc | M |
| PRD-COMP-602 | Bổ sung release tracking | status flow + audit | M |
| PRD-COMP-603 | Bổ sung revaluation history | entity + API + UI | L |
| PRD-COMP-604 | Thiết kế liquidation / recovery allocation | design doc | L |

### Giá trị

- Phù hợp hơn với secured lending.
- Tăng khả năng quản trị tài sản bảo đảm.

## F8. Loan Lifecycle Depth

Priority: Low for current phase
Suggested phase: Wave 3-4
Owner gợi ý: BA + Backend

### Scope ứng viên

- restructure
- reschedule
- extension
- refinance
- top-up / renewal
- write-off / recovery

### Ghi chú

Đây là nhóm mở rộng chiều sâu sản phẩm lending, không nên làm trước khi:

- P0 release blocker đã đóng
- collections, reporting, reconciliation đã ổn
- volume thực tế đã tạo đủ nhu cầu

## 4. Recommended Build Sequence

### Wave 1

- F1 Reversal / Adjustment Flow Completion
- F2 Collections Workflow Completion
- F3 Operational Reporting Expansion

### Wave 2

- F4 Overpayment / Advance Payment Maturity
- F5 Notification / Reminder Completion
- F6 Customer Exposure / Limit Control

### Wave 3

- F7 Collateral Lifecycle Expansion
- F8 Loan Lifecycle Depth

## 5. Delivery Logic

### Nên làm ngay sau P0

- F1
- F2
- F3

Lý do:

- tác động trực tiếp đến vận hành sau go-live
- ít “ảo” hơn các module sâu nhưng mang lại hiệu quả thực tế nhanh

### Nên làm khi production đã ổn định vài tuần

- F4
- F5
- F6

Lý do:

- xử lý edge-case và tối ưu vận hành
- cần dữ liệu usage thực tế để chốt rule tốt hơn

### Nên làm khi muốn mở rộng lending depth

- F7
- F8

## 6. Definition of Done cho nhóm “Completion Features”

Một nhóm tính năng được xem là hoàn thành khi:

- có BA rule hoặc business decision rõ ràng
- có API/service/UI tương ứng nếu thuộc scope người dùng
- có audit log cho action nhạy cảm
- có test hoặc UAT evidence cho happy path và edge case chính
- có tài liệu vận hành hoặc usage note nếu liên quan ops

## 7. Final Recommendation

Nếu chỉ chọn 3 nhóm để làm tiếp ngay sau P0, nên chọn:

1. Reversal / Adjustment Flow Completion
2. Collections Workflow Completion
3. Operational Reporting Expansion

Đây là 3 nhóm mang lại hiệu quả vận hành rõ nhất, giúp hệ thống đi từ “đủ chạy production” sang “dùng production đỡ thủ công hơn”.
