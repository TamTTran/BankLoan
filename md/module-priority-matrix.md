# Module Priority Matrix

Date: 2026-04-16
Project: LoanFlow Lite
Purpose: Xác định thứ tự ưu tiên build module theo business value, operational risk, implementation complexity, và production impact.

## 1. Objective

Tài liệu này dùng để:

- Ưu tiên module nên triển khai tiếp theo
- Cân bằng giữa business value và delivery effort
- Tránh build module "hay" nhưng chưa thật sự cần
- Tập trung vào các capability làm hệ thống đủ dùng cho production trước

## 2. Scoring Model

Mỗi module được đánh giá theo 4 tiêu chí:

- `Business Value`: mức độ tạo giá trị trực tiếp cho vận hành/doanh thu
- `Risk Reduction`: mức độ giảm rủi ro tài chính/vận hành/compliance
- `Implementation Complexity`: độ khó triển khai
- `Production Urgency`: mức độ cần trước khi go-live hoặc scale

Thang điểm:

- 1 = thấp
- 5 = rất cao

Lưu ý:

- `Implementation Complexity` càng cao không có nghĩa là ưu tiên thấp tuyệt đối
- Nếu module có `Risk Reduction` và `Production Urgency` rất cao thì vẫn nên ưu tiên

## 3. Priority Matrix

| Module | Business Value | Risk Reduction | Complexity | Production Urgency | Priority |
|---|---:|---:|---:|---:|---|
| Transaction Idempotency | 5 | 5 | 3 | 5 | P1 |
| Transaction Scope / Consistency | 5 | 5 | 3 | 5 | P1 |
| Audit Log Query | 4 | 4 | 2 | 5 | P1 |
| Reversal / Adjustment Flow | 5 | 5 | 4 | 5 | P1 |
| Reconciliation | 5 | 5 | 4 | 5 | P1 |
| Collections Workflow | 5 | 4 | 3 | 4 | P1 |
| Batch / End-of-Day Processing | 4 | 5 | 4 | 5 | P1 |
| Operational Reporting | 4 | 4 | 3 | 4 | P2 |
| Promise-to-Pay Tracking | 4 | 4 | 2 | 4 | P2 |
| Overpayment / Advance Payment | 4 | 4 | 3 | 4 | P2 |
| Session/Admin Security Control | 3 | 4 | 2 | 4 | P2 |
| Notification / Reminder | 4 | 3 | 3 | 3 | P2 |
| Customer Exposure / Limit Control | 4 | 4 | 4 | 3 | P2 |
| Collateral Lifecycle | 4 | 4 | 4 | 3 | P3 |
| Restructure / Reschedule | 4 | 4 | 5 | 3 | P3 |
| Write-off / Recovery | 4 | 5 | 5 | 3 | P3 |
| Accounting / GL Posting | 5 | 5 | 5 | 3 | P3 |
| Accrual Engine | 4 | 5 | 5 | 3 | P3 |
| Provisioning / NPL | 4 | 5 | 5 | 2 | P4 |
| Regulatory Reporting | 3 | 5 | 5 | 2 | P4 |

## 4. Recommended Priority Bands

### P1 - Build First

Đây là nhóm module nên làm trước vì:

- tác động trực tiếp đến an toàn giao dịch tài chính
- giảm rủi ro production lớn nhất
- là nền để các module sau hoạt động đúng

Các module:

- Transaction Idempotency
- Transaction Scope / Consistency
- Audit Log Query
- Reversal / Adjustment Flow
- Reconciliation
- Collections Workflow
- Batch / End-of-Day Processing

### P2 - Build After P1

Đây là nhóm module nâng vận hành từ MVP lên production thực chiến.

Các module:

- Operational Reporting
- Promise-to-Pay Tracking
- Overpayment / Advance Payment
- Session/Admin Security Control
- Notification / Reminder
- Customer Exposure / Limit Control

### P3 - Expand Financial Depth

Đây là nhóm module làm hệ thống sâu hơn về mặt lending platform.

Các module:

- Collateral Lifecycle
- Restructure / Reschedule
- Write-off / Recovery
- Accounting / GL Posting
- Accrual Engine

### P4 - Bank-grade / Compliance-heavy

Đây là nhóm module chỉ nên làm khi:

- đã ổn định production lending
- đã có scale đủ lớn
- có nhu cầu compliance/reporting sâu

Các module:

- Provisioning / NPL
- Regulatory Reporting

## 5. Detailed Recommendations by Module

### 5.1 Transaction Idempotency

Priority: P1

Lý do:

- Nếu không có idempotency thì payment/disbursement dễ bị ghi trùng
- Đây là rủi ro tài chính trực tiếp

