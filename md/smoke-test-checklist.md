# Smoke Test Checklist — LoanFlow Lite

**Version:** 1.0  
**Date:** 2026-04-20  
**Purpose:** Kiểm tra nhanh (Smoke Test) các luồng nghiệp vụ cốt lõi trước khi Go-Live Production.  
**Ký duyệt:** BA [ ] | QA [ ] | Tech Lead [ ]

---

## Hướng dẫn sử dụng

- Đánh dấu `[x]` khi kịch bản PASS
- Đánh dấu `[!]` khi kịch bản FAIL + ghi rõ lỗi ở cột "Ghi chú"
- Chỉ được Go-Live khi toàn bộ kịch bản Critical (🔴) đều PASS

---

## 1. Xác thực (Authentication)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 1.1 | Login thành công với tài khoản Admin hợp lệ → nhận JWT | | |
| 1.2 | Login bằng sai password → trả 401 Unauthorized | | |
| 1.3 | Gọi API không có JWT → trả 401 | | |
| 1.4 🔴 | Logout → token bị blacklist → gọi lại trả 401 | | |

---

## 2. Quản lý Khách hàng & Hồ sơ Vay

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 2.1 | Tạo khách hàng mới → lưu thành công | | |
| 2.2 | Tạo hồ sơ vay (Application) cho khách hàng → trạng thái = Pending | | |
| 2.3 🔴 | Duyệt hồ sơ vay → trạng thái = Approved | | |
| 2.4 | Từ chối hồ sơ vay → trạng thái = Rejected | | |

---

## 3. Giải ngân (Disbursement)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 3.1 🔴 | Tạo khoản vay từ hồ sơ đã duyệt → trạng thái = PendingDisbursement | | |
| 3.2 🔴 | Giải ngân → trạng thái = Active, lịch trả nợ xuất hiện đủ kỳ | | |
| 3.3 🔴 | Giải ngân 2 lần với cùng Idempotency-Key → lần 2 không tạo giao dịch mới (Idempotency) | | |
| 3.4 | Giải ngân khoản vay đã Active → trả lỗi | | |

---

## 4. Thu tiền (Payment Recording)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 4.1 🔴 | Thu tiền vừa đủ 1 kỳ → kỳ đó chuyển trạng thái Paid | | |
| 4.2 🔴 | Thu tiền thừa (> tổng dư nợ kỳ) → phần dư vào tài khoản Suspense hoặc Advance | | |
| 4.3 🔴 | Thu tiền 2 lần cùng Idempotency-Key → lần 2 bị chặn | | |
| 4.4 | Thu tiền cho khoản vay Closed → trả lỗi | | |

---

## 5. Xử lý Quá hạn & Phí phạt (Overdue & Late Fee)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 5.1 🔴 | Chạy Job EOD sau ngày đến hạn → kỳ trả nợ chuyển sang Overdue | | |
| 5.2 🔴 | Khoản vay có kỳ Overdue → phí phạt (Late Fee) được sinh ra | | |
| 5.3 | Xem chi tiết khoản vay → Late Fee hiển thị đúng, không hiển thị phí cũ của lần trước | | |

---

## 6. Thu hồi nợ (Collections)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 6.1 | Khoản vay Overdue xuất hiện trong hàng đợi Thu hồi | | |
| 6.2 | Manager phân công Collector cho khoản vay | | |
| 6.3 🔴 | Collector ghi nhận Promise-to-Pay (ngày hứa + số tiền) | | |
| 6.4 🔴 | Job kiểm tra Promise chạy sau ngày hứa: nếu nộp ≥ 98% → Kept, ngược lại → Broken | | |

---

## 7. Tái cơ cấu & Nghiệp vụ nâng cao

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 7.1 🔴 | LoanOfficer gửi yêu cầu Tái cơ cấu (Restructure) → tạo Approval Request, trạng thái = Pending | | |
| 7.2 🔴 | Manager Duyệt (Approve) yêu cầu → Approval Request = Approved | | |
| 7.3 | Manager Từ chối → Approval Request = Rejected | | |
| 7.4 | Gửi yêu cầu Write-off → vào hàng đợi phê duyệt | | |

---

## 8. Đối soát (Reconciliation)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 8.1 🔴 | Chạy đối soát ngày hiện tại → không có lỗi hoặc lỗi nằm đúng trong Exception Queue | | |
| 8.2 🔴 | Chạy lại đối soát lần 2 cùng ngày → không tạo thêm Exception mới (idempotent) | | |
| 8.3 | Vào xem Exception Queue → hiển thị đúng danh sách | | |

---

## 9. Backup & Phục hồi

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 9.1 🔴 | Chạy `.\scripts\backup.ps1` → file `.sql.gz` xuất hiện trong thư mục backup | | |
| 9.2 🔴 | Chạy `.\scripts\restore.ps1 -BackupFile <file>` trên DB test → phục hồi thành công | | |

---

## 10. Phân quyền (Authorization Guards)

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 10.1 🔴 | LoanOfficer gọi API Admin-only (vd: `/api/admin-users`) → 403 Forbidden | | |
| 10.2 🔴 | Collector gọi API Approve → 403 Forbidden | | |
| 10.3 | Menu "⚙️ Hệ thống" ẩn với Role Collector/LoanOfficer | | |
| 10.4 | Menu "✅ Phê Duyệt" ẩn với Role Collector | | |

---

## Kết quả tổng hợp

| Hạng mục | Tổng | PASS | FAIL |
|---|---|---|---|
| Critical 🔴 | 18 | | |
| Normal | 17 | | |
| **Tổng** | **35** | | |

---

## Chữ ký nghiệm thu

| Vai trò | Họ tên | Ngày ký | Chữ ký |
|---|---|---|---|
| BA | | | |
| QA | | | |
| Tech Lead | | | |

> **Điều kiện Go-Live:** Toàn bộ 18 kịch bản Critical (🔴) phải PASS.
