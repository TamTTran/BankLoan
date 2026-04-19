# Banking Upgrade Plan v2

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Điều chỉnh lại kế hoạch nâng cấp theo baseline thực tế của codebase hiện tại, tách rõ giữa `Production Lending`, `Advanced Lending Controls`, và `Bank-Grade / Compliance`.

---

## 1. Executive View

Codebase hiện tại **không còn là MVP thuần**, nhưng cũng **chưa phải banking platform**.

Đánh giá thực tế hơn:

- `Loan Origination + Servicing`: đã có phần lớn
- `Production Lending`: đang ở mức partial
- `Bank-Grade / Compliance`: phần lớn còn thiếu

Nói cách khác:

- hệ thống đã đủ nền cho một loan management platform có chiều sâu hơn MVP
- nhưng chưa đủ control để gọi là nền tảng nghiệp vụ ngân hàng hoàn chỉnh

---

## 2. Baseline Thực Tế

Thay vì dùng toàn bộ dấu `✅`, baseline nên chia thành 3 mức:

- `Available`: đã có capability rõ ràng trong code
- `Basic / Partial`: đã có bản đầu tiên nhưng chưa hoàn thiện hoặc chưa production-grade
- `Missing`: chưa có hoặc mới chỉ có placeholder

### 2.1 Capability Matrix

| Nhóm | Trạng thái thực tế | Ghi chú |
|---|---|---|
| Authentication + JWT login/refresh/logout | Available | đã có flow cơ bản |
| User/session admin cho production ops | Basic / Partial | chưa đủ module admin hoàn chỉnh |
| Customer onboarding | Available | có CRUD và assignment cơ bản |
| Loan product setup | Available | có flat, reducing balance, grace, promo |
| Application → approval → create loan | Available | có approval rule và decision flow cơ bản |
| Disbursement | Available | đã có, nhưng correction flow còn thiếu |
| Repayment schedule generation | Available | có flat + reducing balance |
| Payment posting | Available | có allocation cơ bản |
| Late fee logic | Available | có nhưng vẫn cần hardening |
| Early settlement | Basic / Partial | đã có preview + execute, còn rủi ro financial correctness |
| Collateral allocation + LTV validation | Basic / Partial | mới dừng ở allocation và validation |
| Overdue tracking + bucket | Available | đã có daily processing cơ bản |
| Collection workflow | Basic / Partial | đã có assign, action, promise, summary; chưa có escalation strategy hoàn chỉnh |
| Payment reversal | Available | có 2-step request/approve/reject |
| Disbursement correction / reversal | Missing | chưa thấy flow đầy đủ |
| Reconciliation | Basic / Partial | đã có internal reconciliation, chưa đủ external + queue hygiene đầy đủ |
| Suspense / overpayment | Basic / Partial | đã có suspense cơ bản, chưa đủ lifecycle |
| Notification | Basic / Partial | có nền cơ bản, chưa đủ template/log/retry/dunning strategy |
| Reporting | Basic / Partial | có report cơ bản, chưa đủ report vận hành sâu |
| Audit log | Available | có ghi log và query cơ bản |
| Full EOD / banking batch | Basic / Partial | có overdue/EOD cơ bản, chưa có classification/accrual/provisioning |
| Accounting / GL | Missing | chưa có double-entry |
| Accrual engine | Missing | chưa có |
| Loan classification / provisioning / NPL | Missing | chưa có |
| eKYC / CIC integration | Missing | chưa có integration thực |
| Multi-branch control | Missing | chưa có trong runtime flow chính |

### 2.2 Kết luận baseline

Hiện trạng phù hợp hơn với mô tả:

`Production-oriented lending core, but not yet banking-grade`

---

## 3. Target State Tách Thành 3 Stage

## Stage 1 - Production Lending

Mục tiêu:

- chạy production ổn định cho lending operation
- có đủ financial control để sửa sai giao dịch
- có đủ ops workflow để quản trị overdue, correction, reporting

Đây là stage nên làm trước.

## Stage 2 - Advanced Lending Controls

Mục tiêu:

- mở rộng chiều sâu quản lý khoản vay
- kiểm soát nợ xấu, exposure, collateral, restructure
- giảm thao tác thủ công khi volume tăng

## Stage 3 - Bank-Grade / Compliance

Mục tiêu:

- đủ chiều sâu kế toán, provisioning, reporting, compliance
- sẵn sàng cho kiểm toán, regulatory reporting, branch-scale governance

