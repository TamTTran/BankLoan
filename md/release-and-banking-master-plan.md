# Release and Banking Master Plan

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Single source of truth to assess release readiness, define mandatory production gates, and set the upgrade path toward stronger lending and banking-grade controls.

---

## 1. Executive Summary

Short conclusion:

- The system is now beyond MVP and has a solid `loan origination + servicing` core.
- Several production-control capabilities have been implemented since the previous review.
- The current state is no longer a clear `NO-GO` for all production use.
- The practical position is now `GO WITH CONDITIONS` for controlled production rollout.
- The system is still not yet `full production-ready at scale` and not yet `bank-grade / compliance-heavy`.

Current release view:

- `Demo / UAT / pilot`: ready
- `Controlled production rollout`: acceptable with conditions and active monitoring
- `Broad official production rollout`: not yet recommended
- `Bank-grade operations / compliance-heavy scope`: not yet achieved

---

## 2. Current Baseline

Classification:

- `Available`: clear capability exists in code and is usable
- `Basic / Partial`: capability exists but is not yet fully hardened or complete
- `Missing`: not implemented or only present as concept / placeholder

| Capability Group | Status | Notes |
|---|---|---|
| Authentication + JWT + login/refresh/logout | Available | Core flow exists |
| Session and user admin for production ops | Available | Create user, change role/status, lock/unlock, view/revoke sessions now exist |
| Customer onboarding | Available | CRUD and assignment base available |
| Loan product setup | Available | Flat, reducing balance, grace, promo supported |
| Application -> approval -> create loan | Available | Core approval and decision flow available |
| Disbursement | Available | Core flow exists; correction policy still needs expansion |
| Repayment schedule generation | Available | Available |
| Payment posting | Available | Core allocation exists |
| Late fee | Basic / Partial | Available, but still needs continued hardening and consistency checks |
| Early settlement | Available | Preview + execute available |
| Collateral allocation + LTV validation | Basic / Partial | Core exists, full collateral lifecycle still missing |
| Overdue tracking + bucket | Available | Core processing exists |
| Collection workflow | Basic / Partial | Assignment, action history, promise-to-pay, summary available; escalation still incomplete |
| Payment reversal | Available | Two-step reversal exists |
| Disbursement correction / reversal | Basic / Partial | Direction exists, but not yet a complete operational framework |
| Reconciliation | Basic / Partial | Internal reconciliation exists, rerun behavior still needs closing logic for stale exceptions |
| Suspense / overpayment | Basic / Partial | Base capability exists, lifecycle still incomplete |
| Notification | Basic / Partial | Base notification exists, delivery management still limited |
| Reporting | Basic / Partial | Core reporting exists, operational reporting still needs expansion |
| Audit log | Available | Logging and query base available |
| EOD / batch processing | Basic / Partial | Batch base exists and has improved, but still needs stronger release verification |
| Accounting / GL | Basic / Partial | Journal and balancing base now exist |
| Accrual engine | Basic / Partial | Initial capability exists, not yet fully hardened |
| Loan classification / provisioning / NPL | Basic / Partial | Initial classification/NPL direction exists, not yet fully production-grade |
| CIC / eKYC | Missing | No real external integration yet |
| Multi-branch runtime support | Missing | Not part of main runtime flow yet |
| Advanced lifecycle: restructure / reschedule / write-off / recovery | Basic / Partial | Implemented, but approval/control wiring is not yet complete end-to-end |
| Customer exposure / limit control | Basic / Partial | Exposure capability now exists, but policy enforcement still needs completion |

Baseline conclusion:

`Production-oriented lending platform with materially improved operational controls, but still not fully hardened for broad production release and not yet bank-grade`

---

## 3. What Has Improved Since The Previous Review

The following areas have materially improved:

- `Production ops admin`:
  create user, change role/status, lock/unlock account, query sessions, revoke sessions
- `Error handling and traceability`:
  error response now includes `traceId`, `timestamp`, `errorCode`, and supports correlation id
- `Manual job safety`:
  manual overdue/EOD run now rejects future dates
- `Maker-checker foundation`:
  approval request queue and approve/reject workflow now exist
- `Manual adjustment capability`:
  dedicated service now exists
- `Accounting / GL base`:
  journal balancing and posting base now exist
- `Advanced lending lifecycle`:
  restructure, reschedule, write-off, recovery, and exposure query now exist
- `Verification assets`:
  additional automated test files have been added for key financial and batch flows

Impact on readiness:

- The release position improved from `NO-GO except pilot` to `GO WITH CONDITIONS for controlled production`.
- Several items previously marked `Missing` are now at least `Basic / Partial`.

---

## 4. Release Decision

### Current Decision

- `GO WITH CONDITIONS` for controlled production rollout
- `NO-GO` for broad official production rollout until remaining blockers are closed

### Why It Is Not Full GO Yet

1. `Reconciliation rerun` is still not fully clean when old mismatches disappear.
2. `Maker-checker` exists as framework, but coverage is not yet clearly enforced for all sensitive actions.
3. `Advanced lifecycle` features exist, but approval/control integration is still partial.
4. `Backup / restore` governance still requires operational evidence, not only documents.
5. `Test execution evidence` still needs to be attached and verified for release sign-off.

