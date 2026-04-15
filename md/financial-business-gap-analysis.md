# Financial Business Gap Analysis

Date: 2026-04-16
Project: LoanFlow Lite
Purpose: Đánh giá khoảng cách nghiệp vụ tài chính hiện tại và đề xuất lộ trình triển khai theo 3 phase: MVP, Production, Bank-grade.

## 1. Executive Summary

Kết luận ngắn:

- Hệ thống hiện tại đã có nền tảng cho một loan management MVP.
- Hệ thống chưa đủ chiều sâu nghiệp vụ để vận hành như một production lending platform hoàn chỉnh.
- Hệ thống còn thiếu nhiều capability quan trọng ở các lớp:
  - financial control
  - accounting integration
  - transaction correction
  - batch processing
  - reconciliation
  - collections strategy
  - compliance reporting

Đánh giá tổng quát:

- `MVP`: đã có phần lớn
- `Production-ready lending`: chưa đủ
- `Bank-grade lending`: còn thiếu đáng kể

## 2. What The System Already Has

Hiện tại codebase đã có các nghiệp vụ sau:

- Authentication và role-based authorization
- Customer onboarding cơ bản
- Loan product setup cơ bản
- Loan application creation
- Approval workflow cơ bản
- Loan creation from approved application
- Disbursement
- Repayment schedule generation
- Payment posting
- Overdue identification
- Collection note cơ bản
- Audit log cơ bản
- Late fee logic cơ bản
- Early settlement cơ bản
- Collateral allocation cơ bản

Nhận xét:

- Đây là tập capability phù hợp cho demo, PoC, hoặc pilot nội bộ.
- Đây chưa phải tập capability đủ để kiểm soát rủi ro tài chính vận hành thực tế.

## 3. Business Gap Overview

Các khoảng trống chính nằm ở 8 nhóm:

### 3.1 Accounting and Ledger Control

Thiếu:

- double-entry posting
- chart of accounts integration
- GL mapping
- accrual accounting
- accounting period close

### 3.2 Transaction Safety and Financial Control

Thiếu:

- reversal / void / correction
- maker-checker cho giao dịch tiền
- approval cho waive/adjustment
- idempotency tài chính đầy đủ
- transaction lock / posting cutoff

### 3.3 Payment and Settlement Depth

Thiếu:

- overpayment handling
- advance payment handling
- payment allocation flexibility
- suspense/unidentified payment flow
- settlement adjustment rule

### 3.4 Loan Lifecycle Depth

Thiếu:

- restructure
- reschedule
- extension
- refinance
- top-up / renewal
- write-off / charge-off / recovery

### 3.5 Delinquency and Collection Capability

Thiếu:

- promise-to-pay
- collection outcome tracking
- dunning strategy
- collector workload queue
- escalation path
- collection segmentation

### 3.6 Collateral Lifecycle

Thiếu:

- collateral revaluation
- lien/release tracking
- liquidation process
- recovery proceeds allocation

### 3.7 Reconciliation and External Integration

Thiếu:

- bank statement reconciliation
- payout reconciliation
- payment gateway reconciliation
- import/export operational file

### 3.8 Governance and Reporting

Thiếu:

- regulatory reporting
- provisioning logic
- NPL reporting
- management finance report
- operational exception report

## 4. Phase 1 - MVP

### 4.1 Goal

Mục tiêu của phase MVP:

- Chạy được full flow khoản vay từ application đến payment.
- Hỗ trợ demo, PoC, hoặc vận hành quy mô nhỏ với kiểm soát thủ công nhiều.

### 4.2 Scope of MVP

Những gì nên có trong phase này:

- customer management
- loan product setup
- application intake
- approval workflow
- loan creation
- disbursement
- repayment schedule
- payment posting
- overdue identification
- basic collection notes
- audit log cơ bản

### 4.3 Current Status vs MVP

Đánh giá:

- Hệ thống hiện tại về cơ bản đã đạt mức MVP cho loan management.

Những phần nên bổ sung để MVP chắc hơn:

- health check
- global error handling
- startup config validation
- transactional consistency
- idempotency cho disbursement/payment
- frontend/backend contract cleanup

### 4.4 MVP Risks

Nếu chỉ dừng ở MVP:

- phụ thuộc nhiều vào thao tác thủ công
- khó kiểm soát sai lệch tiền
- khó sửa giao dịch sai
- khó vận hành khi volume tăng

## 5. Phase 2 - Production Lending

### 5.1 Goal

Mục tiêu của phase Production:

- Hệ thống có thể vận hành thực tế trong doanh nghiệp cho khối lượng giao dịch trung bình.
- Giảm phụ thuộc vào kiểm soát thủ công.
- Có đủ control để xử lý sai lệch và đối soát.

### 5.2 Required Capabilities

Những capability cần có trong phase này:

#### Financial transaction control

- transaction idempotency đầy đủ
- payment reversal
- disbursement reversal hoặc correction flow
- manual adjustment với approval
- maker-checker cho action nhạy cảm