---

## 4. Stage 1 - Production Lending

> Đây là nhóm ưu tiên cao nhất sau khi P0 release blocker đã đóng.

## 4.1 Financial Safety & Correction

### S1-A1. Maker-Checker cho action nhạy cảm

Hiện trạng:

- approval workflow đã có cho application
- reversal có self-approval guard
- nhưng chưa có maker-checker framework generic cho action vận hành nhạy cảm

Cần bổ sung:

- workflow request chung cho fee waive, manual adjustment, disbursement correction, force settlement
- maker tạo request
- checker approve/reject
- action chỉ execute sau approval

### S1-A2. Manual Adjustment Workflow

Hiện trạng:

- chưa có cách chuẩn để chỉnh principal/interest/fee/outstanding ngoài flow thường

Cần bổ sung:

- `LoanAdjustment`
- reason code bắt buộc
- audit trail riêng
- đi qua maker-checker

### S1-A3. Disbursement Correction / Reversal

Hiện trạng:

- payment reversal đã có
- disbursement correction chưa có

Cần bổ sung:

- reverse/correct disbursement theo rule business
- kiểm soát điều kiện được correction
- audit + approval

### S1-A4. Exception Management

Hiện trạng:

- reconciliation exception đã có bản đầu
- nhưng chưa phải exception management layer hoàn chỉnh

Cần bổ sung:

- exception queue sạch và nhất quán
- trạng thái vòng đời exception
- comment / assignment / ownership cho exception

## 4.2 Collections & Recovery Operations

### S1-B1. Collections Workflow Completion

Hiện trạng:

- có assign collector
- có action history
- có promise-to-pay
- có summary và productivity report

Cần bổ sung:

- escalation path theo bucket
- next follow-up SLA
- collector queue theo priority rõ hơn
- dunning strategy tối thiểu

### S1-B2. Overpayment / Suspense Completion

Hiện trạng:

- có suspense transaction cơ bản

Cần bổ sung:

- rule carry-forward
- unmatched payment flow
- refund rule nếu cần
- edge-case reporting

## 4.3 Operations & Reporting

### S1-C1. Operational Reporting Expansion

Hiện trạng:

- có portfolio report
- có overdue movement
- có collection productivity
- có approval queue report

Cần bổ sung:

- transaction exception report
- reconciliation exception dashboard
- export CSV/Excel
- bộ filter thực dụng cho manager/ops

### S1-C2. Full End-of-Day Batch

Hiện trạng:

- đã có overdue processing
- đã có reconciliation
- đã có notification generation cơ bản

Cần bổ sung:

- classification update
- EOD snapshot đầy đủ hơn
- run ledger / job tracking tốt hơn
- operational rerun policy rõ ràng

### S1-C3. Business Calendar nâng cao

Hiện trạng:

- đã có business date, cutoff, holiday config cơ bản

Cần bổ sung:

- holiday master theo năm
- due-date shift rule nếu business yêu cầu
- admin maintenance cho business calendar

## 4.4 Suggested Deliverables for Stage 1

| ID | Deliverable | Priority |
|---|---|---|
| S1-001 | Maker-checker framework | High |
| S1-002 | Manual adjustment flow | High |
| S1-003 | Disbursement correction flow | High |
| S1-004 | Collections workflow completion | High |
| S1-005 | Operational reporting expansion | High |
| S1-006 | Overpayment / suspense completion | Medium |
| S1-007 | Full EOD batch completion | Medium |
| S1-008 | Business calendar enhancement | Medium |

---

## 5. Stage 2 - Advanced Lending Controls

> Làm sau khi Stage 1 ổn định production.

## 5.1 Loan Lifecycle Depth

### S2-A1. Restructure

- đổi tenor/rate/start date
- tạo lại schedule
- audit + maker-checker

### S2-A2. Reschedule

- đổi payment day hoặc due pattern
- không nhất thiết đổi tenor/rate

### S2-A3. Write-Off & Recovery

- trạng thái `WrittenOff`
- write-off flow có approval
- recovery sau write-off

### S2-A4. Top-Up / Renewal

- chỉ nên làm khi business có demand thực

## 5.2 Risk & Credit Control

### S2-B1. Customer Exposure Limit

- tổng dư nợ theo customer
- max exposure
- approval override nếu vượt ngưỡng

### S2-B2. Internal Credit Scoring

- scorecard nội bộ
- decision support cho application

