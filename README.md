# LoanFlow Lite 💳

> **Hệ thống quản lý khoản vay gọn nhẹ** — dành cho công ty tài chính nhỏ, cửa hàng trả góp, đại lý điện máy/xe máy.

LoanFlow Lite là một ứng dụng quản lý vòng đời khoản vay từ đầu đến cuối: tiếp nhận hồ sơ, phê duyệt, giải ngân, thu nợ, theo dõi quá hạn — tất cả trong một hệ thống thống nhất.

---

## 📋 Mục lục

- [Tổng quan](#tổng-quan)
- [Tính năng chính](#tính-năng-chính)
- [Kiến trúc hệ thống](#kiến-trúc-hệ-thống)
- [Tech Stack](#tech-stack)
- [Cấu trúc dự án](#cấu-trúc-dự-án)
- [Cài đặt & Chạy](#cài-đặt--chạy)
- [API Endpoints](#api-endpoints)
- [Phân quyền (RBAC)](#phân-quyền-rbac)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)

---

## Tổng quan

LoanFlow Lite giải quyết bài toán quản lý khoản vay thủ công bằng Excel hoặc chat nhóm, thay thế bằng một hệ thống số hóa có kiểm soát phê duyệt, lịch sử đầy đủ, và dashboard theo dõi trực quan.

**Đối tượng sử dụng:**
- 🏪 Cửa hàng bán trả góp
- 🏢 Công ty tài chính nhỏ (SME Lender)
- 🚗 Đại lý điện máy / xe máy / thiết bị

---

## Tính năng chính

| Module | Mô tả |
|---|---|
| 🔐 **Authentication & RBAC** | Đăng nhập JWT, phân quyền theo vai trò (Admin / Loan Officer / Approver / Collector) |
| 👤 **Quản lý khách hàng** | Tạo hồ sơ khách hàng, lưu giấy tờ, lịch sử khoản vay |
| 📦 **Sản phẩm vay** | Định nghĩa sản phẩm vay: hạn mức, kỳ hạn, lãi suất, phí xử lý |
| 📝 **Hồ sơ vay** | Tiếp nhận nhu cầu vay, tính toán trả góp dự kiến, quản lý trạng thái |
| ✅ **Phê duyệt** | Luồng Submitted → Under Review → Approved / Rejected, lưu lịch sử quyết định |
| 💰 **Giải ngân** | Tạo khoản vay từ hồ sơ đã duyệt, sinh mã khoản vay tự động (LN-YYYYMMDD-XXXX) |
| 📅 **Lịch trả nợ** | Tự động sinh lịch trả theo tháng (gốc/lãi từng kỳ bằng công thức PMT) |
| 💳 **Thu nợ** | Ghi nhận thanh toán, phân bổ waterfall (lãi → gốc), cập nhật dư nợ |
| ⚠️ **Theo dõi quá hạn** | Phân loại bucket quá hạn (1–7 ngày / 8–30 ngày / 31+ ngày), ghi chú thu hồi |
| 📊 **Dashboard** | Tổng quan portfolio: tổng dư nợ, đến hạn hôm nay, quá hạn theo bucket |

---

## Kiến trúc hệ thống

```
┌─────────────────────────────────────┐
│         Frontend (Vanilla JS)        │
│   index.html + app.js + api.js       │
└──────────────────┬──────────────────┘
                   │ HTTP / REST API
┌──────────────────▼──────────────────┐
│         LoanFlow.API (.NET 8)        │
│   Controllers · JWT Auth · Swagger   │
└────────┬─────────────────┬──────────┘
         │                 │
┌────────▼───────┐ ┌───────▼────────┐
│ LoanFlow.App   │ │LoanFlow.Infra  │
│ Services · DTOs│ │DbContext·Config│
└────────┬───────┘ └───────┬────────┘
         │                 │
┌────────▼─────────────────▼────────┐
│       LoanFlow.Domain              │
│   Entities · Constants · Common    │
└───────────────────────────────────┘
                   │
        ┌──────────▼──────────┐
        │   SQL Server (EF)    │
        └─────────────────────┘
```

**Clean Architecture** với 4 layer:
- `Domain` — Entities, Constants (không phụ thuộc layer nào)
- `Application` — Services, DTOs, Interfaces
- `Infrastructure` — DbContext, EF Configurations, Seeder
- `API` — Controllers, JWT, Swagger, CORS

---

## Tech Stack

### Backend
| Thành phần | Công nghệ |
|---|---|
| Framework | ASP.NET Core 8 (Web API) |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (LocalDB hoặc SQL Server Express) |
| Authentication | JWT Bearer Token |
| Password Hashing | BCrypt.Net-Next |
| API Docs | Swagger / Swashbuckle |
| Architecture | Clean Architecture (DDD-lite) |

### Frontend
| Thành phần | Công nghệ |
|---|---|
| UI | Vanilla HTML / CSS / JavaScript |
| HTTP Client | Fetch API (api.js) |
| State | Module-based JS (app.js, ui.js) |

---

## Cấu trúc dự án

```
BankLoan/
├── LoanFlow.sln
├── README.md
├── md/
│   └── loanflow-lite-functional-planning.md   # Tài liệu planning nghiệp vụ
├── frontend/
│   ├── index.html                             # Single-page UI
│   ├── css/                                   # Stylesheet
│   └── js/
│       ├── api.js                             # HTTP client wrapper
│       ├── app.js                             # App logic & routing
│       └── ui.js                             # UI helpers
└── src/
    ├── LoanFlow.Domain/
    │   ├── Entities/                          # 14 domain entities
    │   │   ├── User.cs
    │   │   ├── Branch.cs
    │   │   ├── Customer.cs
    │   │   ├── CustomerDocument.cs
    │   │   ├── LoanProduct.cs
    │   │   ├── LoanApplication.cs
    │   │   ├── ApplicationDecision.cs
    │   │   ├── Loan.cs
    │   │   ├── LoanDisbursement.cs
    │   │   ├── RepaymentSchedule.cs
    │   │   ├── Payment.cs
    │   │   ├── DelinquencyStatus.cs
    │   │   ├── CollectionNote.cs
    │   │   └── AuditLog.cs
    │   └── Constants/                        # Status constants
    ├── LoanFlow.Application/
    │   ├── Services/                         # 6 business services
    │   │   ├── AuthService.cs
    │   │   ├── CustomerService.cs
    │   │   ├── LoanProductService.cs
    │   │   ├── LoanApplicationService.cs
    │   │   ├── LoanService.cs
    │   │   └── DashboardService.cs
    │   ├── DTOs/                             # Request/Response DTOs
    │   └── Common/                           # Result<T>, PagedResult<T>
    ├── LoanFlow.Infrastructure/
    │   └── Data/
    │       ├── LoanFlowDbContext.cs
    │       ├── DbSeeder.cs                   # Demo seed data
    │       └── Configurations/               # EF Fluent API configs
    └── LoanFlow.API/
        ├── Controllers/                      # 6 REST controllers
        │   ├── AuthController.cs
        │   ├── CustomersController.cs
        │   ├── LoanProductsController.cs
        │   ├── ApplicationsController.cs
        │   ├── LoansController.cs
        │   └── DashboardController.cs
        ├── Program.cs
        └── appsettings.json
```

---

## Cài đặt & Chạy

### Yêu cầu
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB / Express / Developer)
- Trình duyệt (Chrome / Edge / Firefox)

### 1. Clone dự án

```bash
git clone <repo-url>
cd BankLoan
```

### 2. Cấu hình connection string

Mở `src/LoanFlow.API/appsettings.json` và cập nhật:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LoanFlowLite;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "LoanFlow-Super-Secret-Key-2026-Min32Chars!!",
    "Issuer": "LoanFlow.API",
    "Audience": "LoanFlow.Client"
  }
}
```

### 3. Chạy backend

```bash
cd src/LoanFlow.API
dotnet run
```

API sẽ chạy tại `https://localhost:5001` (hoặc port được cấp).  
Swagger UI tại: `http://localhost:<port>/swagger`

> Database sẽ tự động migrate và seed demo data khi khởi động lần đầu.

### 4. Chạy frontend

Mở `frontend/index.html` trực tiếp bằng trình duyệt hoặc dùng Live Server extension (VS Code).

- Đảm bảo URL backend trong `api.js` trỏ đúng port.
- CORS đã được cấu hình cho `localhost:3000`, `localhost:5173`, `127.0.0.1:5500`.

### 5. Tài khoản mặc định (seed data)

| Username | Password | Role |
|---|---|---|
| `admin` | `Admin@123` | Admin |
| `officer1` | `Officer@123` | Loan Officer |
| `approver1` | `Approver@123` | Approver |
| `collector1` | `Collector@123` | Collector |

---

## API Endpoints

### Authentication
| Method | Endpoint | Mô tả |
|---|---|---|
| `POST` | `/api/auth/login` | Đăng nhập, nhận JWT token |

### Customers
| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/customers` | Danh sách khách hàng (có filter/paging) |
| `GET` | `/api/customers/{id}` | Chi tiết khách hàng |
| `POST` | `/api/customers` | Tạo khách hàng mới |
| `PUT` | `/api/customers/{id}` | Cập nhật thông tin |

### Loan Products
| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/loan-products` | Danh sách sản phẩm vay |
| `GET` | `/api/loan-products/{id}` | Chi tiết sản phẩm |
| `POST` | `/api/loan-products` | Tạo sản phẩm vay |
| `PUT` | `/api/loan-products/{id}` | Cập nhật sản phẩm |
| `POST` | `/api/loan-products/preview-installment` | Tính trả góp dự kiến |

### Loan Applications
| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/applications` | Danh sách hồ sơ vay |
| `GET` | `/api/applications/{id}` | Chi tiết hồ sơ |
| `POST` | `/api/applications` | Tạo hồ sơ vay mới |
| `POST` | `/api/applications/{id}/submit` | Nộp hồ sơ (Draft → Submitted) |
| `POST` | `/api/applications/{id}/decide` | Phê duyệt / từ chối |
| `POST` | `/api/applications/{id}/cancel` | Hủy hồ sơ |

### Loans
| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/loans` | Danh sách khoản vay |
| `GET` | `/api/loans/{id}` | Chi tiết khoản vay (kèm lịch trả nợ & thanh toán) |
| `POST` | `/api/loans/disburse` | Giải ngân (tạo khoản vay từ hồ sơ đã duyệt) |
| `GET` | `/api/loans/{id}/schedule` | Lịch trả nợ |
| `POST` | `/api/loans/{id}/payments` | Ghi nhận thanh toán |

### Dashboard
| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/dashboard/summary` | Tổng quan portfolio |
| `GET` | `/api/dashboard/overdue` | Danh sách & thống kê quá hạn |

---

## Phân quyền (RBAC)

| Vai trò | Quyền |
|---|---|
| **Admin** | Quản lý toàn bộ hệ thống, user, branch, sản phẩm |
| **Loan Officer** | Tạo khách hàng, tạo & nộp hồ sơ vay, giải ngân |
| **Approver** | Xem hồ sơ chờ duyệt, approve / reject |
| **Collector** | Ghi nhận thu nợ, xem overdue dashboard, ghi chú thu hồi |

---

## Luồng nghiệp vụ

```
Khách hàng
    │
    ▼
[Customer] → Tạo hồ sơ khách hàng
    │
    ▼
[LoanApplication] → Chọn sản phẩm, nhập số tiền/kỳ hạn
    │                 Tính trả góp dự kiến
    │ submit
    ▼
[Submitted] → Approver xem xét
    │ approve / reject
    ▼
[Approved] ──────────────────► [Rejected]
    │
    │ disburse
    ▼
[Loan Account] → Sinh mã LN-YYYYMMDD-XXXX
    │             Tạo lịch trả nợ tự động
    │
    ▼
[RepaymentSchedule] → Từng kỳ: gốc / lãi / ngày đến hạn
    │
    │ collect payment
    ▼
[Payment] → Phân bổ waterfall (lãi → gốc)
    │         Cập nhật dư nợ còn lại
    │
    ▼
[Overdue Dashboard] → Theo dõi quá hạn theo bucket
                       Ghi chú thu hồi
```

---

## 🗺️ Roadmap

### v1.0 — MVP (Hiện tại)
- [x] Authentication & RBAC
- [x] Customer Management
- [x] Loan Product Management
- [x] Loan Application & Approval Workflow
- [x] Loan Disbursement & Account Creation
- [x] Repayment Schedule Generation (PMT formula)
- [x] Payment Posting với waterfall allocation
- [x] Overdue Dashboard & Delinquency Buckets
- [x] Audit Log cơ bản
- [x] Demo seed data

### v2.0 — Planned
- [ ] Penalty Engine (phí phạt quá hạn tự động)
- [ ] SMS / Email Reminder
- [ ] Report Export (Excel/PDF)
- [ ] Document Management (upload hồ sơ)

### v3.0 — Future
- [ ] Loan Restructure / Reschedule
- [ ] Accounting Export (journal entries)
- [ ] Mobile Collector App
- [ ] Customer Self-Service Portal

---

## 📄 License

Dự án được phát triển cho mục đích demo và thương mại hóa nội bộ.

---

*Built with ❤️ using ASP.NET Core 8 + Clean Architecture*
