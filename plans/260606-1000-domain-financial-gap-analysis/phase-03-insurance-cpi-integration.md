---
phase: 3
title: "Insurance/CPI Integration"
status: pending
priority: P2
effort: "3d"
dependencies: [2]
---

# Phase 3: Insurance / Credit Protection Insurance (CPI)

## Overview

CPI (Credit Protection Insurance / Bảo hiểm tín dụng) là sản phẩm đi kèm khoản vay, bảo hiểm rủi ro tử vong/tàn tật/mất việc cho khách vay. Đây là nguồn thu phí quan trọng cho TCTD (bancassurance income). Hiện LoanFlow chưa có module này.

Cần triển khai:
- Cấu hình gói bảo hiểm theo sản phẩm vay
- Tính phí bảo hiểm và thu cùng kỳ giải ngân
- Chuyển phí sang công ty bảo hiểm (third-party)
- Quản lý khiếu nại bảo hiểm (claim)

## Requirements

**Functional:**
- Cấu hình `InsuranceProduct`: nhà bảo hiểm, loại bảo hiểm, công thức tính phí
- Gắn `InsuranceProduct` tự nguyện/bắt buộc vào `LoanProduct`
- Tính phí bảo hiểm khi tạo repayment schedule (one-time upfront hoặc monthly)
- Thu phí bảo hiểm khi giải ngân (deduct from disbursed amount hoặc first payment)
- Ghi nhận commission bancassurance cho TCTD
- Theo dõi claim: khách hàng claim, claim status, payoff từ insurer

**Non-functional:**
- Phí tính chính xác, reproducible
- Claim workflow tuân thủ SLA insurer

## Architecture

```
InsuranceProduct (entity)
  ├── InsurerId, InsurerName, ProductCode
  ├── CoverageType: DeathDisability | JobLoss | Comprehensive
  ├── PremiumType: OneTimeUpfront | Monthly
  ├── PremiumRateType: FixedAmount | PercentLoanAmount | PercentOutstanding
  ├── PremiumRate, MaxCoverage
  └── BancassuranceCommissionRate (% of premium to TCTD)

LoanInsurance (entity)
  ├── LoanId, InsuranceProductId
  ├── PolicyNumber, CoverageStartDate, CoverageEndDate
  ├── PremiumAmount, BancassuranceCommissionAmount
  ├── Status: Active | Claimed | Expired | Cancelled
  └── CreatedAt

InsuranceClaim (entity)
  ├── LoanInsuranceId, ClaimDate, ClaimType
  ├── ClaimedAmount, ApprovedAmount
  ├── Status: Submitted | UnderReview | Approved | Rejected | Paid
  └── InsurerReferenceId, SettlementDate

InsuranceService
  ├── CalculatePremiumAsync(loanId, insuranceProductId) → PremiumAmount
  ├── AttachInsuranceToLoanAsync(loanId, insuranceProductId)
  ├── ProcessPremiumCollectionAsync(loanId) — on disbursement
  ├── SubmitClaimAsync(loanInsuranceId, claimRequest)
  └── ProcessClaimSettlementAsync(claimId, settledAmount)
```

**Premium formula:**
```
OneTime + PercentLoanAmount: premium = Round(loanAmount * rate, 0)
Monthly + PercentOutstanding: premium_i = Round(outstanding_i * rate / 12, 0) per installment
```

## Related Code Files

**Create:**
- `src/LoanFlow.Domain/Entities/InsuranceProduct.cs`
- `src/LoanFlow.Domain/Entities/LoanInsurance.cs`
- `src/LoanFlow.Domain/Entities/InsuranceClaim.cs`
- `src/LoanFlow.Application/Services/InsuranceService.cs`
- `src/LoanFlow.API/Controllers/InsuranceController.cs`

**Modify:**
- `src/LoanFlow.Domain/Entities/LoanProduct.cs` — add `InsuranceProducts[]` navigation
- `src/LoanFlow.Application/Services/LoanDisbursementService.cs` — process premium collection on disbursement
- `src/LoanFlow.Application/Services/LoanHelpers.cs` — add monthly premium to schedule if monthly type
- `src/LoanFlow.Application/Services/AccountingService.cs` — add Insurance Premium GL mappings
- EF migration required

## Implementation Steps

1. **Entities:** Create `InsuranceProduct`, `LoanInsurance`, `InsuranceClaim`. Link `LoanProduct` → `InsuranceProducts` (many-to-many via join with `IsRequired` flag).

2. **Premium calculation:**
   - On loan application preview: include insurance premium in total cost disclosure
   - On disbursement: `ProcessPremiumCollectionAsync` — deduct one-time premium from net disbursement amount, create `LoanInsurance` record, post GL entry (Debit: Cash / Credit: Insurance Premium Payable + Bancassurance Commission Income)

3. **GL mappings:** Add `Insurance_Premium_Collection`, `Bancassurance_Commission` transaction types to `GLAccountMapping`

4. **Claim workflow:**
   - Loan Officer submits claim via `POST /insurance/claims`
   - Claim → `Submitted` → Manager review → `UnderReview` → Insurer settles
   - On settlement: `ProcessClaimSettlementAsync` — if loan still outstanding, apply claim settlement as payment (through `LoanPaymentService`), close `LoanInsurance`

5. **Monthly premium (if applicable):** Add `InsurancePremiumDue` field to `RepaymentSchedule`, include in waterfall allocation BEFORE LateFee

6. **API:**
   - `GET/POST /insurance-products` — manage products
   - `POST /loans/{id}/insurance` — attach insurance
   - `GET /loans/{id}/insurance` — view policy
   - `POST /insurance/claims` — submit claim
   - `PUT /insurance/claims/{id}/settle` — record settlement

## Success Criteria

- [ ] Premium calculated correctly for OneTime and Monthly types
- [ ] Premium deducted from disbursement net amount correctly
- [ ] GL entries balance for premium collection and bancassurance commission
- [ ] Claim workflow transitions correctly through states
- [ ] Claim settlement applies as payment against outstanding loan
- [ ] Monthly premium included in repayment schedule

## Risk Assessment

- **Regulatory:** Insurance sale must comply with Circular 98/2020/TT-BTC (bancassurance). Ensure disclosure and consent captured.
- **Cancellation:** If loan is rejected after premium collected → full refund required. Handle in reversal flow.