### S2-B3. Collateral Lifecycle Expansion

- release tracking
- revaluation
- liquidation scope xác định sau

## 5.3 Suggested Deliverables for Stage 2

| ID | Deliverable | Priority |
|---|---|---|
| S2-001 | Customer exposure limit | High |
| S2-002 | Loan restructure | Medium |
| S2-003 | Loan reschedule | Medium |
| S2-004 | Write-off & recovery | Medium |
| S2-005 | Collateral lifecycle expansion | Medium |
| S2-006 | Internal credit scoring | Medium-Low |
| S2-007 | Top-up / renewal | Low |

---

## 6. Stage 3 - Bank-Grade / Compliance

> Chỉ làm khi Stage 1 và 2 đã ổn định, và có nhu cầu thật về compliance / finance control sâu.

## 6.1 Accounting Core

### S3-A1. Double-Entry Accounting Engine

- `JournalEntry`
- `JournalEntryLine`
- posting model theo transaction type

### S3-A2. Accrual Engine

- daily accrual
- accrual reversal / recognition flow
- accrued income report

### S3-A3. Accounting Period Close

- accounting period
- posting lock
- month-end close controls

## 6.2 NPL / Provisioning / Compliance

### S3-B1. Loan Classification

- 5 nhóm nợ
- daily classification job

### S3-B2. Provisioning Engine

- provisioning expense
- provision balance
- reversal when quality improves

### S3-B3. NPL Reporting

- NPL ratio
- migration report
- product/officer view

### S3-B4. Regulatory Reporting

- NHNN reporting packs
- CIC periodic report

## 6.3 External Integration

### S3-C1. CIC Integration

- real credit bureau integration

### S3-C2. eKYC / Document Verification

- OCR
- face match
- document status control

### S3-C3. BI / Export Integration

- export API
- BI connector-friendly dataset

## 6.4 Suggested Deliverables for Stage 3

| ID | Deliverable | Priority |
|---|---|---|
| S3-001 | Double-entry accounting engine | Medium |
| S3-002 | Accrual engine | Medium |
| S3-003 | Loan classification | Medium |
| S3-004 | Provisioning engine | Medium |
| S3-005 | NPL reporting | Medium-Low |
| S3-006 | CIC integration | Low |
| S3-007 | eKYC integration | Low |
| S3-008 | Regulatory reporting | Low |
| S3-009 | Multi-branch runtime support | Low-Medium |

---

## 7. What Is Already Strong Enough To Reuse

Các phần hiện tại có thể làm nền cho roadmap mới:

- approval rules và decision flow
- payment reversal base
- reconciliation base
- collection action / promise base
- suspense transaction base
- reporting base
- business calendar / EOD base
- audit log base

Tức là roadmap mới **không bắt đầu từ số 0**.
Nó nên tận dụng các module đã có thay vì thiết kế lại toàn bộ.

---

## 8. Recommended Build Order

### Wave 1

- Maker-checker framework
- Manual adjustment
- Disbursement correction

### Wave 2

- Collections workflow completion
- Operational reporting expansion
- Overpayment / suspense completion

### Wave 3

- Customer exposure limit
- Restructure / reschedule
- Write-off / recovery

### Wave 4

- Accounting engine
- Accrual
- Classification / provisioning

### Wave 5

- CIC / eKYC / regulatory / multi-branch

---

## 9. Key Corrections vs v1

So với bản trước, bản v2 điều chỉnh:

1. Không còn coi toàn bộ current state là `✅`.
2. Tách rõ `Production Lending` khỏi `Bank-Grade`.
3. Đưa `Customer Exposure Limit` lên sớm hơn.
4. Gộp nhóm theo khả năng delivery thực tế thay vì chỉ theo taxonomy nghiệp vụ.
5. Xem `payment reversal`, `reconciliation`, `notification`, `collections` là nền basic/partial, không coi là fully complete.

---

## 10. Final Recommendation

Nếu mục tiêu 6-12 tuần tới là nâng cấp mạnh nhưng vẫn thực tế, nên tập trung:

1. `Maker-checker + adjustment + correction`
2. `Collections workflow completion`
3. `Operational reporting`
4. `Customer exposure + restructure/write-off`

Chưa nên nhảy ngay vào:

- full GL
- accrual
- provisioning
- CIC / eKYC
- regulatory reporting

trừ khi đã có yêu cầu pháp lý hoặc khách hàng enterprise cụ thể.
