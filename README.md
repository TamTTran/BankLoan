# LoanFlow Lite 💳

> **Hệ thống quản lý vòng đời khoản vay End-to-End** — Số hóa toàn bộ quy trình từ tiếp nhận hồ sơ, thẩm định, giải ngân qua NAPAS, thu nợ, xử lý nợ xấu đến đối soát & tất toán.

---

## 📋 Mục lục

- [Hệ thống dùng để làm gì?](#hệ-thống-dùng-để-làm-gì)
- [Ai sử dụng hệ thống?](#ai-sử-dụng-hệ-thống)
- [Các tính năng chính](#các-tính-năng-chính)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)
- [Cài đặt & Chạy](#cài-đặt--chạy)
- [Tài khoản mặc định](#tài-khoản-mặc-định)
- [Tích hợp NAPAS](#tích-hợp-napas)
- [Phân quyền](#phân-quyền)
- [API Reference](#api-reference)

---

## Hệ thống dùng để làm gì?

LoanFlow Lite giúp **công ty tài chính, quỹ tín dụng, và chuỗi bán lẻ trả góp** quản lý toàn bộ vòng đời khoản vay trên một nền tảng duy nhất — thay thế Excel, quy trình thủ công và giấy tờ rời rạc.

**Vấn đề giải quyết:**

| Bài toán | Giải pháp LoanFlow |
|----------|-------------------|
| Hồ sơ vay giấy tờ, mất kiểm soát | Quy trình số hóa, trạng thái rõ ràng từng bước |
| Giải ngân thủ công, chậm và sai sót | Giải ngân tự động qua NAPAS (chuyển khoản liên ngân hàng) |
| Không biết ai đang nợ, nợ bao nhiêu | Dashboard thời gian thực, phân loại DPD tự động |
| Thu nợ không có hệ thống | Phân công Collector, nhắc nợ tự động theo Dunning Strategy |
| Đối soát cuối ngày mất hàng giờ | Reconciliation Engine 6 điểm chạy tự động |
| Không có báo cáo pháp quy (NHNN) | Báo cáo NPL, phân loại nhóm nợ 1–5 tự động |

---

## Ai sử dụng hệ thống?

| Vai trò | Mô tả | Quyền hạn chính |
|---------|-------|----------------|
| **Admin** | Quản trị hệ thống | Toàn quyền — quản lý user, cấu hình, xem tất cả |
| **Manager** | Trưởng phòng / Giám đốc chi nhánh | Xem báo cáo, duyệt write-off, restructure, phân công |
| **Loan Officer** | Nhân viên tín dụng | Tạo hồ sơ, submit phê duyệt, giải ngân, thu nợ |
| **Approver** | Người phê duyệt | Duyệt/từ chối hồ sơ vay, reversal, restructure |
| **Collector** | Nhân viên thu hồi nợ | Xem hàng đợi nợ quá hạn, ghi nhận hành động thu nợ |

---

## Các tính năng chính

### 🧑 Quản lý khách hàng & Hồ sơ
- Tạo hồ sơ khách hàng (CMND/CCCD, SĐT, địa chỉ, thu nhập)
- **eKYC** — Xác minh danh tính điện tử (tích hợp FPT.AI)
- **CIC** — Tra cứu lịch sử tín dụng quốc gia (PCB / mock)
- Credit Scoring nội bộ (tự động chấm điểm A–E)
- AML/KYC — Kiểm tra PEP, mức độ rủi ro rửa tiền (TT09/2023)
- Giới hạn dư nợ khách hàng (Exposure Limit)

### 📝 Hồ sơ vay & Phê duyệt
- Chọn sản phẩm vay (Tín chấp, Thế chấp, Mua nhà, Mua xe, Kinh doanh, Thấu chi)
- Xem trước lịch trả góp (PMT calculator — Flat / Reducing Balance)
- **Maker-Checker** — người tạo khác người duyệt, không thể tự duyệt
- Approval Rules tự động (duyệt điều kiện theo rule engine)
- E-Contract — hợp đồng điện tử có hash PDF

### 💸 Giải ngân qua NAPAS
- **Giải ngân tự động** — gửi lệnh chuyển khoản NAPAS ngay sau khi duyệt
- Hỗ trợ cả **Sync** (kết quả ngay) và **Async** (NAPAS callback sau)
- Retry tự động 3 lần với exponential backoff (Polly)
- Tracking trạng thái: `Pending → Completed / Failed`
- Compensating action tự động khi thất bại (tạo Reversal pending)
- Idempotency — chống giải ngân trùng lặp
- **Mã ngân hàng NAPAS** đầy đủ (30+ ngân hàng: VCB, TCB, MBB, VPB...)

### 🏦 NAPAS Core
- **Transaction Query** — tra cứu giao dịch theo Tx ID hoặc Reference ID
- **Merchant Info** — thông tin & cài đặt Merchant đăng ký NAPAS
- **Reconciliation** — tải file đối soát NAPAS, khớp lệnh tự động
- **VietQR** — tạo mã QR chuẩn EMV QRCPS offline (không cần NAPAS API)
  - Pre-fill amount theo kỳ trả nợ đến hạn
  - Trả về QR string + Base64 PNG + VietQR URL

### 💰 Thu nợ & Phân bổ
- Ghi nhận thanh toán (Tiền mặt / Chuyển khoản)
- **Waterfall Allocation**: Phí trễ hạn → Lãi → Gốc → Suspense
- Tài khoản ảo (Virtual Account) — nhận thanh toán tự động qua webhook
- Tài khoản tạm giữ (Suspense) — xử lý overpayment
- Tất toán sớm (Early Settlement) — tính lãi thực tế

### 📞 Thu hồi nợ (Collection)
- Phân loại DPD tự động (1–7 / 8–30 / 31+ ngày quá hạn)
- Phân công Collector theo khoản vay
- Ghi nhận hành động: Gọi điện, Gặp trực tiếp, Gửi SMS/thư
- **Dunning tự động**: nhắc nợ theo chiến lược DPD

### 🔄 Vòng đời khoản vay
- **Restructure** — cơ cấu lại (đổi lãi suất, kỳ hạn)
- **Reschedule** — dời ngày trả nợ hàng tháng
- **Top-Up** — giải ngân thêm trên khoản vay hiện có
- **Reversal** — hủy giao dịch sai (qua Maker-Checker)
- **Write-Off** — xóa nợ có khả năng mất vốn
- **Recovery** — thu hồi tiền sau khi đã xóa nợ
- Phân loại nợ nhóm 1–5 (theo quy định NHNN)

### 📊 Sổ cái & Đối soát
- **Immutable Ledger** — không bao giờ xóa/sửa dữ liệu gốc
- Bút toán kế toán tự động (Journal Entry) double-entry
- **EOD Job** — chạy tự động cuối ngày (Overdue detection, FX Revaluation)
- **Reconciliation Engine 6 điểm**: Payment / Allocation / Disbursement / Reversal / LateFee / Suspense
- Đối soát file NAPAS — khớp lệnh + tạo exception tự động
- Đóng/mở kỳ kế toán (Accounting Period)

### 📋 Báo cáo
- NPL Report (Non-Performing Loan) — nợ xấu theo nhóm
- Regulatory Report — báo cáo pháp quy (NHNN)
- Dashboard thời gian thực (Portfolio summary)
- AML Dashboard — cảnh báo giao dịch đáng ngờ
- Risk Dashboard — capital adequacy, exposure

### 🔔 Thông báo thời gian thực
- Notification real-time qua SignalR WebSocket
- Các sự kiện: duyệt hồ sơ, giải ngân, thu nợ, reversal, callback NAPAS
- Đánh dấu đã đọc / đọc tất cả

---

## Luồng nghiệp vụ

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Phase 1   │───▶│   Phase 2   │───▶│   Phase 3   │───▶│   Phase 4   │
│ Tiếp nhận   │    │ Thẩm định & │    │ Giải ngân   │    │  Thu nợ &   │
│ khách hàng  │    │  Phê duyệt  │    │   NAPAS     │    │  Phân bổ   │
└─────────────┘    └─────────────┘    └─────────────┘    └──────┬──────┘
                                                                 │
  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
  │   Phase 7   │◀───│   Phase 6   │◀───│   Phase 5   │◀────────┘
  │  Sổ cái &   │    │  Vòng đời   │    │  Thu hồi    │
  │  Đối soát   │    │  nâng cao   │    │   nợ xấu   │
  └─────────────┘    └─────────────┘    └─────────────┘
```

### Trạng thái khoản vay

```
Hồ sơ:   Draft ──submit──▶ Submitted ──approve──▶ Approved ──▶ Giải ngân
                                        │
                                        └──reject──▶ Rejected

Khoản vay: Active ──▶ Overdue ──▶ Restructured / WrittenOff / Closed

Giải ngân: Pending ──▶ Completed
                   └──▶ Failed  ──▶ (Auto reversal pending)
```

---

## Cài đặt & Chạy

### Yêu cầu tối thiểu
- **Docker** & **Docker Compose** *(không cần cài .NET hay Node.js)*

### Khởi động toàn hệ thống

```bash
git clone <repo-url>
cd BankLoan

# Copy file cấu hình
cp .env.example .env

# Build & Start (Frontend + Backend + MySQL)
docker compose --profile full up -d --build
```

### Truy cập

| Dịch vụ | URL | Ghi chú |
|---------|-----|---------|
| 🌐 **Web App** | `http://localhost` | Giao diện người dùng |
| 📡 **API Docs** | `http://localhost:8080/swagger` | Swagger UI |
| 🗄️ **MySQL** | `localhost:3306` | root / `LoanFlow@2026!` |

### Chạy ở môi trường phát triển

```bash
# Chỉ chạy database
docker compose up db -d

# Terminal 1: Backend API
cd src/LoanFlow.API
dotnet watch run

# Terminal 2: Frontend
cd frontend
npm install && npm run dev
# → http://localhost:5173
```

---

## Tài khoản mặc định

Hệ thống tự động tạo tài khoản demo khi khởi động lần đầu.

| Vai trò | Username | Password mặc định |
|---------|----------|-------------------|
| Admin | `admin` | `LoanFlow@2026!` |
| Manager | `manager` | `LoanFlow@2026!` |
| Loan Officer | `officer` | `LoanFlow@2026!` |
| Approver | `approver` | `LoanFlow@2026!` |
| Collector | `collector` | `LoanFlow@2026!` |

> Đặt password trước qua biến môi trường trong `.env`:
> ```
> SEED_ADMIN_PASSWORD=YourPassword@123
> SEED_OFFICER_PASSWORD=YourPassword@123
> ```

---

## Tích hợp NAPAS

### Chế độ Mock (mặc định — chạy ngay không cần credentials)

```env
# .env
USE_MOCK_PAYMENT_GATEWAY=true
```

**Test các scenario giải ngân:**

| BankCode khách hàng | Kết quả |
|---------------------|---------|
| `VCB`, `TCB`, `MBB`... (mã hợp lệ) | ✅ Giải ngân SUCCESS ngay |
| `ASYNC` | ⏳ Trả về Pending, cần callback thủ công |
| `FAIL` | ❌ Giải ngân FAILED, tạo reversal pending |

**Simulate NAPAS callback (async path):**
```bash
curl -X POST http://localhost/api/napas/callback \
  -H "Content-Type: application/json" \
  -H "X-Napas-Signature: MOCK-VALID" \
  -d '{
    "referenceId": "LF-20260501xxxxxx-<guid>",
    "napasTransactionId": "NAPAS-001",
    "status": "SUCCESS",
    "processedAt": "2026-05-01T10:00:00Z"
  }'
```

**Query trạng thái giải ngân:**
```bash
GET /api/napas/disbursements/{referenceId}/status
```

### Chế độ Production (NAPAS Sandbox/Live)

```env
# .env
USE_MOCK_PAYMENT_GATEWAY=false
NAPAS_API_URL=https://sandbox.napas.com.vn/api
NAPAS_API_KEY=<key-từ-NAPAS-merchant-portal>
NAPAS_CALLBACK_SECRET=<hmac-secret>
```

> **Lưu ý bảo mật:** Không commit `.env` lên Git. Dùng Azure Key Vault hoặc Docker secrets cho production.

### VietQR — Tạo mã QR thu tiền

```bash
# Tạo QR cho khoản vay (pre-fill amount từ kỳ trả nợ gần nhất)
GET /api/napas/qr/loan/{loanId}

# Tạo QR tùy chỉnh
POST /api/napas/qr/generate
{
  "amount": 5000000,
  "description": "Tra no ky 1 - LN-20260501-0001",
  "bankCode": "TCB",
  "accountNumber": "19038888888888"
}
```

---

## Phân quyền

| Chức năng | Admin | Manager | Loan Officer | Approver | Collector |
|-----------|:-----:|:-------:|:------------:|:--------:|:---------:|
| Quản lý khách hàng | ✅ | 👁️ | ✅ | 👁️ | ❌ |
| Tạo & submit hồ sơ vay | ✅ | ❌ | ✅ | ❌ | ❌ |
| Phê duyệt / Từ chối | ✅ | ❌ | ❌ | ✅ | ❌ |
| Giải ngân | ✅ | ❌ | ✅ | ❌ | ❌ |
| Ghi nhận thanh toán | ✅ | ❌ | ✅ | ❌ | ✅ |
| Phân công Collector | ✅ | ✅ | ❌ | ❌ | ❌ |
| Thu hồi nợ | ✅ | 👁️ | ❌ | ❌ | ✅ |
| Restructure / Write-off | ✅ | ✅ | ❌ | ✅ | ❌ |
| Xem Dashboard & Báo cáo | ✅ | ✅ | 👁️ | 👁️ | ❌ |
| NAPAS Query / Merchant | ✅ | ✅ | ✅ | ❌ | ❌ |
| NAPAS Reconciliation | ✅ | ✅ | ❌ | ❌ | ❌ |
| VietQR tạo mã QR | ✅ | ✅ | ✅ | ❌ | ✅ |
| Quản lý User | ✅ | ❌ | ❌ | ❌ | ❌ |

> 👁️ = Chỉ xem (read-only)

---

## API Reference

### 🔐 Xác thực
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/api/auth/login` | Đăng nhập → JWT + Refresh Token |
| `POST` | `/api/auth/refresh` | Gia hạn token |
| `POST` | `/api/auth/logout` | Đăng xuất |
| `POST` | `/api/auth/mfa/enable` | Bật xác thực 2 bước (TOTP) |

### 👤 Khách hàng & KYC
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET/POST` | `/api/customers` | Danh sách / Tạo khách hàng |
| `GET/PUT` | `/api/customers/{id}` | Chi tiết / Cập nhật |
| `POST` | `/api/ekyc/verify` | eKYC — xác minh CMND/CCCD |
| `POST` | `/api/cic/inquire` | Tra cứu CIC |
| `GET/PUT` | `/api/exposure-limits/{customerId}` | Hạn mức dư nợ |

### 📝 Hồ sơ vay
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET/POST` | `/api/applications` | Danh sách / Tạo hồ sơ |
| `POST` | `/api/applications/preview` | Xem trước lịch trả góp |
| `POST` | `/api/applications/{id}/submit` | Nộp hồ sơ |
| `POST` | `/api/applications/{id}/approve` | Phê duyệt |
| `POST` | `/api/applications/{id}/reject` | Từ chối |

### 💰 Khoản vay
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/loans` | Danh sách khoản vay |
| `GET` | `/api/loans/{id}` | Chi tiết khoản vay |
| `POST` | `/api/loans/disburse` | Giải ngân (NAPAS auto-transfer) |
| `POST` | `/api/loans/{id}/payments` | Ghi nhận thanh toán |
| `GET` | `/api/loans/{id}/schedule` | Lịch trả nợ |
| `POST` | `/api/loans/{id}/early-settle/preview` | Preview tất toán sớm |
| `POST` | `/api/loans/{id}/early-settle` | Tất toán sớm |

### 🔄 Vòng đời khoản vay
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/api/loans/{id}/restructure` | Cơ cấu lại |
| `POST` | `/api/loans/{id}/reschedule` | Dời lịch trả nợ |
| `POST` | `/api/loans/{id}/request-reversal` | Yêu cầu hủy giao dịch |
| `POST` | `/api/reversals/{id}/approve` | Duyệt reversal |
| `POST` | `/api/loans/{id}/write-off` | Xóa nợ |
| `POST` | `/api/loans/{id}/recovery` | Thu hồi sau xóa nợ |
| `POST` | `/api/loans/{id}/topup` | Giải ngân thêm (Top-Up) |

### 📞 Thu hồi nợ
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/collections/queue` | Hàng đợi nợ quá hạn |
| `POST` | `/api/loans/{id}/assign-collector` | Phân công Collector |
| `POST` | `/api/collections/{id}/actions` | Ghi nhận hành động thu nợ |
| `POST` | `/api/dunning/execute` | Chạy nhắc nợ tự động |

### 🏦 NAPAS Gateway
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/api/napas/callback` | Nhận callback async từ NAPAS |
| `GET` | `/api/napas/disbursements/{refId}/status` | Query trạng thái giải ngân |
| `GET` | `/api/napas/transactions/{napasTransactionId}` | Chi tiết giao dịch NAPAS |
| `GET` | `/api/napas/transactions/by-reference/{refId}` | Tra cứu theo Reference ID |
| `POST` | `/api/napas/transactions/search` | Tìm kiếm giao dịch |
| `GET` | `/api/napas/merchant` | Thông tin Merchant NAPAS |
| `GET` | `/api/napas/merchant/settings` | Cài đặt hạn mức & kênh |
| `GET` | `/api/napas/reconciliation/files` | Danh sách file đối soát |
| `GET` | `/api/napas/reconciliation/files/{date}/download` | Tải file đối soát |
| `POST` | `/api/napas/reconciliation/files/{date}/match` | Khớp lệnh NAPAS vs nội bộ |
| `POST` | `/api/napas/qr/generate` | Tạo VietQR code |
| `POST` | `/api/napas/qr/decode` | Giải mã QR string |
| `GET` | `/api/napas/qr/loan/{loanId}` | QR code cho khoản vay |

### 📊 Báo cáo & Đối soát
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/dashboard/summary` | Portfolio tổng quan |
| `POST` | `/api/reconciliation/run` | Chạy đối soát 6 điểm |
| `GET` | `/api/reconciliation/exceptions` | Danh sách lệch sổ |
| `POST` | `/api/eod/run` | Chạy End-of-Day thủ công |
| `GET` | `/api/reports/npl` | Báo cáo nợ xấu NPL |
| `GET` | `/api/reports/regulatory` | Báo cáo pháp quy NHNN |
| `POST` | `/api/interbank/sync-journals` | Đồng bộ Core Banking |
| `POST` | `/api/interbank/virtual-account-webhook` | Webhook nhận thanh toán VA |

---

## Tech Stack

| Layer | Công nghệ | Ghi chú |
|-------|-----------|---------|
| **Backend** | ASP.NET Core 8, EF Core 8, MySQL 8.4 | 42 controllers, Clean Architecture |
| **Frontend** | React 18, Vite 5, React Router v6 | 18 trang, SignalR real-time |
| **Payment** | NAPAS Pay 24/7 API | Sync + Async, Polly retry |
| **QR Code** | VietQR EMV QRCPS | Offline generation |
| **Security** | JWT Bearer, BCrypt, HMAC-SHA256 | Rate limiting, Idempotency |
| **Observability** | Serilog, OpenTelemetry, Prometheus | Structured logging |
| **DevOps** | Docker Compose, Nginx | Automated DB backup (Ofelia) |

---

## 📄 License

Phát triển cho mục đích demo và thương mại hóa nội bộ.

---

*Built with ❤️ using ASP.NET Core 8 · React 18 · MySQL · Docker · NAPAS*