---

## 5. Release Gates

Release should be considered acceptable only when all 3 gates are satisfied.

### Gate 1 - Financial Safety

- No known open blocker in payment, disbursement, settlement, reconciliation, overdue, and late fee behavior
- Sensitive money-moving commands are protected against duplicate execution
- Batch and `businessDate` behavior follow the BA-approved rule

### Gate 2 - Operational Control

- Admin can manage users and sessions without direct DB edits
- Error and log tracing are sufficient for incident investigation
- Backup/restore runbook exists and restore test evidence is available

### Gate 3 - Verification

- Regression tests exist for critical money flows and EOD-related flows
- Smoke test checklist exists
- BA signs off on sensitive business rules

---

## 6. Must-Have Before Broad Production Release

> These are still the remaining must-have items before broad production go-live.

## 6.1 WS1 - Close Remaining Financial Blockers

### Goal

Close the logic gaps that still affect financial correctness or operational trust.

### Action Tasks

| Task ID | Task | Required Output | Priority |
|---|---|---|---|
| REL-001 | Fix reconciliation rerun so it does not leave stale open exceptions after mismatch is resolved | clean rerun behavior and consistent exception queue | P0 |
| REL-002 | Confirm and harden broken-promise logic against BA rule | correct promise kept/broken behavior | P0 |
| REL-003 | Continue validating late fee read consistency in loan detail and related overdue views | consistent response behavior | P0 |
| REL-004 | Finalize and document the `businessDate` rule for background jobs and manual run | BA sign-off and stable behavior | P0 |

### Acceptance Criteria

- Reconciliation rerun on the same date does not create duplicate mismatches
- Reconciliation exceptions that are no longer valid are no longer shown as open
- Promise logic matches BA-approved commitment rule
- Loan detail and overdue views do not return stale late fee data

## 6.2 WS2 - Complete Sensitive Action Control

### Goal

Ensure sensitive operations are consistently protected and approved.

### Action Tasks

| Task ID | Task | Required Output | Priority |
|---|---|---|---|
| REL-101 | Audit all sensitive actions that require maker-checker | approved action inventory | P0 |
| REL-102 | Extend maker-checker coverage beyond manual adjustment where required | enforced approval flow | P0 |
| REL-103 | Align restructure, reschedule, write-off, and correction flows with approval policy | end-to-end controlled lifecycle | P0 |
| REL-104 | Standardize reason codes and approval evidence for sensitive actions | audit-ready behavior | P1 |

## 6.3 WS3 - Harden Financial Command Safety

### Goal

Prevent duplicate posting and strengthen transaction safety.

### Action Tasks

| Task ID | Task | Required Output | Priority |
|---|---|---|---|
| REL-201 | Review all financial commands that require idempotency | endpoint inventory | P0 |
| REL-202 | Confirm early settlement idempotency with tests | endpoint + test evidence | P0 |
| REL-203 | Confirm suspense apply idempotency and edge-case behavior | endpoint + test evidence | P0 |
| REL-204 | Standardize duplicate/in-progress API response contract | clear API behavior | P1 |
| REL-205 | Review transaction boundaries for money-moving commands | review note + fixes if needed | P0 |

## 6.4 WS4 - Backup / Restore / Release Evidence

### Goal

Move from documentation-only readiness to operationally verified readiness.

### Action Tasks

| Task ID | Task | Required Output | Priority |
|---|---|---|---|
| REL-301 | Finalize backup runbook | approved runbook | P0 |
| REL-302 | Finalize restore runbook | approved runbook | P0 |
| REL-303 | Execute restore test in non-prod and store evidence | restore evidence package | P0 |
| REL-304 | Finalize retention policy for audit/session/log | policy document | P1 |
| REL-305 | Attach release evidence package to go-live decision | sign-off package | P0 |

## 6.5 WS5 - Test And Verification Closure

### Goal

Reduce regression risk in critical financial and batch behavior.

### Action Tasks

| Task ID | Task | Required Output | Priority |
|---|---|---|---|
| REL-401 | Run and record payment allocation tests | automated test evidence | P0 |
| REL-402 | Run and record disbursement and idempotency tests | automated test evidence | P0 |
| REL-403 | Run and record early settlement tests | automated test evidence | P0 |
| REL-404 | Run and record overdue / late fee / business date tests | automated test evidence | P0 |
| REL-405 | Run and record reconciliation rerun tests | automated test evidence | P0 |
| REL-406 | Maintain smoke test checklist | manual checklist | P1 |
| REL-407 | Attach BA and QA sign-off package | release evidence | P1 |

---

## 7. Go / No-Go Rule

`NO-GO` for broad production if any of the following remain open:

- any `P0` item in section 6
- reconciliation rerun still leaves stale open exceptions
- sensitive lifecycle actions are still not consistently covered by approval control
- no restore test evidence exists
- no BA sign-off exists for `businessDate`, reconciliation behavior, and promise rule
- no regression evidence exists for critical financial flows

