# Banking Upgrade Plan

Date: 2026-04-19
Project: LoanFlow Lite
Purpose: Nâng cấp hệ thống từ loan management MVP trở thành nền tảng lending đủ tiêu chuẩn nghiệp vụ ngân hàng.

---

## 1. Baseline — Hệ thống hiện tại đang ở đâu

Hệ thống hiện tại đã đạt mức **Loan Origination + Servicing MVP**:

| Nhóm | Trạng thái |
|---|---|
| Authentication, JWT, session control | ✅ Đủ production |
| Customer onboarding cơ bản | ✅ Có |
| Loan product setup | ✅ Có (Flat + Reducing Balance, grace period, promo rate) |
| Loan application → approval → create loan | ✅ Có |
| Disbursement + repayment schedule generation | ✅ Có |
| Payment posting với allocation (LateFee → Interest → Principal) | ✅ Có |
| Late fee calculation | ✅ Có |
| Early settlement (preview + execute) | ✅ Có |
| Collateral allocation + LTV validation | ✅ Có |
| Overdue tracking + DPD bucket | ✅ Có |
| Debt collection (assign, action, promise-to-pay) | ✅ Có |
| Transaction reversal | ✅ Có |
| Reconciliation (idempotent rerun) | ✅ Có |
| Suspense transaction (overpayment) | ✅ Có |
| Admin session/user management | ✅ Có |
| Audit log cơ bản | ✅ Có |
| Notification + Email + SMS | ✅ Có (cơ bản) |
| Dashboard + Reporting | ✅ Có (cơ bản) |
| Background job overdue tự động hàng ngày | ✅ Có |

**Thiếu so với nghiệp vụ ngân hàng thực tế:** xem phần 2 → 7 bên dưới.

---

## 2. Nhóm A — Kiểm Soát Tài Chính Sâu Hơn (Financial Control)

> Đây là nhóm ưu tiên cao nhất. Ngân hàng không thể vận hành nếu không có cơ chế sửa sai giao dịch tiền một cách kiểm soát được.

### A1. Maker-Checker cho giao dịch nhạy cảm

**Thiếu gì:**
- Các action như waive fee, manual adjustment, force-close loan, write-off hiện không có cơ chế 2 người phê duyệt.
- Disbursement và settlement chỉ cần 1 user action.

**Cần bổ sung:**
- Bảng `PendingApproval` hoặc `WorkflowRequest` để giữ action chờ duyệt
- Maker tạo request → Checker approve/reject → hệ thống execute
- Áp dụng cho: manual fee waive, force settlement, disbursement adjustment, write-off

**Entities cần thêm:**
```csharp
public class PendingApprovalRequest
{
    public int Id { get; set; }
    public string ActionType { get; set; }   // "FeeWaive" | "WriteOff" | "DisbursementCorrect"
    public string Payload { get; set; }       // JSON chứa parameters
    public int RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string Status { get; set; }        // "Pending" | "Approved" | "Rejected"
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}
```

**API cần thêm:**
- `POST /approval-requests` — maker submit
- `GET /approval-requests?status=Pending` — checker xem queue
- `POST /approval-requests/{id}/approve`
- `POST /approval-requests/{id}/reject`

---

### A2. Manual Adjustment Workflow

**Thiếu gì:**
- Không có cách điều chỉnh outstanding, fee, hoặc interest ngoài flow bình thường.
- Khi data sai (nhập sai số tiền, lỗi hệ thống), hiện tại phải sửa DB tay.

**Cần bổ sung:**
- API `POST /loans/{id}/adjustments` cho phép ghi correction entry
- Adjustment phải đi qua maker-checker (A1)
- Audit trail riêng cho adjustment
- Reason code bắt buộc

**Entities cần thêm:**
```csharp
public class LoanAdjustment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string AdjustmentType { get; set; }  // "Principal" | "Interest" | "LateFee" | "Fee"
    public decimal Amount { get; set; }          // + hoặc -
    public string ReasonCode { get; set; }
    public string? Note { get; set; }
    public int CreatedByUserId { get; set; }
    public int ApprovedByUserId { get; set; }
    public DateTime AppliedAt { get; set; }
}
```

