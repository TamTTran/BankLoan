# LoanFlow Lite - cURL Collection for Postman

Base URL: `http://localhost:5000`
Sau khi login, copy `accessToken` và thay vào biến `{{TOKEN}}` ở tất cả request bên dưới.

---

## 🔐 1. AUTH

### 1.1 Login — Đăng nhập lấy Access Token
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -c cookies.txt \
  -d '{
    "username": "officer01",
    "password": "<SEED_OFFICER_PASSWORD>",
    "deviceId": "postman-device-001",
    "deviceName": "Postman Test"
  }'
```
> Response trả về `data.accessToken` — dùng cho tất cả request tiếp theo.

### 1.2 Login với tài khoản Manager (dùng cho Checker role)
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -c cookies_manager.txt \
  -d '{
    "username": "manager01",
    "password": "<SEED_MANAGER_PASSWORD>",
    "deviceId": "postman-device-002",
    "deviceName": "Postman Manager"
  }'
```

### 1.3 Refresh Token
```bash
curl -X POST http://localhost:5000/api/auth/refresh \
  -b cookies.txt \
  -c cookies.txt
```

### 1.4 Xem Active Sessions
```bash
curl -X GET http://localhost:5000/api/auth/sessions \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 1.5 Logout (session hiện tại)
```bash
curl -X POST http://localhost:5000/api/auth/logout \
  -H "Authorization: Bearer {{TOKEN}}" \
  -b cookies.txt
```

### 1.6 Logout All Sessions
```bash
curl -X POST http://localhost:5000/api/auth/logout-all \
  -H "Authorization: Bearer {{TOKEN}}" \
  -b cookies.txt
```

---

## ✅ 2. APPROVAL FLOW (Maker-Checker) — Phase 1 mới

### 2.1 Maker: Gửi yêu cầu điều chỉnh LateFee (Loan ID = 1)
```bash
curl -X POST http://localhost:5000/api/approval-requests \
  -H "Authorization: Bearer {{OFFICER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "actionType": "ManualAdjustment",
    "payload": "{\"LoanId\":1,\"AdjustmentType\":\"LateFee\",\"Amount\":-150000,\"ReasonCode\":\"CustomerHardship\",\"Note\":\"Mien giam phi tre han theo yeu cau khach hang\"}"
  }'
```

### 2.2 Checker: Xem danh sách pending (cần login Manager)
```bash
curl -X GET http://localhost:5000/api/approval-requests \
  -H "Authorization: Bearer {{MANAGER_TOKEN}}"
```
> Response: `{ "Items": [ {...}, {...} ] }`

### 2.3 Checker: Approve request (id = 1)
```bash
curl -X POST http://localhost:5000/api/approval-requests/1/approve \
  -H "Authorization: Bearer {{MANAGER_TOKEN}}" \
  -H "Content-Type: application/json"
```

### 2.4 Checker: Reject request (id = 2)
```bash
curl -X POST http://localhost:5000/api/approval-requests/2/reject \
  -H "Authorization: Bearer {{MANAGER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "rejectionReason": "Khong du dieu kien mien giam phi theo chinh sach hien hanh"
  }'
```

---

## 👥 3. CUSTOMERS

### 3.1 Lấy danh sách khách hàng
```bash
curl -X GET "http://localhost:5000/api/customers?page=1&pageSize=10" \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 3.2 Lấy chi tiết khách hàng (id = 1)
```bash
curl -X GET http://localhost:5000/api/customers/1 \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 3.3 Tạo khách hàng mới
```bash
curl -X POST http://localhost:5000/api/customers \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Nguyen Van Test",
    "phone": "0909999888",
    "idNumber": "079100009999",
    "address": "123 Test Street, Q1, HCM",
    "dateOfBirth": "1990-06-15"
  }'
```

### 3.4 Cập nhật khách hàng (id = 1)
```bash
curl -X PUT http://localhost:5000/api/customers/1 \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Nguyen Van A Updated",
    "address": "456 New Street, Q3, HCM"
  }'
```

---

## 📋 4. LOAN APPLICATIONS

### 4.1 Lấy danh sách hồ sơ vay
```bash
curl -X GET "http://localhost:5000/api/applications?page=1&pageSize=10" \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 4.2 Tạo hồ sơ vay mới
```bash
curl -X POST http://localhost:5000/api/applications \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 2,
    "productId": 1,
    "requestedAmount": 15000000,
    "requestedTenorMonths": 6,
    "purpose": "Tu sua nha"
  }'
```

### 4.3 Phê duyệt hồ sơ (id = 3) — cần role Approver
```bash
curl -X POST http://localhost:5000/api/applications/3/approve \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "remarks": "Du dieu kien phe duyet"
  }'
```

