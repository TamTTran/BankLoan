---
phase: 5
title: "Loan Portfolio Analytics"
status: pending
priority: P2
effort: "3d"
dependencies: [1, 4]
---

# Phase 5: Loan Portfolio Analytics

## Overview

Risk management trưởng thành cần phân tích danh mục nâng cao hơn dashboard hiện tại. Thiếu:
- **Vintage analysis:** default rate theo cohort (tháng giải ngân)
- **Roll-rate matrix:** tỷ lệ chuyển nhóm nợ tháng T→T+1
- **Portfolio aging:** phân bổ DPD bucket theo thời gian
- **Concentration risk:** tập trung theo ngành, địa lý, sản phẩm, DSA

Đây không phải UI cosmetic — đây là công cụ risk manager dùng để điều chỉnh credit policy.

## Requirements

**Functional:**
- Vintage report: cho mỗi tháng giải ngân (vintage), track % NPL theo tuổi khoản vay (months on book)
- Roll-rate matrix: N×N matrix debt group transition M→M+1
- Portfolio aging bucket: DPD 0, 1–30, 31–60, 61–90, 90+ — theo tháng
- Concentration: top-N by product, branch, DSA agent, loan size band
- All reports: filter by date range, export CSV/Excel
- Periodic snapshot (monthly) để trend analysis

**Non-functional:**
- Reports load <3s (pre-computed snapshots, not real-time scan)
- Snapshot job chạy đầu tháng (sau EOD cuối tháng)

## Architecture

```
PortfolioSnapshot (entity)
  ├── AsOfDate (first of month)
  ├── TotalLoans, TotalOutstanding
  ├── Stage1Count/Amount, Stage2Count/Amount, Stage3Count/Amount
  ├── DpdBucket0, DpdBucket1_30, DpdBucket31_60, DpdBucket61_90, DpdBucket90Plus
  └── NplAmount, NplRate

VintageDataPoint (entity)
  ├── VintageMonth (YYYY-MM — tháng giải ngân cohort)
  ├── MonthsOnBook (0, 1, 2, ... N)
  ├── OriginalLoans, OriginalAmount
  ├── DefaultedLoans, DefaultedAmount
  └── CumulativeDefaultRate

RollRateSnapshot (entity)
  ├── AsOfDate
  ├── FromGroup, ToGroup (1..5)
  └── LoanCount, Amount

PortfolioAnalyticsService
  ├── TakeMonthlySnapshotAsync(asOfDate)
  ├── GetVintageReportAsync(fromVintage, toVintage) → VintageTable
  ├── GetRollRateMatrixAsync(month, year) → RollRateMatrix
  ├── GetAgingBucketTrendAsync(fromDate, toDate) → AgingTrend[]
  └── GetConcentrationReportAsync(dimension, asOfDate) → ConcentrationDto
```

**Vintage table structure:**
```
VintageMonth | MOB0 | MOB1 | MOB2 | MOB3 | MOB6 | MOB12 | MOB24
2025-01      | 0%   | 0.5% | 1.2% | 2.1% | 4.5% | 8.2%  | 12.1%
2025-02      | 0%   | 0.3% | 0.9% | ...
```

**Roll-rate matrix:**
```
           → Grp1  Grp2  Grp3  Grp4  Grp5  Closed
Grp1 (current) 85%   8%    3%    1%    0.5%  2.5%
Grp2           15%   60%   12%   5%    2%    6%
...
```

## Related Code Files

**Create:**
- `src/LoanFlow.Domain/Entities/PortfolioSnapshot.cs`
- `src/LoanFlow.Domain/Entities/VintageDataPoint.cs`
- `src/LoanFlow.Domain/Entities/RollRateSnapshot.cs`
- `src/LoanFlow.Application/Services/PortfolioAnalyticsService.cs`
- `src/LoanFlow.API/Controllers/PortfolioAnalyticsController.cs`

**Modify:**
- `src/LoanFlow.Application/Services/AccrualJobService.cs` — add monthly snapshot trigger
- `src/LoanFlow.API/BackgroundJobs/DailyOverdueBackgroundService.cs` — trigger vintage/roll-rate snapshot month-end
- `src/LoanFlow.Infrastructure/Data/LoanFlowDbContext.cs`
- EF migration required

## Implementation Steps

1. **Monthly snapshot job:** On last day of month EOD, `TakeMonthlySnapshotAsync`:
   - Group all loans by DPD bucket → save `PortfolioSnapshot`
   - Group by (FromGroup, ToGroup) vs prior month classification → save `RollRateSnapshot` 
   - For each vintage cohort: count loans where first DPD > 90 by MOB → save `VintageDataPoint`

2. **Vintage calculation:**
   - `VintageMonth` = month of `loan.DisbursementDate`
   - `MonthsOnBook` = months elapsed since DisbursementDate
   - `DefaultedLoans` = loans in that vintage that hit DPD>90 at that MOB or earlier
   - Cumulative default rate = DefaultedLoans / OriginalLoans

3. **Roll-rate matrix:** Join `ClassificationSnapshot` for month T and T-1. Count transitions. Present as percentage matrix.

4. **Aging bucket trend:** Query `PortfolioSnapshot` for date range, return time-series of bucket percentages.

5. **Concentration:** Real-time query (not pre-computed) — group by product/branch/DSA/size-band, return top-20 by outstanding amount.

6. **Backfill:** On first deployment, backfill historical snapshots from existing `ClassificationSnapshot` and `OverdueDailySnapshot` data.

7. **API:**
   - `GET /analytics/vintage?from=2025-01&to=2025-12` — vintage table
   - `GET /analytics/roll-rate?month=5&year=2026` — roll rate matrix
   - `GET /analytics/aging-trend?from=2026-01-01&to=2026-06-01` — aging over time
   - `GET /analytics/concentration?dimension=product|branch|dsa|size` — top-N concentration
   - `POST /analytics/snapshot` — manual trigger snapshot
   - `GET /analytics/export?report=vintage&format=excel` — export

## Success Criteria

- [ ] Monthly snapshot job runs without error
- [ ] Vintage table shows correct cumulative default rates per cohort
- [ ] Roll-rate matrix sums correctly (each row ~100%)
- [ ] Aging bucket trend matches sum of DPD classifications
- [ ] Concentration report returns correct groupings
- [ ] Export (CSV/Excel) works for all report types
- [ ] Historical backfill doesn't duplicate existing snapshots

## Risk Assessment

- **Snapshot idempotency:** Running snapshot twice for same month must be safe — add `UNIQUE(AsOfDate)` constraint on `PortfolioSnapshot`
- **Vintage cold-start:** First 6 months of deployment have thin vintage data — normal, document it
- **Performance:** Concentration is real-time query on all active loans — add index on `BranchId`, `ProductId`, `Status`
