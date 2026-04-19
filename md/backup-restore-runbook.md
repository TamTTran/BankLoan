# Runbook: Backup & Restore

Date: 2026-04-19
Owner: DevOps Team
Target: LoanFlow Lite Production Database

> [!WARNING]
> Luôn thực hiện Backup trước bất kỳ quá trình Maintainance/Restore nào để tránh mất mát dữ liệu do sai thao tác. Hệ thống phải đảm bảo Zero Data Loss đối với dữ liệu Tài chính (Financial Records). 

---

## 1. Backup Thủ công (Manual Full Backup)

### Tiền quyết
- Các tools đã có trong Path: `mysqldump`.
- Database user có quyền: `SELECT`, `SHOW VIEW`, `TRIGGER`, `LOCK TABLES`.

### Thực thi
Thực thi script đã được tạo bằng PowerShell:
```powershell
.\scripts\backup.ps1 -HostName 127.0.0.1 -Port 3306 -User loanflow -Password "LoanFlow@2026!" -Database LoanFlowLite
```
Kết quả dự kiến: MySQL Dump xuất file `<db_name>_backup_YYYYMMDD_HHMMSS.sql` lưu trong thư mục `backups`.

---

## 2. Restore Từ Bản Sao (Manual Restore)

### Tiền quyết
- File SQL Backup hợp lệ (không dính lỗi BOM).
- Các job liên quan (`EndOfDayService`) và Client API đang off để ngăn Data Overwrite giữa chừng.

### Thực thi
Thực thi script phục hồi:
```powershell
.\scripts\restore.ps1 -BackupFile "D:\Project\BankLoan\backups\LoanFlowLite_backup_20260419_120000.sql" -HostName 127.0.0.1 -Port 3306 -User loanflow -Password "LoanFlow@2026!" -Database LoanFlowLite
```

Script sẽ hỏi `Are you sure you want to proceed? (y/N)`. Nhập `y` để hệ thống drop & inject data.

> [!CAUTION]
> Quá trình restore có thể kéo dài tùy dung lượng (Data Size). TUYỆT ĐỐI không tắt terminal trong khi script đang nhả log executing.
