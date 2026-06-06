---
phase: 4
title: "IFRS 9 ECL Proper Implementation"
status: pending
priority: P2
effort: "4d"
dependencies: [1]
---

# Phase 4: IFRS 9 ECL — Proper Implementation

## Overview

`RiskModelService.cs` hiện dùng PD hardcoded ("Simple mock PD model") — không đủ cho BCTC kiểm toán hoặc NHNN inspection. Cần triển khai IFRS 9 Stage classification và ECL đúng chuẩn:
- Stage 1: 12-month ECL (no significant credit deterioration)
- Stage 2: Lifetime ECL (significant credit deterioration, SICR)
- Stage 3: Lifetime ECL (credit-impaired / NPL)

**Không cần ML model phức tạp.** Cần PD/LGD/EAD có thể calibrate được và Stage classification rule-based rõ ràng.

## Requirements

**Functional:**
- Stage classification logic theo IFRS 9 (DPD-based + qualitative flags)
- PD table configurable (stored in DB, not hardcoded)
- LGD with proper collateral haircut table
- EAD = Outstanding principal + accrued interest
- ECL = PD × LGD × EAD (12-month for Stage 1, lifetime for Stage 2/3)
- Lifetime PD = marginal PD curves per remaining tenor
- Monthly ECL calculation + Stage movement history
- Portfolio ECL summary by Stage

**Non-functional:**
- ECL calculation idempotent per month/year
- Audit trail for Stage changes
- Batch completes <5 min for 50K loans

## Architecture

```
IFrs9StageRule (config entity)
  ├── StageNumber: 1 | 2 | 3
  ├── MinDpd, MaxDpd
  ├── QualitativeFlags: json (Restructured=true → Stage2, WriteOff → Stage3)
  └── Priority

PdCurve (config entity)
  ├── CreditScoreBand: e.g. "700-799"
  ├── RemainingTenorMonths
  └── Pd12Month, PdLifetime

LgdMatrix (config entity)
  ├── CollateralType: Unsecured | RealEstate | Vehicle | Cash
  ├── HaircutRate (e.g. 0.30 for RealEstate = 30% haircut on collateral value)
  └── BaseRecoveryRate

EclRecord (entity)
  ├── LoanId, AsOfDate (month/year)
  ├── Stage: 1 | 2 | 3
  ├── PreviousStage (for movement tracking)
  ├── Ead, Pd12m, PdLifetime, Lgd
  ├── Ecl12m, EclLifetime, EclRecognized
  └── JournalEntryId (provision entry)

EclBatchRecord (entity)
  ├── RunDate, Month, Year
  ├── TotalLoans, Stage1Count, Stage2Count, Stage3Count
  └── TotalEcl12m, TotalEclLifetime, Status

Ifrs9Service
  ├── ClassifyStageAsync(loan) → Stage
  ├── CalculateEclAsync(loan, asOfDate) → EclRecord
  ├── RunMonthlyEclBatchAsync(month, year)
  └── GetPortfolioEclSummaryAsync(asOfDate)
```

**Stage classification:**
```
Stage 3: DPD > 90 OR Status=WrittenOff OR Status=NPL
Stage 2: DPD 31-90 OR Restructured OR CreditScore deteriorated by > 50 pts
Stage 1: All others
```

**ECL formula:**
```
EAD = OutstandingPrincipal + AccruedInterest
LGD = 1 - RecoveryRate
RecoveryRate = Min(AdjustedCollateralValue / EAD, 1.0)
AdjustedCollateralValue = Sum(collateral.Value * (1 - haircut))

Stage 1: ECL = EAD * PD_12m * LGD
Stage 2/3: ECL = Sum over remaining tenor of [EAD_t * MarginalPD_t * LGD * DiscountFactor_t]
```

## Related Code Files

**Create:**
- `src/LoanFlow.Domain/Entities/PdCurve.cs`
- `src/LoanFlow.Domain/Entities/LgdMatrix.cs`
- `src/LoanFlow.Domain/Entities/Ifrs9StageRule.cs`
- `src/LoanFlow.Domain/Entities/EclRecord.cs`
- `src/LoanFlow.Domain/Entities/EclBatchRecord.cs`
- `src/LoanFlow.Application/Services/Ifrs9Service.cs`
- `src/LoanFlow.API/Controllers/Ifrs9Controller.cs`

**Modify (replace mock):**
- `src/LoanFlow.Application/Services/RiskModelService.cs` — replace hardcoded PD with PdCurve lookup, add Stage awareness
- `src/LoanFlow.Infrastructure/Data/LoanFlowDbContext.cs`
- DB seed: populate `PdCurve`, `LgdMatrix`, `Ifrs9StageRule` with initial calibrated values
- EF migration required

## Implementation Steps

1. **Config tables:** Create and seed `PdCurve` (7 credit score bands × tenor buckets), `LgdMatrix` (4 collateral types), `Ifrs9StageRule` (3 rules).

2. **Stage classifier:** `Ifrs9Service.ClassifyStageAsync` — evaluate rules in priority order, return Stage 1/2/3. Persist stage to `EclRecord`.

3. **EAD:** `EAD = loan.OutstandingAmount + Sum(unpaid accrued interest from AccrualRecords)`. Add accrued interest tracking if not already present.

4. **LGD:** Look up `LgdMatrix` by collateral type. `RecoveryRate = Sum(col.Value * (1 - haircut)) / EAD`. `LGD = 1 - Min(RecoveryRate, 1)`.

5. **PD:** Look up `PdCurve` by credit score band and remaining tenor. For Stage 1: use `PD_12m`. For Stage 2/3: use marginal PD curve to project lifetime PD.

6. **ECL recognition:** `EclRecognized = Stage1 ? Ecl12m : EclLifetime`. Delta vs prior month → post journal (Debit: ECL Expense / Credit: ECL Allowance). Add GL codes `5002 ECL Expense`, `1306 ECL Allowance`.

7. **Monthly batch:** `RunMonthlyEclBatchAsync` — stream loans in pages of 500, calculate ECL per loan, post net delta journal, save `EclBatchRecord`.

8. **Portfolio summary:** Group by Stage, sum EAD/ECL, compute coverage ratio.

9. **API:**
   - `POST /ifrs9/run-batch?month=&year=` — trigger batch
   - `GET /ifrs9/portfolio-summary?asOfDate=` — portfolio ECL view
   - `GET /ifrs9/loan/{id}/ecl-history` — loan-level Stage/ECL timeline
   - `GET /ifrs9/config/pd-curves` — view/update PD table
   - `GET /ifrs9/config/lgd-matrix` — view/update LGD table

## Success Criteria

- [ ] Stage 1/2/3 classified correctly for all test scenarios (DPD, restructured, NPL)
- [ ] ECL 12m vs lifetime correctly selected by Stage
- [ ] LGD haircut applied correctly per collateral type
- [ ] PD loaded from DB, not hardcoded
- [ ] Monthly batch processes 10K loans without timeout
- [ ] ECL journal entries balance
- [ ] Stage movement history tracked (for IFRS 9 audit trail)
- [ ] Portfolio summary matches sum of individual ECL records

## Risk Assessment

- **PD calibration:** Initial PD values need to be calibrated against actual portfolio. Seed with conservative estimates; document that recalibration is needed after 12 months of data.
- **Discount rate:** IFRS 9 lifetime ECL requires discounting at EIR. Simplified approach: use loan interest rate as discount rate. Document this assumption.
- **Batch performance:** 50K loans × complex calculation. Use `IAsyncEnumerable` + chunked SaveChanges (500 per chunk).
