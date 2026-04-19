# Production Release Must-Have Plan

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Chốt danh sách hạng mục bắt buộc trước production release, kèm action task cụ thể để giao việc và theo dõi thực thi.

## 1. Mục tiêu

Tài liệu này chỉ tập trung vào nhóm việc:

- chặn lỗi tài chính và dữ liệu trước go-live
- giảm rủi ro vận hành production
- hoàn thiện control tối thiểu để release an toàn

Không bao gồm các tính năng nice-to-have sau go-live.

## 2. Nguyên tắc release

Chỉ được xem là production-ready khi thỏa cả 3 điều kiện:

1. Không còn known blocker ở flow tiền và batch production.
2. Có đủ control để vận hành, điều tra, rollback vận hành, và xử lý sự cố.
3. Có kiểm chứng tối thiểu bằng test hoặc UAT có log kết quả.

## 3. Release Gate

### Gate 1 - Financial Safety

- Payment, disbursement, settlement không ghi trùng.
- Reconciliation rerun không tạo dữ liệu sai hoặc duplicate exception.
- Business date, overdue, late fee chạy đúng theo rule production đã chốt.

### Gate 2 - Operational Control

- Có thể truy vết lỗi theo trace/request.
- Có thể quản trị session/user ở mức vận hành.
- Có backup/restore checklist và runbook.

### Gate 3 - Verification

- Có test cho critical flow.
- Có checklist smoke test trước release.
- Có danh sách known issue còn lại nếu release có điều kiện.

## 4. Must-Have Workstreams

### WS1. Fix các blocker nghiệp vụ hiện tại

Status: Block release
Owner gợi ý: Backend

#### Mục tiêu

Loại bỏ các lỗi logic đã xác định trong review hiện tại.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-BLOCK-001 | Sửa `Reconciliation` để rerun idempotent | không tạo duplicate `ReconciliationException` cho cùng business date và cùng mismatch | P0 |
| PRD-BLOCK-002 | Sửa logic `broken promise` theo từng promise record | promise được đánh dấu `Kept/Broken` đúng khi loan có nhiều payment | P0 |
| PRD-BLOCK-003 | Sửa `LoanService.GetByIdAsync()` để read không trả stale late fee | response loan detail nhất quán sau khi tính late fee | P0 |
| PRD-BLOCK-004 | Review lại default business date cho job/manual run theo BA production rule | tài liệu + code + API behavior thống nhất | P0 |

#### Acceptance criteria

- Re-run reconciliation cùng ngày không sinh duplicate open exception.
- Case một loan có nhiều payment sau promise date được đánh dấu đúng.
- Loan detail trả late fee đúng ngay trong response đầu tiên.
- Manual job run và background job dùng đúng business date đã BA chốt.

#### Verification

- Unit test cho reconciliation rerun.
- Unit test cho broken promise.
- Integration test hoặc service test cho loan detail late fee.
- UAT script cho EOD / overdue / reconciliation rerun.

## WS2. Hoàn thiện idempotency cho financial commands

Status: Block release
Owner gợi ý: Backend

#### Mục tiêu

Ngăn duplicate posting do retry, timeout, hoặc user submit nhiều lần.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-IDEMP-001 | Audit toàn bộ financial command hiện có | danh sách endpoint bắt buộc idempotent | P0 |
| PRD-IDEMP-002 | Áp dụng `Idempotency-Key` cho `POST /loans/{id}/early-settle` | endpoint không tạo settlement trùng | P0 |
| PRD-IDEMP-003 | Áp dụng `Idempotency-Key` cho `POST /suspense/{id}/apply` nếu action có thể retry | không apply suspense trùng | P0 |
| PRD-IDEMP-004 | Chuẩn hóa response khi request key bị duplicate/in-progress | contract API rõ ràng cho FE/Ops | P1 |
| PRD-IDEMP-005 | Document policy dùng `Idempotency-Key` cho client | tài liệu API + ví dụ request | P1 |

#### Acceptance criteria

- Các command tài chính nhạy cảm đều có cơ chế idempotency.
- Cùng request lặp lại không tạo thêm payment/disbursement/settlement.
- FE hoặc API consumer biết khi nào phải gửi `Idempotency-Key`.

#### Verification

- Test duplicate request cho payment, disbursement, early settlement, suspense apply.