---

### A3. Disbursement Correction / Reversal Policy

**Thiếu gì:**
- Không có flow rõ ràng nếu disbursement sai (sai số tiền, sai ngày, gửi sai tài khoản).
- Reversal service hiện có nhưng chưa bao gồm disbursement.

**Cần bổ sung:**
- Disbursement reversal nếu chưa phát sinh payment (loan vẫn còn `PendingDisbursement` hoặc `Active` ngay sau disbursement)
- Correction flow: reverse disbursement → disbursement lại với số đúng
- Giới hạn thời gian: chỉ cho reverse trong N ngày sau disbursement (business rule)

---

## 3. Nhóm B — Kế Toán & Sổ Cái (Accounting & GL)

> Ngân hàng phải ghi nhận mọi giao dịch tiền vào sổ cái kép (double-entry). Đây là yêu cầu pháp lý và kiểm toán.

### B1. Double-Entry Accounting Engine

**Hiện trạng:** `LoanTransaction` chỉ ghi transaction type + amount — không phải double-entry.

**Cần bổ sung:**
- Bảng `JournalEntry` và `JournalEntryLine` theo mô hình double-entry
- Mỗi giao dịch tài chính phải tạo ít nhất 2 journal lines (Debit / Credit) có tổng = 0
- Chart of Accounts (CoA) tối thiểu cho lending operations

**Entities cần thêm:**
```csharp
public class JournalEntry
{
    public int Id { get; set; }
    public string TransactionRef { get; set; }   // liên kết với LoanTransaction / Payment / Disbursement
    public string EntryType { get; set; }         // "Disbursement" | "Payment" | "LateFee" | "Accrual"
    public DateOnly PostingDate { get; set; }
    public string Status { get; set; }            // "Posted" | "Reversed"
    public ICollection<JournalEntryLine> Lines { get; set; }
}

public class JournalEntryLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public string AccountCode { get; set; }   // GL account
    public string DrCr { get; set; }          // "Dr" | "Cr"
    public decimal Amount { get; set; }
}
```

**GL Mapping mẫu cho Lending:**

| Giao dịch | Debit | Credit |
|---|---|---|
| Disbursement | Loans Receivable | Cash/Nostro |
| Payment — Principal | Cash | Loans Receivable |
| Payment — Interest | Cash | Interest Income |
| Payment — Late Fee | Cash | Fee Income |
| Late Fee Accrual | Accrued Receivable | Interest Income |
| Write-off | Loan Loss Provision | Loans Receivable |

---

### B2. Accrual Engine (Lãi Dồn Tích)

**Hiện trạng:** Lãi chỉ được ghi nhận khi thực sự thu tiền (cash basis).

**Cần bổ sung:**
- Job chạy hàng ngày tính accrued interest cho toàn bộ loan đang active
- Ghi journal entry accrual: Dr Accrued Interest Receivable / Cr Interest Income
- Khi thu tiền, reverse accrual và ghi cash entry
- Báo cáo accrued income theo ngày

---

### B3. Accounting Period Close

**Cần bổ sung:**
- Khái niệm `AccountingPeriod` (tháng/quý)
- Không cho phép posting vào period đã đóng
- End-of-month close bao gồm: accrual reconciliation, GL trial balance

---

## 4. Nhóm C — Vòng Đời Khoản Vay Sâu Hơn (Advanced Loan Lifecycle)

> Ngân hàng xử lý nhiều tình huống khoản vay ngoài flow bình thường.

### C1. Loan Restructure (Cơ Cấu Lại)

**Khi nào dùng:** Khách không trả được đúng hạn, ngân hàng gia hạn/giảm lãi để tránh NPL.

**Cần bổ sung:**
- API `POST /loans/{id}/restructure` với các tham số: new tenor, new rate, new start date
- Hủy schedule cũ, tạo schedule mới
- Ghi audit trail với lý do restructure
- Maker-checker bắt buộc

**Trạng thái mới:**
```
Active → Restructured → Active (với schedule mới)
```

---

### C2. Loan Reschedule (Điều Chỉnh Lịch Trả)