#### Payment handling

- partial payment chuẩn
- overpayment handling
- advance payment handling
- unidentified payment / suspense bucket

#### Reconciliation

- payment reconciliation
- disbursement reconciliation
- exception queue cho lệch dữ liệu

#### Collections

- promise-to-pay
- collection result tracking
- collector follow-up workflow
- overdue action timeline

#### Operational governance

- batch end-of-day
- status transition control rõ hơn
- admin session control
- richer audit query

#### Reporting

- portfolio summary
- overdue movement
- collection performance
- transaction exception report

### 5.3 Gap vs Current Codebase

Hiện trạng so với Production:

- thiếu reconciliation
- thiếu reversal/correction
- thiếu payment edge-case handling hoàn chỉnh
- thiếu batch/day-end processing
- thiếu collections workflow thực chiến
- thiếu reporting vận hành

### 5.4 Priority Order for Production

Ưu tiên nên làm:

1. Transaction consistency and idempotency
2. Reversal and adjustment flows
3. Reconciliation
4. Collections workflow
5. Batch processing
6. Operations reporting

## 6. Phase 3 - Bank-Grade Lending

### 6.1 Goal

Mục tiêu của phase Bank-grade:

- Hệ thống đạt mức kiểm soát nghiệp vụ và tài chính sâu hơn.
- Có thể phục vụ tổ chức tài chính có governance cao hơn, nhiều phân hệ hơn, và yêu cầu compliance/reporting mạnh hơn.

### 6.2 Required Capabilities

#### Accounting and ledger

- double-entry accounting
- GL mapping by transaction type
- accrual engine
- fee accrual / interest accrual
- accounting period close

#### Advanced loan lifecycle

- restructure
- reschedule
- extension
- refinance
- top-up
- write-off
- recovery allocation

#### Advanced delinquency

- bucket migration engine
- collection strategy by segment
- NPL classification
- provisioning support

#### Collateral lifecycle

- collateral revaluation
- collateral release
- liquidation
- recovery proceeds distribution

#### Compliance and governance

- segregation of duty
- multi-level approval on sensitive actions
- immutable finance audit trail
- exception management
- regulatory reporting packs

#### Finance reporting

- accrued income reports
- write-off reports
- recovery reports
- branch profitability / product profitability
- vintage / cohort analysis

### 6.3 Gap vs Current Codebase

Hiện trạng so với Bank-grade:

- chưa có accounting core
- chưa có accrual logic
- chưa có write-off/recovery lifecycle
- chưa có provisioning/NPL reporting
- chưa có collateral lifecycle hoàn chỉnh
- chưa có compliance/reporting framework đủ sâu

### 6.4 Priority Order for Bank-grade

Ưu tiên nên làm:

1. Accounting posting model
2. Accrual engine
3. Loan restructure and correction lifecycle
4. NPL / provisioning / recovery
5. Collateral lifecycle
6. Regulatory and management reporting

## 7. Recommended Capability Matrix

### Phase MVP

Nên có:

- onboarding
- approval
- disbursement
- schedule
- payment
- overdue
- collection note
- audit log

Mức độ hiện tại:

- Mostly available

### Phase Production

Nên có:

- idempotent transactions
- reversal / adjustment
- reconciliation
- collection workflow
- batch processing
- operational reporting

Mức độ hiện tại:

- Largely missing

### Phase Bank-grade

Nên có:

- accounting / GL
- accrual
- restructure / write-off / recovery
- provisioning
- collateral lifecycle
- regulatory reporting

Mức độ hiện tại:

- Mostly missing

## 8. Suggested Delivery Roadmap

### Stage A - Complete MVP Hardening

Thực hiện:

- health check
- global error handling
- startup validation
- transaction scope
- idempotency
- audit query

### Stage B - Reach Production Lending

Thực hiện:

- reversal / correction
- reconciliation
- batch jobs
- collection workflow
- operational reporting

### Stage C - Reach Bank-grade Depth

Thực hiện:

- accounting engine
- accrual engine
- restructure lifecycle
- write-off / recovery
- NPL / provisioning
- compliance reporting

## 9. Final Recommendation

Kết luận:

- Nếu mục tiêu là demo hoặc pilot: codebase hiện tại có thể tiếp tục hoàn thiện nhẹ.
- Nếu mục tiêu là vận hành production thật: cần xây thêm phase Production lending trước.
- Nếu mục tiêu là platform tài chính mạnh hoặc bank-grade: cần thêm một roadmap lớn, không chỉ refactor kỹ thuật mà phải mở rộng nghiệp vụ sâu.

Khuyến nghị thực tế:

- Không nên nhảy thẳng từ current state lên bank-grade.
- Nên đi theo thứ tự:
  1. harden MVP
  2. complete production financial controls
  3. expand to bank-grade capabilities

Đây là đường đi có chi phí hợp lý và ít rủi ro nhất.