## WS3. Startup validation và cấu hình production

Status: Block release
Owner gợi ý: Backend + DevOps

#### Mục tiêu

Fail fast khi config production thiếu hoặc sai.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-CFG-001 | Validate `Jwt:Issuer` và `Jwt:Audience` khi startup | app không boot nếu thiếu | P0 |
| PRD-CFG-002 | Validate `Cors:AllowedOrigins` ở production | app fail fast nếu để rỗng hoặc invalid | P0 |
| PRD-CFG-003 | Review secret loading path cho production | tài liệu nguồn secret chuẩn | P0 |
| PRD-CFG-004 | Bỏ mọi fallback secret không an toàn khỏi compose/runtime path | không có default secret dùng được thật | P0 |
| PRD-CFG-005 | Viết checklist config bắt buộc trước deploy | file checklist release | P1 |

#### Acceptance criteria

- Production không thể start với config thiếu.
- Không còn known fallback secret nguy hiểm.
- DevOps có checklist config rõ ràng trước release.

## WS4. Error handling và observability tối thiểu

Status: Block release
Owner gợi ý: Backend + DevOps

#### Mục tiêu

Cho phép điều tra lỗi nhanh khi production incident xảy ra.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-OBS-001 | Thêm `traceId` vào standard error response | lỗi API có thể map sang log | P0 |
| PRD-OBS-002 | Thêm timestamp và code chuẩn cho lỗi hệ thống | contract lỗi nhất quán | P0 |
| PRD-OBS-003 | Thêm correlation/request id vào logging pipeline | log request end-to-end | P0 |
| PRD-OBS-004 | Chuẩn hóa structured logging cho background jobs | log EOD/overdue/reconciliation có thể query | P1 |
| PRD-OBS-005 | Xác định metrics/alert tối thiểu | danh sách alert cho health, job failure, 5xx spike | P1 |

#### Acceptance criteria

- Mọi lỗi 5xx có `traceId`.
- Có thể tra log theo request hoặc theo job run.
- Có danh sách alert tối thiểu cho production.

## WS5. Session administration và user/role administration

Status: Block release
Owner gợi ý: Backend + Frontend

#### Mục tiêu

Có đủ control cho admin/security/operator xử lý sự cố user và session.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-ADM-001 | Tạo API xem session active theo user dành cho admin | admin tra được session user | P0 |
| PRD-ADM-002 | Tạo API force logout theo session hoặc theo user | revoke session vận hành | P0 |
| PRD-ADM-003 | Tạo API lock/unlock account | xử lý incident bảo mật | P0 |
| PRD-ADM-004 | Tạo module user admin cơ bản: create/deactivate/change role | quản trị user production | P0 |
| PRD-ADM-005 | Thêm UI tối thiểu cho admin operations | màn hình quản trị cơ bản | P1 |
| PRD-ADM-006 | Chốt password reset policy cho vận hành | runbook + API/flow rõ ràng | P1 |

#### Acceptance criteria

- Admin có thể khóa user, mở khóa, revoke session.
- Có thể tạo và vô hiệu hóa user mà không can thiệp DB tay.
- Đổi role có audit log.

## WS6. Backup, restore, retention

Status: Block release
Owner gợi ý: DevOps + Backend

#### Mục tiêu

Không chỉ chạy được mà còn khôi phục được khi có sự cố.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-OPS-001 | Viết runbook backup database | file hướng dẫn backup chuẩn | P0 |
| PRD-OPS-002 | Viết runbook restore database | file hướng dẫn restore chuẩn | P0 |
| PRD-OPS-003 | Thực hiện restore test trên môi trường non-prod | biên bản restore test | P0 |
| PRD-OPS-004 | Xác định retention policy cho audit/session/log | policy document | P1 |
| PRD-OPS-005 | Nếu cần, thêm cleanup job cho dữ liệu hết hạn | task kỹ thuật + config | P1 |

#### Acceptance criteria

- Có backup procedure rõ ràng.
- Có restore test được thực hiện ít nhất 1 lần trước go-live.
- Có retention policy được chốt.

## WS7. Test coverage và release verification

Status: Block release
Owner gợi ý: Backend + QA/BA

#### Mục tiêu