**Khi nào dùng:** Thay đổi ngày trả nợ mà không đổi lãi suất hay tenor.

**Cần bổ sung:**
- API `POST /loans/{id}/reschedule` với new_payment_day
- Tính toán lại lịch từ ngày reschedule
- Audit log đầy đủ

---

### C3. Write-Off & Recovery (Xóa Nợ & Thu Hồi)

**Khi nào dùng:** Khoản vay quá hạn lâu, không thể thu hồi → ngân hàng write-off ra khỏi bảng cân đối.

**Cần bổ sung:**
- Trạng thái loan mới: `WrittenOff`
- API `POST /loans/{id}/write-off` với approval workflow
- Journal entry: Dr Loan Loss Provision / Cr Loans Receivable
- Khi thu hồi được tiền sau write-off: `POST /loans/{id}/recovery`
- Journal entry recovery: Dr Cash / Cr Recovery Income

**Entity cần thêm:**
```csharp
public class LoanWriteOff
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal WriteOffAmount { get; set; }
    public string Reason { get; set; }
    public DateOnly WriteOffDate { get; set; }
    public int ApprovedByUserId { get; set; }
}

public class LoanRecovery
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal RecoveredAmount { get; set; }
    public DateOnly RecoveryDate { get; set; }
    public string RecoveryMethod { get; set; }  // "Cash" | "CollateralLiquidation"
}
```

---

### C4. Top-Up Loan (Vay Thêm)

**Khi nào dùng:** Khách đang trả nợ tốt, muốn vay thêm tiền trên cùng khoản vay.

**Cần bổ sung:**
- Kiểm tra điều kiện top-up (DPD = 0, đã trả ≥ N tháng, tổng dư nợ mới không vượt limit)
- Tính lại outstanding và schedule sau top-up
- Tạo disbursement record mới

---

## 5. Nhóm D — Quản Lý Nợ Xấu (NPL & Provisioning)

> Ngân hàng phải phân loại nợ và trích lập dự phòng rủi ro theo quy định NHNN.

### D1. Loan Classification (Phân Loại Nợ)

**Cơ sở pháp lý:** Thông tư 11/2021/TT-NHNN (Việt Nam)

**5 nhóm nợ:**

| Nhóm | Loại | DPD | Tỷ lệ dự phòng |
|---|---|---|---|
| 1 | Nợ đủ tiêu chuẩn | 0–10 ngày | 0% |
| 2 | Nợ cần chú ý | 10–30 ngày | 5% |
| 3 | Nợ dưới tiêu chuẩn | 30–90 ngày | 20% |
| 4 | Nợ nghi ngờ | 90–180 ngày | 50% |
| 5 | Nợ có khả năng mất vốn | >180 ngày | 100% |

**Cần bổ sung:**
- Job phân loại hàng ngày dựa trên DPD
- Trường `LoanClassification` và `ProvisioningGroup` trên Loan
- Không cho restructure loan nhóm 4–5 mà không có waiver

---

### D2. Provisioning Engine (Trích Lập Dự Phòng)

**Cần bổ sung:**
- Tính toán dự phòng cần trích = outstanding × tỷ lệ theo nhóm
- Journal entry: Dr Provision Expense / Cr Loan Loss Provision
- Báo cáo dự phòng theo tháng
- Khi nợ cải thiện (migration lên nhóm tốt hơn): hoàn nhập dự phòng

---

### D3. NPL Reporting

**Cần bổ sung:**
- Tỷ lệ NPL = (dư nợ nhóm 3–5) / tổng dư nợ
- Báo cáo NPL theo sản phẩm, kỳ hạn, nhân viên tín dụng
- Xu hướng migration giữa các nhóm nợ (bucket flow report)

---

## 6. Nhóm E — KYC & Tín Dụng (Customer Due Diligence)

### E1. Tích Hợp CIC (Trung Tâm Tín Dụng Quốc Gia)

**Hiện trạng:** `CreditCheck` chỉ là record lưu kết quả, không tích hợp thực.

