# Hướng dẫn cấu hình dịch vụ bên thứ 3

LoanFlow Lite tích hợp 5 dịch vụ bên ngoài. Mỗi dịch vụ đều có **chế độ Mock** để phát triển mà không cần credentials thực.

---

## Mục lục

- [Cách hoạt động của Mock/Real toggle](#cách-hoạt-động-của-mockreal-toggle)
- [1. NAPAS — Thanh toán & Giải ngân](#1-napas--thanh-toán--giải-ngân)
- [2. PCB/CIC — Tra cứu tín dụng](#2-pcbcic--tra-cứu-tín-dụng)
- [3. FPT eKYC — Xác minh danh tính](#3-fpt-ekyc--xác-minh-danh-tính)
- [4. Twilio — SMS & OTP](#4-twilio--sms--otp)
- [5. Certificate Authority — Ký số eContract](#5-certificate-authority--ký-số-econtract)
- [Checklist chuyển sang Production](#checklist-chuyển-sang-production)

---

## Cách hoạt động của Mock/Real toggle

Mỗi dịch vụ được kiểm soát bởi một **feature flag** trong `.env`:

```
USE_MOCK_PAYMENT_GATEWAY=true   # → MockNapasPaymentGatewayService (offline)
USE_MOCK_PAYMENT_GATEWAY=false  # → NapasPaymentGatewayService (gọi API thật)
```

| Flag | `true` (mặc định) | `false` |
|------|-------------------|---------|
| `USE_MOCK_PAYMENT_GATEWAY` | Mock NAPAS (offline) | NAPAS API thật |
| `USE_MOCK_CIC` | Mock CIC (luôn pass) | PCB API thật |
| `USE_MOCK_EKYC` | Mock eKYC (luôn pass) | FPT eKYC API thật |
| `USE_MOCK_SMS` | Null SMS (log ra console) | Twilio API thật |
| `USE_MOCK_CERTIFICATE_AUTHORITY` | Mock CA (hash tự tạo) | CA API thật |

> **Khuyến nghị**: Trong development, giữ tất cả `USE_MOCK_*=true`. Chỉ bật từng dịch vụ thật khi cần test integration.

---

## 1. NAPAS — Thanh toán & Giải ngân

**Vai trò:** Chuyển khoản liên ngân hàng tự động khi giải ngân khoản vay. Hỗ trợ thanh toán QR (VietQR).

### Đăng ký

1. Vào [NAPAS Merchant Portal](https://napas.com.vn) → Đăng ký Merchant
2. Chọn loại dịch vụ: **Pay 24/7** (Outbound Transfer)
3. Nhận: `Merchant ID`, `API Key`, `Callback Secret`
4. Khai báo **Callback URL**: `https://your-domain.com/api/napas/callback`

### Cấu hình `.env`

```env
USE_MOCK_PAYMENT_GATEWAY=false

NAPAS_API_URL=https://sandbox.napas.com.vn/api   # hoặc https://api.napas.com.vn (prod)
NAPAS_API_KEY=<api-key-từ-merchant-portal>
NAPAS_CALLBACK_SECRET=<hmac-secret-từ-merchant-portal>
NAPAS_TIMEOUT_SECONDS=10
NAPAS_MAX_RETRY_ATTEMPTS=3
NAPAS_RETRY_BASE_DELAY_MS=1000
NAPAS_ENVIRONMENT=sandbox   # đổi thành production khi live
```

### Kiểm tra callback

NAPAS gọi về `POST /api/napas/callback` với header `X-Napas-Signature`.  
Hệ thống verify bằng **HMAC-SHA256** với `NAPAS_CALLBACK_SECRET`.

```bash
# Test callback thủ công (mock signature cho sandbox)
curl -X POST https://your-domain.com/api/napas/callback \
  -H "Content-Type: application/json" \
  -H "X-Napas-Signature: <hmac-sha256-of-body>" \
  -d '{
    "referenceId": "LF-20260501xxxxxx-<guid>",
    "napasTransactionId": "NAPAS-20260501-001",
    "status": "SUCCESS",
    "processedAt": "2026-05-01T10:00:00Z"
  }'
```

### Test với Mock

| `BankCode` của khách hàng | Kết quả giả lập |
|--------------------------|----------------|
| `VCB`, `TCB`, `MBB`... (mã NAPAS hợp lệ) | ✅ `SUCCESS` ngay lập tức |
| `ASYNC` | ⏳ `Pending` — cần gọi callback thủ công |
| `FAIL` | ❌ `FAILED` — tạo reversal pending |

### Lưu ý bảo mật

- `NAPAS_CALLBACK_SECRET` phải khác nhau giữa sandbox và production
- Đảm bảo `/api/napas/callback` **không** yêu cầu JWT (NAPAS gọi server-to-server)
- Hệ thống tự động **idempotency check** — cùng `referenceId` gọi nhiều lần chỉ xử lý 1 lần

---

## 2. PCB/CIC — Tra cứu tín dụng

**Vai trò:** Tra cứu lịch sử tín dụng quốc gia của khách hàng trước khi cho vay.

### Đăng ký

1. Vào [PCB Vietnam](https://pcb.com.vn) → Đăng ký tài khoản tổ chức tín dụng
2. Ký hợp đồng sử dụng dữ liệu CIC với NHNN
3. Nhận: `API Key`, `Institution Code`

### Cấu hình `.env`

```env
USE_MOCK_CIC=false

PCB_API_URL=https://api.pcb.com.vn/v2
PCB_API_KEY=<api-key-từ-pcb>
```

### Mock hoạt động như thế nào

Khi `USE_MOCK_CIC=true`, mọi tra cứu CIC đều trả về:
- `CreditGrade: B` (tốt)
- `TotalDebt: 0`
- `DefaultHistory: []`

Để test luồng từ chối vay vì CIC xấu — hiện chưa có mock scenario riêng, có thể tự set thủ công trong DB.

---

## 3. FPT eKYC — Xác minh danh tính

**Vai trò:** Xác minh CMND/CCCD và khuôn mặt (Face Match + Liveness Detection) của khách hàng.

### Đăng ký

1. Vào [FPT eKYC Portal](https://ekyc.fpt.ai) → Đăng ký tài khoản doanh nghiệp
2. Tạo project → nhận `API Key`
3. Chọn gói: OCR + Face Match + Liveness

### Cấu hình `.env`

```env
USE_MOCK_EKYC=false

FPT_EKYC_API_URL=https://ekyc.fpt.ai/api/v2
FPT_EKYC_API_KEY=<api-key-từ-fpt-ekyc-portal>
```

### Flow xác minh

```
Client upload ảnh CMND + ảnh selfie
    │
    ▼
POST /api/ekyc/verify
    │
    ▼
FptEkycProvider → FPT API:
    ├─ OCR: trích xuất họ tên, CMND, ngày sinh, địa chỉ
    ├─ Face Match: ảnh CMND vs ảnh selfie (score %)
    └─ Liveness: phát hiện ảnh thật/giả
    │
    ▼
EkycVerification entity lưu vào DB:
    - OcrConfidence, FaceMatchScore, LivenessScore
    - Status: Approved / Rejected / ManualReview
```

### Ngưỡng chấp nhận (cấu hình trong `appsettings.json`)

Mặc định:
- `FaceMatchScore >= 80%` → Approved
- `FaceMatchScore 60–79%` → ManualReview
- `FaceMatchScore < 60%` → Rejected

---

## 4. Twilio — SMS & OTP

**Vai trò:** Gửi OTP xác thực và thông báo SMS cho khách hàng.

### Đăng ký

1. Vào [Twilio Console](https://console.twilio.com) → Tạo tài khoản
2. Mua số điện thoại Việt Nam (hoặc dùng Twilio Verify Service)
3. Nhận: `Account SID`, `Auth Token`, `From Number`

> **Lưu ý cho Việt Nam:** Twilio yêu cầu đăng ký thương hiệu (Brand Registration) để gửi SMS qua mạng Việt Nam. Nếu chưa đăng ký, SMS có thể bị filter. Thay thế: dùng **ESMS**, **VIETGUYS**, hoặc **SpeedSMS**.

### Cấu hình `.env`

```env
USE_MOCK_SMS=false

TWILIO_ACCOUNT_SID=ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
TWILIO_AUTH_TOKEN=<auth-token-từ-twilio-console>
TWILIO_FROM_NUMBER=+84901234567
```

### Mock SMS

Khi `USE_MOCK_SMS=true`, tất cả SMS được **log ra console** thay vì gửi thật:

```
[NULL-SMS] To: +84901234567 | Message: Mã OTP của bạn là: 123456. Hiệu lực 5 phút.
```

Rất tiện để phát triển vì không tốn tiền và không cần SIM thật.

---

## 5. Certificate Authority — Ký số eContract

**Vai trò:** Ký số hợp đồng vay điện tử (eContract) bằng chứng thư số được NHNN công nhận.

### Đăng ký

Tích hợp với một trong các CA được cấp phép tại Việt Nam:
- **VNPT-CA**: https://ca.vnpt.vn
- **Viettel-CA**: https://ca.viettel.com.vn
- **BKAV-CA**: https://ca.bkav.com.vn
- **FPT-CA**: https://ca.fpt.com.vn

Mỗi CA có API riêng. Implement `ICertificateAuthorityService` interface theo tài liệu API của CA bạn chọn.

### Cấu hình `.env`

```env
USE_MOCK_CERTIFICATE_AUTHORITY=false

CERTIFICATE_AUTHORITY_API_URL=https://ca-provider.example/api/sign
CERTIFICATE_AUTHORITY_API_KEY=<api-key-từ-ca-provider>
```

### Mock CA

Khi `USE_MOCK_CERTIFICATE_AUTHORITY=true`, hệ thống:
- Tạo **SHA-256 hash** của nội dung PDF hợp đồng
- Lưu `PdfContentHash` vào `EContract` entity
- Không gọi CA API thật

Hợp đồng mock **không có giá trị pháp lý** — chỉ dùng cho dev/test.

---

## Checklist chuyển sang Production

Trước khi go-live, đảm bảo hoàn thành toàn bộ checklist sau:

### 🔐 Secrets

- [ ] `JWT_KEY` — tạo mới bằng `openssl rand -base64 48`, **khác hoàn toàn** so với dev
- [ ] `DB_PASSWORD` — password mạnh, không giống môi trường dev
- [ ] `WEBHOOK_SECRET` — tạo mới bằng `openssl rand -hex 32`
- [ ] Tất cả secrets lưu trong **Azure Key Vault** / **AWS Secrets Manager** / **Docker Secrets** — không để trong `.env` trên server

### 🏦 NAPAS

- [ ] Đổi `NAPAS_API_URL` sang endpoint production (không phải sandbox)
- [ ] Đổi `NAPAS_ENVIRONMENT=production`
- [ ] Verify callback URL đã được đăng ký chính xác trên NAPAS Merchant Portal
- [ ] Test 1 giao dịch nhỏ (10.000 VND) trước khi mở toàn bộ

### 🔌 Dịch vụ khác

- [ ] `USE_MOCK_PAYMENT_GATEWAY=false`
- [ ] `USE_MOCK_SMS=false`
- [ ] `USE_MOCK_CIC=false`
- [ ] `USE_MOCK_EKYC=false`
- [ ] `USE_MOCK_CERTIFICATE_AUTHORITY=false`

### 🌐 Network & TLS

- [ ] TLS certificate hợp lệ (Let's Encrypt hoặc CA mua)
- [ ] `PUBLIC_APP_ORIGIN` trỏ đúng domain thật
- [ ] `TRUSTED_PROXY_IP` = IP của Nginx / Load Balancer
- [ ] Firewall: chỉ mở port 80/443 ra ngoài; MySQL/API chỉ trong nội bộ

### 📊 Monitoring

- [ ] Prometheus + Grafana đã kết nối
- [ ] Alert rules cho: API error rate > 1%, NAPAS callback timeout, DB connection pool
- [ ] Log retention policy (Loki hoặc CloudWatch)

---

## Câu hỏi thường gặp

**Q: Có thể bật từng dịch vụ thật riêng lẻ không?**  
A: Có. Ví dụ `USE_MOCK_SMS=false` (Twilio thật) trong khi `USE_MOCK_PAYMENT_GATEWAY=true` (NAPAS mock). Các flag hoàn toàn độc lập.

**Q: Mock NAPAS có gửi tiền thật không?**  
A: Không. `MockNapasPaymentGatewayService` và `MockNapasCoreService` hoàn toàn offline, không gọi bất kỳ API nào ra ngoài.

**Q: NAPAS callback không được nhận — debug thế nào?**  
A: Kiểm tra theo thứ tự:
1. Log container: `docker logs loanflow-api | grep NAPAS-CALLBACK`
2. Đảm bảo `NAPAS_CALLBACK_SECRET` khớp với secret trên Merchant Portal
3. Endpoint `/api/napas/callback` phải accessible từ IP của NAPAS (thêm vào firewall whitelist)
4. Header `X-Napas-Signature` phải có trong request

**Q: Làm sao thay Twilio bằng nhà cung cấp SMS Việt Nam?**  
A: Implement interface `ISmsService` (trong `LoanFlow.Application/Services/ISmsService.cs`) rồi đăng ký trong `Program.cs`:
```csharp
builder.Services.AddHttpClient<ISmsService, EsmsSmsService>();
```