Giảm regression ở flow tiền và batch production.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-TST-001 | Bổ sung test cho payment allocation | test case principal/interest/late fee/overpayment | P0 |
| PRD-TST-002 | Bổ sung test cho disbursement + idempotency | test duplicate disburse | P0 |
| PRD-TST-003 | Bổ sung test cho early settlement | test amount, fee waive, duplicate request | P0 |
| PRD-TST-004 | Bổ sung test cho overdue / late fee / business date | test cuối tuần, holiday, cutoff | P0 |
| PRD-TST-005 | Bổ sung test cho reconciliation rerun | test không duplicate exception | P0 |
| PRD-TST-006 | Tạo smoke test checklist trước release | checklist manual/API smoke test | P1 |
| PRD-TST-007 | Tạo UAT evidence cho BA sign-off | file kết quả test và known issue | P1 |

#### Acceptance criteria

- Critical flow có automated test tối thiểu.
- Có smoke checklist trước release.
- Có UAT evidence hoặc sign-off rõ ràng.

## WS8. Governance cho action nhạy cảm

Status: Strongly recommended trước production, block nếu scope vận hành yêu cầu
Owner gợi ý: BA + Backend

#### Mục tiêu

Tránh một người tự thực hiện action tài chính nhạy cảm mà không có control.

#### Action tasks

| Task ID | Task | Output cần có | Ưu tiên |
|---|---|---|---|
| PRD-GOV-001 | Review các action cần maker-checker | danh sách action nhạy cảm | P0 |
| PRD-GOV-002 | Chốt rule cho waive late fee, reversal, manual adjustment | BA decision document | P0 |
| PRD-GOV-003 | Nếu bắt buộc, thêm approval flow cho action nhạy cảm | API + audit + UI tối thiểu | P1 |

#### Acceptance criteria

- Có quyết định rõ action nào cần approval riêng.
- Nếu production policy yêu cầu, action nhạy cảm không còn chạy one-step.

## 5. Delivery Plan

### Phase A - Release Blockers First

- WS1 Fix blocker nghiệp vụ hiện tại
- WS2 Idempotency cho financial commands
- WS3 Startup validation và config production
- WS4 Error handling và observability tối thiểu
- WS7 Test coverage cho critical flow

Target outcome:

- hệ thống không còn known blocker ở flow tiền
- deploy không chạy với config lỗi
- có trace để điều tra incident

### Phase B - Operational Readiness

- WS5 Session admin và user/role admin
- WS6 Backup / restore / retention

Target outcome:

- admin vận hành được production mà không sửa DB tay
- có khả năng phục hồi khi xảy ra sự cố

### Phase C - Governance Hardening

- WS8 Governance cho action nhạy cảm

Target outcome:

- control tốt hơn cho waive/reversal/manual adjustment

## 6. Suggested Owners

| Workstream | Primary Owner | Supporting |
|---|---|---|
| WS1 | Backend | BA, QA |
| WS2 | Backend | Frontend |
| WS3 | Backend | DevOps |
| WS4 | Backend | DevOps |
| WS5 | Backend | Frontend, Security/Admin |
| WS6 | DevOps | Backend |
| WS7 | Backend | QA, BA |
| WS8 | BA | Backend, QA |

## 7. Definition of Done for Production Release

Chỉ chốt release khi:

- tất cả task P0 của WS1, WS2, WS3, WS4, WS5, WS6, WS7 hoàn thành
- có evidence test hoặc UAT cho các flow tiền
- không còn release blocker mở
- có checklist deploy và rollback
- có sign-off từ BA hoặc owner nghiệp vụ cho business date / EOD / reconciliation behavior

## 8. Open Decisions cần chốt ngay

Các quyết định dưới đây phải được chốt bằng văn bản trước khi release:

1. `businessDate` mặc định của manual run có phải là ngày làm việc kế tiếp hay không
2. action nào bắt buộc maker-checker
3. production có cần accounting/journal export ngay từ phase này hay để sau
4. retention policy cho audit/session/log là bao lâu
5. ai là owner vận hành session admin và restore procedure

## 9. Recommended Next Actions trong tuần này

1. Chốt WS1 và WS2 trước.
2. Song song hoàn thiện WS3 và WS4 để tránh release xong mới thiếu trace/config.
3. Tạo test cho các lỗi vừa sửa trong WS7 ngay trong cùng PR.
4. Chốt quyết định BA ở mục 8 trước khi đóng scope release.