### 4.4 Từ chối hồ sơ (id = 3)
```bash
curl -X POST http://localhost:5000/api/applications/3/reject \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "remarks": "Thu nhap khong du dieu kien"
  }'
```

---

## 💰 5. LOANS

### 5.1 Lấy danh sách khoản vay
```bash
curl -X GET "http://localhost:5000/api/loans?page=1&pageSize=10" \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.2 Lấy chi tiết khoản vay (id = 1) — bao gồm schedule, payments, late fees
```bash
curl -X GET http://localhost:5000/api/loans/1 \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.3 Tạo khoản vay từ hồ sơ đã duyệt (applicationId = 1)
```bash
curl -X POST http://localhost:5000/api/applications/1/create-loan \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json"
```

### 5.4 Giải ngân khoản vay (loanId = 1)
```bash
curl -X POST http://localhost:5000/api/loans/1/disburse \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: disburse-loan-1-20260419" \
  -d '{
    "disbursedAmount": 20000000,
    "disbursementDate": "2026-04-19"
  }'
```

### 5.5 Xem lịch trả nợ (loanId = 1)
```bash
curl -X GET http://localhost:5000/api/loans/1/schedule \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.6 Ghi nhận thanh toán (loanId = 1)
```bash
curl -X POST http://localhost:5000/api/loans/1/payments \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: pay-loan-1-20260419-001" \
  -d '{
    "paymentDate": "2026-04-19",
    "amount": 1966667,
    "paymentMethod": "BankTransfer",
    "remarks": "Thanh toan ky 2"
  }'
```

### 5.7 Xem lịch sử thanh toán (loanId = 1)
```bash
curl -X GET http://localhost:5000/api/loans/1/payments \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.8 Preview tất toán trước hạn (loanId = 1)
```bash
curl -X GET "http://localhost:5000/api/loans/1/early-settle-preview?settlementDate=2026-05-01" \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.9 Tất toán trước hạn (loanId = 1) — cần role Approver
```bash
curl -X POST http://localhost:5000/api/loans/1/early-settle \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: early-settle-loan-1-20260419" \
  -d '{
    "settlementDate": "2026-05-01",
    "waiveFee": false
  }'
```

### 5.10 Xem danh sách khoản vay quá hạn
```bash
curl -X GET http://localhost:5000/api/overdue \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 5.11 Thêm collection note (loanId = 2 - khoản vay overdue)
```bash
curl -X POST http://localhost:5000/api/loans/2/collection-notes \
  -H "Authorization: Bearer {{COLLECTOR_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "noteDate": "2026-04-19",
    "noteText": "Khach hang da lien he, hua tra vao ngay 25/04"
  }'
```

---

## 🏷️ 6. COLLATERALS

### 6.1 Tạo tài sản thế chấp
```bash
curl -X POST http://localhost:5000/api/collaterals \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "collateralType": "RealEstate",
    "description": "Can ho 2PN tai Binh Thanh",
    "estimatedValue": 2500000000,
    "documentReference": "GCN-2024-001"
  }'
```

### 6.2 Gắn collateral vào khoản vay (loanId = 1, collateralId = 1)
```bash
curl -X POST http://localhost:5000/api/loans/1/collaterals \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "collateralId": 1,
    "allocatedValue": 1500000000
  }'
```

### 6.3 Xem collateral của khoản vay (loanId = 1)
```bash
curl -X GET http://localhost:5000/api/loans/1/collaterals \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 6.4 Gỡ collateral khỏi khoản vay
```bash
curl -X DELETE http://localhost:5000/api/loans/1/collaterals/1 \
  -H "Authorization: Bearer {{TOKEN}}"
```

---

## 💸 7. LATE FEES & SUSPENSE

### 7.1 Waive late fee (feeId = 1) — cần role Approver
```bash
curl -X POST http://localhost:5000/api/late-fees/1/waive \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}"
```

### 7.2 Apply suspense (suspenseId = 1) — Áp dụng tiền thừa
```bash
curl -X POST http://localhost:5000/api/suspense/1/apply \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Idempotency-Key: apply-suspense-1-20260419"
```

---

## ↩️ 8. REVERSALS

### 8.1 Request hoàn đảo giao dịch (transactionId = 1)
```bash
curl -X POST http://localhost:5000/api/reversals \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": 1,
    "reason": "Ghi nham so tien thanh toan"
  }'
```

### 8.2 Approve reversal (reversalId = 1) — cần role Approver
```bash
curl -X POST http://localhost:5000/api/reversals/1/approve \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}"
```

### 8.3 Reject reversal (reversalId = 1)
```bash
curl -X POST http://localhost:5000/api/reversals/1/reject \
  -H "Authorization: Bearer {{APPROVER_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Giao dich hop le, khong duoc phep hoan"
  }'
