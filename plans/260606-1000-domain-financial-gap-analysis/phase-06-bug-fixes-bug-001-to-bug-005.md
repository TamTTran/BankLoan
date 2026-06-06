---
phase: 6
title: "Bug Fixes (BUG-001 to BUG-005)"
status: completed
priority: P1
effort: "2d"
dependencies: []
---

# Phase 6: Bug Fixes — BUG-001 to BUG-005

## Overview

5 bug nghiêm trọng phát hiện trong architecture review. BUG-002 (GL balance lost-update) là data-integrity blocker — **must fix trước khi prod**. Implement phase này TRƯỚC các phase khác.

## Requirements

- Fix tất cả 5 bug không introduce regression
- Unit tests cho mỗi fix

## Bug Details & Fixes

---

### BUG-001 — Late Fee Double-Count After Partial Payment
**File:** `LateFeeService.cs:53-75`

**Problem:** `calculatedFee = overdueAmount * dailyRate * effectiveDays` dùng `RemainingDueAmount` làm base. Sau partial payment, base thay đổi nhưng delta logic so sánh fee cũ (old base × old days) với fee mới (new base × new days) → sai.

**Fix:**
1. Add `BaseOverdueAmount` field to `LateFee` entity — frozen at grace-period-end
2. In `CalculateLateFeesAsync`: khi tạo `LateFee` mới, set `BaseOverdueAmount = schedule.RemainingDueAmount` at that moment
3. Daily accrual delta = `BaseOverdueAmount * dailyRate * 1` (per day, not cumulative recalc)
4. Update formula: `calculatedFee = existingFee.BaseOverdueAmount * dailyRate * effectiveDays`

**Files:**
- `src/LoanFlow.Domain/Entities/LateFee.cs` — add `BaseOverdueAmount`
- `src/LoanFlow.Application/Services/LateFeeService.cs:53-75` — fix formula
- EF migration for new column

---

### BUG-002 — GL Balance Lost-Update (DATA INTEGRITY — CRITICAL)
**File:** `AccountingService.cs:73-86`

**Problem:** `account.Balance += delta` in EF tracking. Two concurrent payments to same GL account (e.g., Cash 1001) both read same balance → one overwrites other.

**Fix:** Replace in-memory balance update with raw SQL inside transaction:
```csharp
// REPLACE:
account.Balance += lineDto.DebitAmount;
account.Balance -= lineDto.CreditAmount;

// WITH (per line):
var netDelta = lineDto.DebitAmount - lineDto.CreditAmount;
if (account.AccountType == GLAccountType.Asset || account.AccountType == GLAccountType.Expense)
    await _db.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE gl_accounts SET balance = balance + {netDelta}, updated_at = {DateTime.UtcNow} WHERE id = {account.Id}");
else
    await _db.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE gl_accounts SET balance = balance - {netDelta}, updated_at = {DateTime.UtcNow} WHERE id = {account.Id}");
```

**Files:**
- `src/LoanFlow.Application/Services/AccountingService.cs:73-86`

---

### BUG-003 — EarlySettle Reference Equality Fragile
**File:** `LoanPaymentService.cs:466-469`

**Problem:**
```csharp
s == loan.RepaymentSchedules.FirstOrDefault(x => x.DueDate >= request.SettlementDate)
```
Reference equality fails if schedule loaded in different EF context query.

**Fix:** Replace with `InstallmentNo` comparison:
```csharp
var firstUnsettledInstallmentNo = loan.RepaymentSchedules
    .Where(x => x.DueDate >= request.SettlementDate)
    .OrderBy(x => x.InstallmentNo)
    .Select(x => x.InstallmentNo)
    .FirstOrDefault();

// In the foreach:
s.InterestDue = s.InterestPaid = s.InstallmentNo == firstUnsettledInstallmentNo
    ? preview.ProRataInterest
    : 0;
```

**Files:**
- `src/LoanFlow.Application/Services/LoanPaymentService.cs:460-478`

---

### BUG-004 — Stale EF Tracking After Rollback
**File:** `AccountingService.cs` + `LoanPaymentService.cs`

**Problem:** `PostJournalAsync` modifies `account.Balance` in EF tracking but doesn't SaveChanges. On rollback, DB reverts but EF tracking keeps dirty state. Next operation on same DbContext scope writes wrong balance.

**Fix:** After rollback in `LoanPaymentService`, reload affected GL accounts. Since BUG-002 fix moves GL balance to raw SQL, `account.Balance` is no longer in EF tracking → BUG-004 is automatically resolved by BUG-002 fix.

**Dependency:** BUG-004 resolved by BUG-002 fix. No separate change needed.

---

### BUG-005 — SaveChanges Inside Foreach Loop
**File:** `LateFeeService.cs:108`

**Problem:** `await _db.SaveChangesAsync()` inside foreach per schedule to get ID for back-link. N round-trips per payment.

**Fix:** Two-pass approach:
```csharp
// Pass 1: Create all new LateFee + LoanTransaction without SaveChanges per item
var newFees = new List<(LateFee fee, LoanTransaction tx)>();
foreach (var schedule in ...)
{
    var newFee = new LateFee { ... };
    var accrualTx = new LoanTransaction { ... };
    _db.LateFees.Add(newFee);
    _db.LoanTransactions.Add(accrualTx);
    newFees.Add((newFee, accrualTx));
}

// Pass 2: Single SaveChanges — EF populates IDs
await _db.SaveChangesAsync();

// Pass 3: Back-link IDs
foreach (var (fee, tx) in newFees)
    fee.AccrualLoanTransactionId = tx.Id;

// Pass 4: Single SaveChanges for back-links
await _db.SaveChangesAsync();
```

**Files:**
- `src/LoanFlow.Application/Services/LateFeeService.cs:79-135`

## Related Code Files

**Modify:**
- `src/LoanFlow.Domain/Entities/LateFee.cs`
- `src/LoanFlow.Application/Services/LateFeeService.cs`
- `src/LoanFlow.Application/Services/AccountingService.cs`
- `src/LoanFlow.Application/Services/LoanPaymentService.cs`
- EF migration for `LateFee.BaseOverdueAmount`

## Implementation Steps

1. Fix BUG-002 first (GL raw SQL) — unblocks data integrity
2. Fix BUG-001 (late fee base amount) — revenue integrity
3. Fix BUG-003 (EarlySettle InstallmentNo comparison) — silent calculation error
4. Fix BUG-005 (batch SaveChanges) — performance
5. BUG-004 auto-resolved by BUG-002
6. Write unit tests for each fix covering: partial payment → late fee recalculation, concurrent GL update, early settle pro-rata, batch late fee calculation

## Success Criteria

- [ ] BUG-002: Concurrent payment test shows correct GL balance (no lost update)
- [ ] BUG-001: Partial payment followed by late fee recalculation uses frozen BaseOverdueAmount
- [ ] BUG-003: EarlySettle with non-tracked entity correctly applies ProRataInterest to first installment
- [ ] BUG-005: LateFeeService calls SaveChanges exactly 2 times regardless of overdue schedule count
- [ ] All existing tests still pass
- [ ] EF migration applied successfully

## Risk Assessment

- **BUG-002 migration risk:** Raw SQL bypasses EF change tracking. After the fix, `account.Balance` in EF tracking will be stale. Remove any code that reads `account.Balance` from EF after posting (use DB query instead). Audit `AccountingService` for any `account.Balance` reads post-`PostJournalAsync`.