**Cần bổ sung:**
- API kết nối CIC để pull lịch sử tín dụng theo CCCD/MST
- Lưu CIC response: tổng dư nợ tại các TCTD khác, lịch sử trễ hạn
- Điều kiện từ chối tự động nếu CIC score dưới ngưỡng hoặc đang có nợ nhóm 3–5

---

### E2. eKYC & Document Verification

**Hiện trạng:** `CustomerDocument` chỉ là entity rỗng.

**Cần bổ sung:**
- Upload ảnh CCCD mặt trước/sau
- Tích hợp eKYC service (face matching, OCR)
- Document status: `Pending` | `Verified` | `Rejected`
- Không cho phép tạo loan nếu KYC chưa `Verified`

---

### E3. Internal Credit Scoring Model

**Hiện trạng:** Có `CreditScore` trên Customer nhưng không có logic tính.

**Cần bổ sung:**
- Scorecard model (có thể đơn giản): tuổi, thu nhập, DPD lịch sử, số khoản vay active
- Tự động tính score khi submit application
- Score quyết định: tự duyệt / chuyển manual review / từ chối tự động
- Reviewer có thể override với lý do

---

## 7. Nhóm F — Báo Cáo Vận Hành & Regulatory

### F1. Báo Cáo Quản Lý (Management Reports)

**Cần bổ sung:**

| Báo cáo | Mô tả |
|---|---|
| Portfolio Summary | Tổng dư nợ theo sản phẩm, trạng thái, ngày |
| Disbursement Report | Giải ngân theo ngày/tuần/tháng, theo nhân viên |
| Collection Performance | Tỷ lệ thu hồi theo collector, bucket, tháng |
| Overdue Movement | Số khoản vay vào/ra từng nhóm nợ theo tuần |
| NPL Trend | Xu hướng tỷ lệ NPL theo thời gian |
| Income Report | Lãi thu được, phí thu được, lãi dồn tích |
| Write-off Report | Danh sách xóa nợ, lý do, tổng số tiền |
| Recovery Report | Tiền thu hồi sau write-off |

### F2. Báo Cáo NHNN (Regulatory Reporting)

**Theo quy định Việt Nam — Cần bổ sung:**
- Báo cáo CIC định kỳ (thường hàng tháng)
- Báo cáo phân loại nợ và dự phòng (Thông tư 11)
- Báo cáo cho vay tiêu dùng (Thông tư 43/2016)

### F3. Export & BI Integration

**Cần bổ sung:**
- Export CSV/Excel cho tất cả báo cáo chính
- API endpoint trả về data cho BI tools (Power BI, Tableau)
- Filter nâng cao: theo ngày, sản phẩm, nhân viên, chi nhánh, trạng thái

---

## 8. Nhóm G — Vận Hành Hàng Ngày (Operational)

### G1. Business Calendar Nâng Cao

**Hiện trạng:** Business clock đã có nhưng chỉ ở mức basic.

**Cần bổ sung:**
- Lịch nghỉ lễ theo năm (public holidays)
- Rule: nếu due date rơi vào ngày nghỉ → chuyển sang ngày làm việc kế tiếp
- API admin cập nhật calendar hàng năm

---

### G2. End-of-Day Processing Hoàn Chỉnh

**Hiện trạng:** `EndOfDayService` đã có nhưng cần mở rộng.

**EOD batch nên bao gồm:**
1. Overdue marking (DPD update) — đã có
2. Late fee calculation — đã có
3. Loan classification update (nhóm nợ)
4. Interest accrual posting
5. Provisioning calculation
6. Notification dispatch (nhắc nợ, due soon)
7. EOD reconciliation summary
8. Tạo EOD report snapshot

---

### G3. Multi-Branch Support (Đa Chi Nhánh)

**Hiện trạng:** Entity `Branch` đã có nhưng chưa dùng nhiều.

**Cần bổ sung:**
- Filter tất cả query theo branch
- Role-based: teller/officer chỉ thấy dữ liệu chi nhánh mình
- Branch manager thấy toàn chi nhánh mình
- Head office thấy tất cả
- Báo cáo theo chi nhánh

---

### G4. Notification & Dunning Nâng Cao

**Hiện trạng:** Notification cơ bản đã có.