`GO WITH CONDITIONS` is acceptable only when:

- all known financial blockers are either closed or explicitly accepted with owner and mitigation
- operational runbook and escalation owner are defined
- controlled rollout scope is limited by volume and monitoring

---

## 8. Completion Features After P0

> These are the next completion items after release blockers are closed.

## 8.1 CF1 - Reversal / Adjustment Completion

Current state:

- payment reversal exists
- manual adjustment exists
- approval framework exists

Next improvement:

- expand maker-checker to all required sensitive actions
- complete disbursement correction policy and implementation
- standardize reason codes and reversal/adjustment evidence

## 8.2 CF2 - Collections Workflow Completion

Current state:

- collector assignment, action history, promise-to-pay, and summary exist

Next improvement:

- escalation path by DPD/bucket
- stronger collector queue
- next follow-up SLA
- minimal dunning strategy
- better overdue timeline in UI

## 8.3 CF3 - Operational Reporting Expansion

Current state:

- core reporting exists

Next improvement:

- reconciliation exception dashboard
- transaction exception report
- CSV/Excel export
- richer filtering by date/product/officer/collector/status

## 8.4 CF4 - Overpayment / Advance Payment Maturity

Current state:

- suspense base exists

Next improvement:

- carry-forward rule
- unmatched payment flow
- refund policy if required by business

## 8.5 CF5 - Notification Completion

Current state:

- notification base exists

Next improvement:

- approval pending reminder
- settlement/disbursement notification
- template matrix
- delivery log and retry policy

## 8.6 CF6 - Exposure And Limit Control Completion

Current state:

- exposure capability exists in base form

Next improvement:

- enforce total exposure by customer
- max exposure rules
- override approval when threshold is exceeded

---

## 9. Banking Upgrade Roadmap

> This is the path toward stronger lending and banking-grade capability. It is not the same as the immediate production release checklist.

## Stage 1 - Controlled Production Lending

Goal:

- run production in a controlled and supportable way
- close financial correctness gaps
- avoid routine manual DB intervention

Includes:

- all items in section 6
- high-priority completion features in section 8

## Stage 2 - Advanced Lending Controls

Goal:

- broaden the loan lifecycle
- improve risk, collections, and credit control depth

Main capabilities:

- customer exposure limit enforcement
- restructure
- reschedule
- write-off and recovery
- expanded collateral lifecycle
- stronger internal credit controls

## Stage 3 - Bank-Grade / Compliance

Goal:

- reach stronger accounting, provisioning, compliance, and regulatory depth

Main capabilities:

- stronger double-entry accounting engine
- hardened accrual engine
- full loan classification
- provisioning engine
- NPL reporting maturity
- CIC integration
- eKYC / document verification
- regulatory reporting
- multi-branch runtime support

---

## 10. Suggested Build Order

### Wave 1

- REL-001 to REL-405

### Wave 2

- complete maker-checker coverage
- complete disbursement correction
- complete collections workflow
- expand operational reporting

### Wave 3

- exposure limit enforcement
- restructure / reschedule hardening
- write-off / recovery hardening

### Wave 4

- stronger GL
- accrual hardening
- classification / provisioning / NPL maturity

### Wave 5

- CIC
- eKYC
- regulatory reporting
- multi-branch

---

## 11. Owner Model

| Area | Primary Owner | Supporting |
|---|---|---|
| Financial correctness | Backend lead | BA, QA |
| Sensitive action approval control | Backend lead | BA, QA |
| Idempotency / transaction safety | Backend | QA |
| Startup config / secret / deploy | DevOps | Backend |
| Observability | Backend | DevOps |
| Session/user admin | Backend | Frontend, Security/Admin |
| Backup/restore | DevOps | Backend |
| UAT / business rule sign-off | BA | QA, Backend |
| Banking upgrade roadmap | BA + Tech lead | Backend, Ops |

---

## 12. Open Business Decisions To Finalize

The following decisions must be explicitly finalized before continuing delivery:

1. What is the default `businessDate` for manual run
2. What exact condition marks a promise as `Kept`
3. Which actions are mandatory maker-checker actions
4. What is the rule for overpayment vs advance payment
5. What is the policy and allowed time window for disbursement correction
6. Whether due dates shift on holiday/weekend
7. What retention period applies to audit/session/log
8. Whether stronger GL/accounting is required in the next rollout or later stage

---

## 13. Final Recommendation

If the immediate goal is `release with proper control`:

- focus first on section 6
- do not expand deep banking scope before closing remaining release blockers

If the goal is `move toward stronger banking-aligned operations`:

- close section 6
- then complete section 8
- then continue through section 9 Stage 2 and Stage 3

Practical conclusion:

- the system is now **stronger than the previous release review**
- the system is now **acceptable for controlled production rollout with conditions**
- the system is **not yet ready for broad official production rollout**
- the system is **not yet a complete banking-grade platform**
- the architecture and current implementation are already strong enough to upgrade further without rebuilding from scratch
