---
phase: 1
title: "Regulatory Compliance (Provisioning + CIC + Tax)"
status: pending
priority: P1
effort: "5d"
dependencies: []
---

# Phase 1: Regulatory Compliance — Provisioning + CIC + Tax

## Overview

3 nghĩa vụ pháp lý bắt buộc chưa được triển khai đầy đủ:
- **Trích lập dự phòng** theo Thông tư 11/2021/TT-NHNN (5 nhóm nợ, tỷ lệ 0/5/20/50/100%)
- **Báo cáo CIC** định kỳ (monthly submission file to PCB/CIC)
- **Khấu trừ thuế TNCN** trên thu nhập lãi vay (5% trên lãi vay cá nhân > 10tr/năm)

## Requirements

**Functional:**
- Tính và ghi nhận dự phòng hàng tháng dựa trên nhóm nợ hiện tại của từng khoản vay
- Tự động đảo dự phòng khi khoản vay được trả/tất toán/cải thiện nhóm
- Xuất file CIC theo định dạng PCB chuẩn (CSV/XML)
- Tính và khấu trừ thuế TNCN trên lãi thu được, sinh chứng từ khấu trừ

**Non-functional:**
- Provisioning batch chạy cuối tháng (tích hợp vào EOD/AccrualJob)
- CIC export <30s cho 50K khoản vay
- Thuế khấu trừ chính xác đến đồng

## Architecture

```
ProvisioningService
  ├── CalculateMonthlyProvisionAsync(month, year)
  │   ├── Load all active loans with ClassificationSnapshot
  │   ├── Map DebtGroup → ProvisionRate (TT11 table)
  │   ├── EAD = OutstandingPrincipal - CollateralValue (after haircut)
  │   ├── ProvisionAmount = EAD * Rate
  │   └── Delta vs existing provision → post JournalEntry
  └── ReverseProvisionAsync(loanId)
      └── Reverse remaining provision on loan close/recovery

CicReportingService
  ├── GenerateMonthlyReportAsync(month, year) → CicReportDto
  ├── ValidateCicData() → List<ValidationError>
  └── ExportToCicFormat() → byte[] (PCB CSV format)

WithholdingTaxService
  ├── CalculateTaxOnPaymentAsync(paymentId) → TaxAmount
  ├── RecordTaxWithholdingAsync(paymentId, taxAmount)
  └── GenerateTaxCertificateAsync(customerId, year)
```

**Debt Group → Provision Rate mapping (TT11/2021):**
| Nhóm | Tên | DPD | Tỷ lệ dự phòng |
|------|-----|-----|----------------|
| 1 | Tiêu chuẩn | 0 | 0% |
| 2 | Cần chú ý | 1–10 | 5% |
| 3 | Dưới tiêu chuẩn | 11–30 | 20% |
| 4 | Nghi ngờ | 31–90 | 50% |
| 5 | Có khả năng mất vốn | >90 | 100% |

## Related Code Files

**Create:**
- `src/LoanFlow.Domain/Entities/ProvisionRecord.cs`
- `src/LoanFlow.Domain/Entities/WithholdingTaxRecord.cs`
- `src/LoanFlow.Domain/Entities/CicSubmissionLog.cs`
- `src/LoanFlow.Application/Services/ProvisioningService.cs`
- `src/LoanFlow.Application/Services/CicReportingService.cs`
- `src/LoanFlow.Application/Services/WithholdingTaxService.cs`
- `src/LoanFlow.API/Controllers/ProvisioningController.cs`
- `src/LoanFlow.API/Controllers/CicReportingController.cs`

**Modify:**
- `src/LoanFlow.Application/Services/AccrualJobService.cs` — trigger monthly provisioning
- `src/LoanFlow.Application/Services/ClassificationJobService.cs` — feed debt group to provisioning
- `src/LoanFlow.Application/Services/LoanPaymentService.cs` — trigger tax withholding on payment
- `src/LoanFlow.Infrastructure/Data/LoanFlowDbContext.cs` — add new DbSets
- EF migration required

## Implementation Steps

1. **Domain entities:** Create `ProvisionRecord` (LoanId, DebtGroup, EAD, ProvisionRate, ProvisionAmount, Month, Year, JournalEntryId), `WithholdingTaxRecord`, `CicSubmissionLog`

2. **Provisioning calculation:**
   - `ProvisioningService.CalculateMonthlyProvisionAsync`: load `ClassificationSnapshot` for each loan, apply TT11 rate table, compute delta vs last month's provision, post journal (Debit: Provision Expense / Credit: Provision for Loan Losses), save `ProvisionRecord`
   - On loan close/recovery: `ReverseProvisionAsync` — reverse outstanding provision to income

3. **Provision GL accounts:** Add to seed: `5001 Provision Expense`, `1305 Loan Loss Reserve`

4. **CIC report:** Map customer + loan fields to PCB data format (customer ID, loan account, debt group, outstanding, overdue days). Add `CicSubmissionLog` with status/file hash.

5. **Tax withholding:** On each interest payment component in `LoanPaymentService.RecordPaymentAsync`, calculate 5% tax if applicable. Create `WithholdingTaxRecord`. Add tax line to journal (Debit: Interest Income / Credit: Tax Payable). Deduct from net interest paid to customer.

6. **API endpoints:**
   - `POST /provisioning/run?month=6&year=2026` — trigger batch
   - `GET /provisioning/report?month=6&year=2026` — view provision summary
   - `POST /cic/export?month=6&year=2026` — generate CIC file download
   - `GET /tax/certificates/{customerId}?year=2026` — download tax cert PDF

7. **AccrualJobService integration:** Add provisioning step to month-end EOD run

## Success Criteria

- [ ] Provisioning batch correctly computes TT11 rates for all 5 debt groups
- [ ] Provision journal entries balance (debit = credit)
- [ ] Provision reversal fires correctly on loan close
- [ ] CIC export file validates against PCB schema
- [ ] Tax withholding deducted from correct payment components
- [ ] Tax certificate PDF generated correctly
- [ ] All new GL codes seeded and mapped

## Risk Assessment

- **SBV schema changes:** CIC format changes periodically — design CIC mapping as configuration, not hardcode
- **Backdated provisioning:** If debt group retroactively changes, provision must be restated — add `AsOfDate` to records
- **Tax threshold tracking:** Annual cumulative interest per customer required for threshold check — add `AnnualInterestAccrual` on Customer

## Security Considerations

- CIC file contains PII — encrypt at rest, audit access
- Tax certificate endpoint requires customer-owner permission check
