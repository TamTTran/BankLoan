# API Security Recommendations

## Muc tieu

Tai lieu nay tong hop cac de xuat uu tien de nang muc do an toan cho API `LoanFlow`, dua tren review ma nguon backend hien tai.

## Uu tien 1: Chan takeover ngay

- Bo toan bo secret khoi source code:
  - `Jwt:Key`
  - database password
  - moi credential mau trong `appsettings.json` va `.env.example`
- Rotate ngay tat ca secret hien tai vi da bi commit vao repo.
- Tat seed tai khoan mac dinh o production.
- Neu van can tai khoan demo cho local/dev, chi seed trong `Development`.
- Neu buoc phai tao user bootstrap, dung mat khau ngau nhien va bat buoc doi mat khau khi dang nhap lan dau.

## Uu tien 2: Hardening authentication

- Them rate limiting cho `POST /api/auth/login`:
  - theo IP
  - theo username
- Them failed-attempt counter va lockout tam thoi.
- Giam JWT lifetime tu `8h` xuong muc ngan hon, vi du `15-60 phut`.
- Neu can session dai hon, dung refresh token thay vi access token song qua lau.
- Cau hinh JWT chat hon:
  - key length toi thieu
  - `ClockSkew` nho
  - tach secret theo moi truong
- Xem xet them `jti` va co che revoke token neu he thong can logout thuc su.

## Uu tien 3: Giam attack surface

- Chi bat Swagger trong `Development`, hoac bao ve bang admin auth/IP allowlist.
- Them `UseHttpsRedirection()`.
- Neu chay sau reverse proxy/nginx, cau hinh `ForwardedHeaders` dung cach.
- Dua danh sach CORS origins vao config theo moi truong, khong hardcode origin dev cho production runtime.

## Uu tien 4: Bao ve du lieu nhay cam

- Giam PII tra ve tu API.
- Tach DTO theo use case:
  - danh sach chi tra masked fields
  - detail moi tra du lieu day du cho role phu hop
- Can xem lai cac field nhay cam:
  - `IdNumber`
  - `Address`
  - `DateOfBirth`
  - `Phone`
- Mask phone va thong tin khach hang trong overdue/report neu khong that su can thiet.

## Uu tien 5: Input va audit an toan

- Khong tao JSON audit bang string interpolation.
- Dung `JsonSerializer.Serialize(...)` khi ghi `OldValuesJson` va `NewValuesJson`.
- Them validation cho request DTO:
  - do dai chuoi
  - regex
  - min/max
  - enum whitelist
  - required fields
- Kiem soat chat cac free-text fields nhu:
  - rejection reason
  - remarks
  - collection note
- Muc tieu la tranh:
  - malformed JSON
  - log injection
  - payload nguy hiem duoc luu roi render lai o frontend

## Uu tien 6: Authorization theo least privilege

- Ra soat lai role access cho tung endpoint.
- `DashboardController.GetSummary()` hien chi can authenticated, nen bo sung role cu the.
- Kiem tra ky cac endpoint sau:
  - dashboard
  - reports
  - customer detail
  - overdue list
  - approval flows
- Muc tieu la moi endpoint chi mo cho role can thiet nhat.

## Uu tien 7: Van hanh va kiem soat

- Them audit/log cho:
  - login fail
  - lockout
  - admin action
  - security-sensitive config error
- Them monitoring va alert cho:
  - brute force
  - access bat thuong
  - loi auth tang dot bien
- Them security test cho:
  - unauthorized access
  - role bypass
  - login throttling
  - seed bi vo hieu hoa o production
  - app fail fast khi secret thieu hoac yeu

## Goi patch toi thieu nen lam truoc

1. Bo secret hardcode khoi codebase.
2. Chi cho phep seeding demo trong `Development`.
3. Tat Swagger ngoai `Development`.
4. Them rate limiting cho login.
5. Sua audit log sang JSON serialization an toan.

## Ket luan

Neu chi duoc chon mot it viec de lam ngay, hay uu tien:

1. Rotate secret.
2. Tat seed credential mac dinh o production.
3. Them bao ve cho login.
4. Giam PII tra ra.
5. Khoa bt Swagger/production surface.