Recommendation:

- áp dụng cho disbursement
- áp dụng cho payment
- áp dụng cho early settlement

### 5.2 Transaction Scope / Consistency

Priority: P1

Lý do:

- Nhiều flow đang có nhiều `SaveChangesAsync`
- Có rủi ro ghi dở dang

Recommendation:

- tất cả command tài chính cần transaction boundary rõ

### 5.3 Audit Log Query

Priority: P1

Lý do:

- Đã có audit log nhưng chưa khai thác được
- Rất quan trọng khi xử lý khiếu nại và incident

Recommendation:

- filter theo actor, action, entity, date range

### 5.4 Reversal / Adjustment Flow

Priority: P1

Lý do:

- Production chắc chắn có giao dịch sai
- Không có reversal thì chỉ có cách sửa DB hoặc workaround

Recommendation:

- payment reversal
- disbursement correction
- fee waive / manual adjustment có approval

### 5.5 Reconciliation

Priority: P1

Lý do:

- Đây là module kiểm soát sai lệch tiền quan trọng nhất sau payment posting

Recommendation:

- đối soát payment
- đối soát disbursement
- exception queue

### 5.6 Collections Workflow

Priority: P1

Lý do:

- Hệ thống đang có overdue và note nhưng chưa đủ workflow thu hồi nợ

Recommendation:

- promise-to-pay
- outcome tracking
- next follow-up date
- collector queue

### 5.7 Batch / End-of-Day Processing

Priority: P1

Lý do:

- Production lending cần chốt ngày
- Không nên phụ thuộc hoàn toàn vào query-time computation

Recommendation:

- overdue aging
- late fee generation
- bucket update
- daily snapshot

### 5.8 Operational Reporting

Priority: P2

Lý do:

- Giúp manager và ops theo dõi danh mục
- Không critical bằng reconciliation nhưng rất cần sau go-live

Recommendation:

- portfolio report
- overdue movement
- collection productivity
- approval queue report

### 5.9 Overpayment / Advance Payment

Priority: P2

Lý do:

- Tình huống này sẽ xuất hiện sớm trong production
- Nếu không có rule rõ sẽ sinh sai lệch outstanding

Recommendation:

- define allocation rules
- suspense or carry-forward handling

### 5.10 Notification / Reminder

Priority: P2

Lý do:

- Tăng hiệu quả vận hành
- Giảm tỷ lệ trễ hạn

Recommendation:

- due reminder
- overdue reminder
- approval pending reminder

### 5.11 Customer Exposure / Limit Control

Priority: P2

Lý do:

- Cần khi số lượng khoản vay trên một khách hàng tăng lên

Recommendation:

- tổng dư nợ theo customer
- max exposure
- approval override nếu vượt hạn mức

### 5.12 Accounting / GL Posting

Priority: P3

Lý do:

- Rất quan trọng nếu muốn tiến tới core lending thực thụ
- Nhưng complexity cao, không nên làm trước khi operational control đã ổn

Recommendation:

- design accounting events trước
- chưa cần full ERP integration ngay

## 6. Suggested Build Sequence

### Wave 1

- Transaction Idempotency
- Transaction Scope / Consistency
- Audit Log Query

### Wave 2

- Reversal / Adjustment Flow
- Reconciliation
- Batch / End-of-Day Processing

### Wave 3

- Collections Workflow
- Promise-to-Pay
- Operational Reporting
- Overpayment / Advance Payment

### Wave 4

- Notification / Reminder
- Customer Exposure / Limit Control
- Session/Admin Security Control

### Wave 5

- Accounting / GL Posting
- Accrual Engine
- Restructure / Reschedule
- Write-off / Recovery

### Wave 6

- Provisioning / NPL
- Regulatory Reporting

## 7. Strategic Recommendation

Nếu mục tiêu là:

### Demo / Pilot

Tập trung:

- UI polish
- bug fix
- MVP hardening

### Production Lending

Tập trung:

- P1 trước
- sau đó P2

### Bank-grade Platform

Tập trung:

- chỉ bắt đầu P3/P4 sau khi P1/P2 đã ổn định

## 8. Final Recommendation

Thứ tự ưu tiên thực tế nên là:

1. transaction safety
2. financial correction
3. reconciliation
4. collections workflow
5. operational reporting
6. advanced lending depth
7. bank-grade compliance

Nếu chỉ chọn 5 module phải build tiếp ngay, tôi recommend:

- Transaction Idempotency
- Transaction Scope / Consistency
- Reversal / Adjustment Flow
- Reconciliation
- Collections Workflow

Đây là nhóm module cho hiệu quả business và risk reduction tốt nhất trong ngắn hạn.