```

---

## 🔁 9. RECONCILIATION

### 9.1 Chạy reconciliation
```bash
curl -X POST http://localhost:5000/api/reconciliation/run \
  -H "Authorization: Bearer {{TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "businessDate": "2026-04-19"
  }'
```

---

## 📊 10. REPORTS & DASHBOARD

### 10.1 Dashboard tổng quan
```bash
curl -X GET http://localhost:5000/api/dashboard \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 10.2 Portfolio report
```bash
curl -X GET http://localhost:5000/api/reports/portfolio \
  -H "Authorization: Bearer {{TOKEN}}"
```

### 10.3 Overdue movement report
```bash
curl -X GET "http://localhost:5000/api/reports/overdue-movement?fromDate=2026-04-01&toDate=2026-04-19" \
  -H "Authorization: Bearer {{TOKEN}}"
```

---

## ⚙️ 11. ADMIN

### 11.1 Tạo user mới
```bash
curl -X POST http://localhost:5000/api/admin/users \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "newofficer",
    "password": "NewPass@2026!",
    "fullName": "Tran Van New",
    "role": "LoanOfficer"
  }'
```

### 11.2 Lock account user (userId = 3)
```bash
curl -X POST http://localhost:5000/api/admin/users/3/lock \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}"
```

### 11.3 Unlock account user (userId = 3)
```bash
curl -X POST http://localhost:5000/api/admin/users/3/unlock \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}"
```

### 11.4 Xem danh sách active sessions của user (userId = 2)
```bash
curl -X GET http://localhost:5000/api/admin/users/2/sessions \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}"
```

### 11.5 Force logout user (userId = 2)
```bash
curl -X POST http://localhost:5000/api/admin/users/2/force-logout \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}"
```

---

## 🔔 12. NOTIFICATIONS

### 12.1 Xem notifications
```bash
curl -X GET http://localhost:5000/api/notifications \
  -H "Authorization: Bearer {{TOKEN}}"
```

---

## 🏃 13. JOBS (Manual trigger)

### 13.1 Trigger End-of-Day job thủ công
```bash
curl -X POST http://localhost:5000/api/jobs/end-of-day \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "businessDate": "2026-04-19"
  }'
```

### 13.2 Trigger Overdue job thủ công
```bash
curl -X POST http://localhost:5000/api/jobs/overdue \
  -H "Authorization: Bearer {{ADMIN_TOKEN}}"
```

---

## 🔢 Thứ tự chạy test Phase 1 (Maker-Checker flow đầy đủ)

### Step 1: Login 2 users
```bash
# Login Maker (Officer)
TOKEN_OFFICER=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -c cookies_officer.txt \
  -d '{"username":"officer01","password":"<PASSWORD>","deviceId":"d1"}' \
  | jq -r '.data.accessToken')

# Login Checker (Manager)
TOKEN_MANAGER=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -c cookies_manager.txt \
  -d '{"username":"manager01","password":"<PASSWORD>","deviceId":"d2"}' \
  | jq -r '.data.accessToken')
```

### Step 2: Maker gửi request
```bash
curl -X POST http://localhost:5000/api/approval-requests \
  -H "Authorization: Bearer $TOKEN_OFFICER" \
  -H "Content-Type: application/json" \
  -d '{
    "actionType": "ManualAdjustment",
    "payload": "{\"LoanId\":1,\"AdjustmentType\":\"Interest\",\"Amount\":-500000,\"ReasonCode\":\"SystemError\",\"Note\":\"Dieu chinh lai sai\"}"
  }'
```

### Step 3: Checker xem queue
```bash
curl -X GET http://localhost:5000/api/approval-requests \
  -H "Authorization: Bearer $TOKEN_MANAGER"
```

### Step 4: Checker approve (id từ step 2)
```bash
curl -X POST http://localhost:5000/api/approval-requests/4/approve \
  -H "Authorization: Bearer $TOKEN_MANAGER"
```

### Step 5: Xác nhận loan đã thay đổi Outstanding
```bash
curl -X GET http://localhost:5000/api/loans/1 \
  -H "Authorization: Bearer $TOKEN_OFFICER"
```

---

## 📝 Lưu ý

| Role | Username | Dùng cho |
|---|---|---|
| Admin | `admin` | Lock/unlock user, admin ops |
| LoanOfficer | `officer01` | Tạo hồ sơ, giải ngân, ghi payment — **Maker** |
| Approver | `approver01` | Phê duyệt hồ sơ, early settle |
| Collector | `collector01` | Collection notes, view overdue |
| Manager | `manager01` | Duyệt approval queue — **Checker** |

> **Idempotency-Key**: Các endpoint POST tài chính (`/disburse`, `/payments`, `/early-settle`, `/suspense/{id}/apply`) bắt buộc phải có header `Idempotency-Key` để tránh double-posting.
> Password của từng user được in ra console khi app khởi động lần đầu (seeding).
