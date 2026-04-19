# Production Release P0 Backlog

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Chuyển toàn bộ task P0 trước production thành backlog theo dõi thực thi.

## 1. Cách dùng

- `Status`: `Todo` | `In Progress` | `Blocked` | `Done`
- `ETA`: ngày mục tiêu hoàn thành
- `Assignee`: người hoặc team chịu trách nhiệm chính
- `Dependency`: task hoặc quyết định cần xong trước
- `Evidence`: PR, test result, UAT note, runbook, hoặc tài liệu xác nhận

## 2. P0 Backlog

| Task ID | Workstream | Task | Status | ETA | Assignee | Dependency | Evidence | Release Impact |
|---|---|---|---|---|---|---|---|---|
| PRD-BLOCK-001 | WS1 | Sửa `Reconciliation` để rerun idempotent | Done | TBD | Backend | none | PR + test rerun | Block |
| PRD-BLOCK-002 | WS1 | Sửa logic `broken promise` theo từng promise record | Done | TBD | Backend | none | PR + unit test | Block |
| PRD-BLOCK-003 | WS1 | Sửa `LoanService.GetByIdAsync()` để không trả stale late fee | Done | TBD | Backend | none | PR + service test | Block |
| PRD-BLOCK-004 | WS1 | Chốt và đồng bộ behavior `businessDate` cho job/manual run | Todo | TBD | BA + Backend | BA decision | Decision note + test/UAT | Block |
| PRD-IDEMP-001 | WS2 | Audit toàn bộ financial command bắt buộc idempotent | Done | TBD | Backend | none | endpoint inventory | Block |
| PRD-IDEMP-002 | WS2 | Áp dụng `Idempotency-Key` cho `POST /loans/{id}/early-settle` | Done | TBD | Backend | PRD-IDEMP-001 | PR + duplicate request test | Block |
| PRD-IDEMP-003 | WS2 | Áp dụng `Idempotency-Key` cho `POST /suspense/{id}/apply` nếu action có thể retry | Done | TBD | Backend | PRD-IDEMP-001 | PR + duplicate request test | Block |
| PRD-CFG-001 | WS3 | Validate `Jwt:Issuer` và `Jwt:Audience` lúc startup | Done | TBD | Backend | none | PR + startup validation note | Block |
| PRD-CFG-002 | WS3 | Validate `Cors:AllowedOrigins` cho production | Done | TBD | Backend + DevOps | env definition | PR + config checklist | Block |
| PRD-CFG-003 | WS3 | Review secret loading path cho production | Done | TBD | DevOps | none | config design note | Block |
| PRD-CFG-004 | WS3 | Bỏ fallback secret không an toàn khỏi runtime path | Done | TBD | DevOps + Backend | PRD-CFG-003 | compose/config diff | Block |
| PRD-OBS-001 | WS4 | Thêm `traceId` vào standard error response | Done | TBD | Backend | none | PR + sample error response | Block |
| PRD-OBS-002 | WS4 | Thêm timestamp và code chuẩn cho lỗi hệ thống | Done | TBD | Backend | PRD-OBS-001 | PR + API contract note | Block |
| PRD-OBS-003 | WS4 | Thêm correlation/request id vào logging pipeline | Done | TBD | Backend + DevOps | none | PR + log sample | Block |
| PRD-ADM-001 | WS5 | Tạo API xem session active theo user dành cho admin | Done | TBD | Backend | none | PR + API doc | Block |
| PRD-ADM-002 | WS5 | Tạo API force logout theo session hoặc theo user | Done | TBD | Backend | PRD-ADM-001 | PR + test | Block |
| PRD-ADM-003 | WS5 | Tạo API lock/unlock account | Done | TBD | Backend | none | PR + API doc | Block |
| PRD-ADM-004 | WS5 | Tạo module user admin cơ bản: create/deactivate/change role | Done | TBD | Backend + Frontend | none | PR + UI/API demo | Block |
| PRD-OPS-001 | WS6 | Viết runbook backup database | Todo | TBD | DevOps | none | runbook file | Block |
| PRD-OPS-002 | WS6 | Viết runbook restore database | Todo | TBD | DevOps | PRD-OPS-001 | runbook file | Block |
| PRD-OPS-003 | WS6 | Thực hiện restore test trên non-prod | Todo | TBD | DevOps | PRD-OPS-002 | restore test evidence | Block |
| PRD-TST-001 | WS7 | Bổ sung test cho payment allocation | Todo | TBD | Backend | none | test result | Block |
| PRD-TST-002 | WS7 | Bổ sung test cho disbursement + idempotency | Todo | TBD | Backend | PRD-IDEMP-001 | test result | Block |
| PRD-TST-003 | WS7 | Bổ sung test cho early settlement | Todo | TBD | Backend | PRD-IDEMP-002 | test result | Block |
| PRD-TST-004 | WS7 | Bổ sung test cho overdue / late fee / business date | Todo | TBD | Backend | PRD-BLOCK-004 | test result | Block |
| PRD-TST-005 | WS7 | Bổ sung test cho reconciliation rerun | Todo | TBD | Backend | PRD-BLOCK-001 | test result | Block |

## 3. Recommended Execution Order

### Track A - Core logic

1. `PRD-BLOCK-001`
2. `PRD-BLOCK-002`
3. `PRD-BLOCK-003`
4. `PRD-BLOCK-004`

### Track B - Financial safety

1. `PRD-IDEMP-001`
2. `PRD-IDEMP-002`
3. `PRD-IDEMP-003`

### Track C - Runtime and ops

1. `PRD-CFG-001`
2. `PRD-CFG-002`
3. `PRD-CFG-003`
4. `PRD-CFG-004`
5. `PRD-OBS-001`
6. `PRD-OBS-002`
7. `PRD-OBS-003`

### Track D - Admin control

1. `PRD-ADM-001`
2. `PRD-ADM-002`
3. `PRD-ADM-003`
4. `PRD-ADM-004`

### Track E - Verification and runbook

1. `PRD-OPS-001`
2. `PRD-OPS-002`
3. `PRD-OPS-003`
4. `PRD-TST-001`
5. `PRD-TST-002`
6. `PRD-TST-003`
7. `PRD-TST-004`
8. `PRD-TST-005`

## 4. Suggested Weekly Plan

### Week 1

- Chốt BA decision cho `businessDate`
- Sửa toàn bộ WS1
- Hoàn thành inventory idempotency
- Bổ sung test tương ứng cho WS1

### Week 2

- Hoàn thành WS2
- Hoàn thành startup validation
- Hoàn thành error response + traceId + request id
- Bổ sung test WS2

### Week 3

- Hoàn thành admin session/user operations
- Hoàn thành backup/restore runbook
- Chạy restore test non-prod

### Week 4

- Chạy regression + smoke test
- Chốt UAT evidence
- Review backlog còn mở
- Go/No-Go meeting

## 5. Go / No-Go Rule

`No-Go` nếu còn bất kỳ task nào dưới đây chưa `Done`:

- bất kỳ task `PRD-BLOCK-*`
- bất kỳ task `PRD-IDEMP-*`
- bất kỳ task `PRD-CFG-*`
- `PRD-OBS-001`
- `PRD-OBS-003`
- `PRD-ADM-002`
- `PRD-ADM-003`
- `PRD-OPS-003`
- `PRD-TST-005`

## 6. Immediate Next Update Needed

Trước khi dùng backlog này để chạy thật, cần cập nhật ngay:

1. `ETA` cụ thể cho từng task
2. `Assignee` thật theo người/team
3. link `Evidence` khi bắt đầu có PR hoặc tài liệu
4. `Status` theo tiến độ thực tế
