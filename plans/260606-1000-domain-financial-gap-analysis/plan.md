---
title: "Domain Financial Gap - Missing Business Logic"
description: "Gap analysis of missing financial domain features in LoanFlow. Covers regulatory compliance obligations, revenue-generating modules, and critical data-integrity bug fixes identified in architecture review."
status: pending
priority: P1
created: 2026-06-06
---

# Domain Financial Gap — Missing Business Logic

## Overview

LoanFlow hiện có nền tảng vận hành tốt (payment waterfall, GL double-entry, approval flow, collections). Tuy nhiên, còn 5 nhóm nghiệp vụ chưa có hoặc chưa đủ để đáp ứng yêu cầu pháp lý và vận hành thực tế của một TCTD/Fintech tại Việt Nam:

1. **Regulatory Compliance** — trích lập dự phòng theo TT11/2021, báo cáo CIC, khấu trừ thuế lãi tiết kiệm
2. **DSA Commission** — quản lý hoa hồng đại lý/môi giới (major revenue channel)
3. **Insurance/CPI** — bảo hiểm khoản vay (significant fee income)
4. **IFRS 9 ECL** — mô hình tổn thất tín dụng kỳ vọng (current model is placeholder)
5. **Portfolio Analytics** — vintage/roll-rate/cohort analysis cho risk management

Phase 6 xử lý 5 bug nghiêm trọng phát hiện trong architecture review (`/ask`).

## Phases

| Phase | Name | Priority | Effort | Status |
|-------|------|----------|--------|--------|
| 1 | [Regulatory Compliance (Provisioning + CIC + Tax)](./phase-01-regulatory-compliance-provisioning-cic-tax.md) | P1 | 5d | Pending |
| 2 | [DSA Commission Management](./phase-02-dsa-commission-management.md) | P1 | 3d | Pending |
| 3 | [Insurance/CPI Integration](./phase-03-insurance-cpi-integration.md) | P2 | 3d | Pending |
| 4 | [IFRS 9 ECL Proper Implementation](./phase-04-ifrs-9-ecl-proper-implementation.md) | P2 | 4d | Pending |
| 5 | [Loan Portfolio Analytics](./phase-05-loan-portfolio-analytics.md) | P2 | 3d | Pending |
| 6 | [Bug Fixes (BUG-001 to BUG-005)](./phase-06-bug-fixes-bug-001-to-bug-005.md) | P1 | 2d | **Completed** |

**Recommended execution order:** Phase 6 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5

Phase 6 (bug fixes) first — BUG-002 (GL balance corruption) is a data-integrity blocker.

## Dependencies

- Phase 4 (IFRS 9) depends on Phase 1 (Provisioning) — share debt classification data
- Phase 5 (Analytics) depends on Phase 1 (Provisioning snapshots) for complete portfolio data
- Phase 3 (Insurance) depends on Phase 2 (DSA) pattern — similar commission/fee structure
