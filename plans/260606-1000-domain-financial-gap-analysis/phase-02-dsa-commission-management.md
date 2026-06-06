---
phase: 2
title: "DSA Commission Management"
status: pending
priority: P1
effort: "3d"
dependencies: []
---

# Phase 2: DSA Commission Management

## Overview

DSA (Direct Sales Agent) / môi giới tín dụng là kênh phân phối chính cho consumer lending tại Việt Nam. Hiện LoanFlow không có module quản lý đại lý/hoa hồng. Cần triển khai:
- Quản lý danh sách DSA và hợp đồng hoa hồng
- Tính hoa hồng tự động theo từng khoản vay giải ngân
- Thanh toán hoa hồng theo kỳ (monthly/disbursement-trigger)
- Báo cáo hiệu suất DSA

## Requirements

**Functional:**
- CRUD DSA profile (cá nhân/pháp nhân, mã đại lý, hợp đồng, tài khoản ngân hàng)
- Cấu hình commission scheme trên sản phẩm vay: flat fee, % disbursed amount, tiered slab
- Tự động tính commission khi khoản vay chuyển sang Active (disbursed)
- Commission approval flow (maker-checker cho khoản > threshold)
- Thanh toán commission qua NAPAS, track trạng thái
- Commission clawback nếu loan early settle/write-off trong X tháng

**Non-functional:**
- Commission tính trong <1s sau disbursement event
- Report export (Excel) cho kỳ thanh toán

## Architecture

```
DsaAgent (entity)
  ├── Id, AgentCode, FullName, TaxCode, BankAccountNo, BankCode
  ├── Status: Active/Suspended/Terminated
  └── CommissionSchemes[] → CommissionScheme

CommissionScheme (entity)
  ├── ProductId (nullable = all products)
  ├── SchemeType: FlatFee | PercentDisbursed | TieredSlab
  ├── FlatFeeAmount | CommissionRate | TierConfig (JSON)
  ├── ClawbackPeriodMonths
  └── ValidFrom, ValidTo

DsaLoanLink (entity)
  ├── LoanApplicationId, DsaAgentId
  └── LinkedAt, LinkedByUserId

CommissionRecord (entity)
  ├── LoanId, DsaAgentId, SchemeId
  ├── DisbursedAmount, CommissionAmount
  ├── Status: Pending | Approved | Paid | ClawedBack
  ├── PaymentTransactionId (NAPAS ref)
  └── CreatedAt, PaidAt

DsaCommissionService
  ├── CalculateCommissionAsync(loanId) — triggered post-disbursement
  ├── ApproveCommissionAsync(commissionId, userId)
  ├── ProcessPaymentBatchAsync(month, year) — bulk NAPAS transfer
  ├── ClawbackAsync(loanId, reason) — on early settle/write-off
  └── GetDsaPerformanceReportAsync(dsaId, from, to)
```

## Related Code Files

**Create:**
- `src/LoanFlow.Domain/Entities/DsaAgent.cs`
- `src/LoanFlow.Domain/Entities/CommissionScheme.cs`
- `src/LoanFlow.Domain/Entities/DsaLoanLink.cs`
- `src/LoanFlow.Domain/Entities/CommissionRecord.cs`
- `src/LoanFlow.Application/Services/DsaCommissionService.cs`
- `src/LoanFlow.API/Controllers/DsaController.cs`
- `src/LoanFlow.API/Controllers/CommissionController.cs`

**Modify:**
- `src/LoanFlow.Application/Services/LoanDisbursementService.cs` — trigger commission calculation post-disbursement
- `src/LoanFlow.Application/Services/LoanLifecycleService.cs` — trigger clawback on early settle/write-off
- `src/LoanFlow.Application/Services/LoanApplicationService.cs` — link DSA to application on submit
- `src/LoanFlow.Infrastructure/Data/LoanFlowDbContext.cs`
- EF migration required

## Implementation Steps

1. **Domain entities:** Create all entities above. `CommissionScheme.TierConfig` = JSON column for slab tiers `[{minAmount, maxAmount, rate}]`.

2. **Commission calculation logic:**
   ```
   FlatFee: commissionAmount = scheme.FlatFeeAmount
   Percent: commissionAmount = Round(loan.ApprovedAmount * scheme.CommissionRate, 0)
   Tiered:  find tier where DisbursedAmount falls, apply tier.rate
   ```

3. **Disbursement hook:** After `LoanDisbursementService` confirms NAPAS success, call `DsaCommissionService.CalculateCommissionAsync(loanId)`. Persist `CommissionRecord` with status `Pending`.

4. **Approval flow:** Commission > 5,000,000 VND requires Approver sign-off (reuse `ApprovalFlowService` pattern). Below threshold → auto-approve.

5. **Payment batch:** `POST /commissions/pay-batch?month=6&year=2026` — collect all Approved records for month, group by DSA, generate bulk NAPAS transfer. Mark `Paid` on callback.

6. **Clawback:** On early settle or write-off within `ClawbackPeriodMonths`, create `ClawbackRecord`, reverse commission as `ClawedBack`. If already paid → create receivable from DSA.

7. **DSA performance report:** GetDsaPerformanceReportAsync returns {totalLoans, totalDisbursed, totalCommission, avgDPD, nplRate} per DSA for date range. Export Excel.

8. **API:**
   - `GET/POST/PUT /dsa` — CRUD agents
   - `GET/POST /commission-schemes` — manage schemes per product
   - `GET /commissions?dsaId=&status=&month=` — list records
   - `POST /commissions/{id}/approve`
   - `POST /commissions/pay-batch`
   - `GET /dsa/{id}/performance`

## Success Criteria

- [ ] Commission calculated correctly for all 3 scheme types
- [ ] Commission created automatically on NAPAS disbursement confirm
- [ ] Approval flow works for amounts > threshold
- [ ] Clawback correctly reverses commission within clawback period
- [ ] Performance report returns accurate aggregates
- [ ] Payment batch integrates with NAPAS

## Risk Assessment

- **Multiple DSA per loan:** Policy decision — allow split commission? Default: single DSA per loan
- **Scheme versioning:** If rate changes mid-month, use scheme valid at application date, not disbursement date
- **Clawback collection:** If DSA is terminated before clawback recovery — needs manual collection process, flag in UI