**Dunning strategy cần bổ sung:**

| DPD | Kênh | Nội dung |
|---|---|---|
| -3 ngày (pre-due) | SMS + Email | Nhắc đến hạn |
| DPD 1–5 | SMS | Nhắc trả nợ |
| DPD 6–15 | SMS + Call | Cảnh báo lãi phạt |
| DPD 16–30 | SMS + Email + Call | Thông báo overdue chính thức |
| DPD 31–60 | Thư + Call | Yêu cầu thanh toán |
| DPD 61–90 | Thư đòi nợ | Pre-legal notice |
| DPD >90 | Legal team | Khởi kiện / phát mãi |

**API cần thêm:**
- `GET /dunning-config` — xem cấu hình
- `PUT /dunning-config` — admin cập nhật rule
- `GET /notifications/failed` — xem notification gửi thất bại

---

## 9. Lộ Trình Thực Hiện Đề Xuất

### Phase 1 — Financial Safety (4–6 tuần)

> Làm ngay sau khi P0 release blocker xong

| ID | Nâng cấp | Độ ưu tiên |
|---|---|---|
| UPG-001 | Maker-Checker framework | 🔴 Cao |
| UPG-002 | Manual Adjustment workflow | 🔴 Cao |
| UPG-003 | Disbursement correction flow | 🔴 Cao |
| UPG-004 | Loan classification (5 nhóm nợ) | 🔴 Cao |
| UPG-005 | EOD batch hoàn chỉnh (accrual + classification) | 🟠 Trung bình |

### Phase 2 — Lending Depth (4–6 tuần)

| ID | Nâng cấp | Độ ưu tiên |
|---|---|---|
| UPG-101 | Loan Restructure | 🟠 Trung bình |
| UPG-102 | Loan Reschedule | 🟠 Trung bình |
| UPG-103 | Write-Off & Recovery | 🟠 Trung bình |
| UPG-104 | Customer Exposure Limit | 🟠 Trung bình |
| UPG-105 | Internal Credit Scoring Model | 🟡 Thấp |

### Phase 3 — Bank-Grade Infrastructure (6–8 tuần)

| ID | Nâng cấp | Độ ưu tiên |
|---|---|---|
| UPG-201 | Double-Entry Accounting Engine | 🟠 Trung bình |
| UPG-202 | Accrual Engine | 🟠 Trung bình |
| UPG-203 | Provisioning Engine | 🟠 Trung bình |
| UPG-204 | NPL Reporting | 🟡 Thấp |
| UPG-205 | CIC Integration | 🟡 Thấp |
| UPG-206 | Multi-Branch Support | 🟡 Thấp |
| UPG-207 | Regulatory Reporting | 🟡 Thấp |

### Phase 4 — Advanced Features (tùy nhu cầu)

| ID | Nâng cấp | Độ ưu tiên |
|---|---|---|
| UPG-301 | Top-Up Loan | 🟡 Thấp |
| UPG-302 | eKYC Integration | 🟡 Thấp |
| UPG-303 | BI/Export Integration | 🟡 Thấp |
| UPG-304 | Collateral Liquidation | 🟡 Thấp |
| UPG-305 | Accounting Period Close | 🟡 Thấp |

---

## 10. Tóm Tắt Delta — Hiện Tại vs Ngân Hàng

| Nhóm | Hiện tại | Sau nâng cấp |
|---|---|---|
| Financial Control | Payment flow cơ bản | Maker-checker, adjustment, correction |
| Accounting | Transaction log đơn giản | Double-entry GL, accrual |
| Loan Lifecycle | Application → Disburse → Pay → Close | + Restructure, Reschedule, Write-off, Top-up |
| NPL/Provisioning | DPD bucket | 5-nhóm nợ, dự phòng, NPL report |
| Credit Risk | CreditScore field | Scoring model, CIC, exposure limit |
| Reporting | Dashboard cơ bản | Management report, regulatory report, export |
| Operations | EOD job cơ bản | Full EOD batch, dunning, multi-branch |
| Compliance | Audit log cơ bản | Immutable audit, maker-checker trail, regulatory packs |
