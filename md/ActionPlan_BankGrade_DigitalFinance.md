# Kế hoạch Hành động (Action Plan): Nâng cấp Hệ thống LoanFlow lên Tiêu chuẩn Bank-Grade & Tài chính Số

Tài liệu này vạch ra các định hướng thiết kế và các thay đổi cần thiết để nâng cấp hệ thống LoanFlow (hiện đang chạy dưới dạng monolithic/docker-compose) lên mức **Bank-Grade (Tiêu chuẩn cấp ngân hàng)** và **Tài chính Digital**. Kế hoạch được chia thành các Giai đoạn (Phase) để tiện triển khai.

---

## Giai đoạn 1: Nền tảng Bảo mật & Quản trị Rủi ro (P0)

Mục tiêu giai đoạn này là đảm bảo hệ thống không bị tấn công, thất thoát dữ liệu và có cơ chế kiểm soát chéo nội bộ.

- [ ] **Chuyển đổi Hệ thống Xác thực (Auth)**: Chuyển từ JWT Symmetric sang JWT Asymmetric (RSA/ECDSA) kết hợp với OAuth2.0 / OpenID Connect (OIDC).
- [ ] **Quản lý Secrets Tập trung**: Loại bỏ thông tin nhạy cảm ở `env` / `docker-compose`, tích hợp AWS Secrets Manager hoặc HashiCorp Vault.
- [ ] **Quy trình Maker-Checker (4-eyes principle)**: Áp dụng luồng người tạo - người duyệt độc lập cho tất cả các hành động liên quan đến Giải ngân, Thay đổi thông số, Xóa nợ/Miễn giảm và Thay đổi cấu hình cốt lõi.

---

## Giai đoạn 2: Trục Kế toán & Nghiệp vụ Lõi (P0)

Mục tiêu giai đoạn này là đảm bảo tính toàn vẹn tuyệt đối về mặt con số tài chính, làm cơ sở cho mọi hệ thống báo cáo (GL, Balance Sheet).

- [x] **Hạch Toán Kế toán Kép (Double-entry Ledger)**: Thiết kế và tích hợp Sổ cái (General Ledger - GL) dựa trên hệ thống tài khoản kế toán chuẩn; áp dụng cho mọi giao dịch sinh ra dòng tiền. (Đã khắc phục lỗi bất biến sổ cái cho LateFee và Early Settlement).
- [ ] **Nâng cấp Đầu ngày/Cuối ngày (EOD/BOD)**: Viết lại quy trình EOD đảm bảo tính ACID cao nhất, có cơ chế Transaction Lock trên toàn hệ thống và Rollback tự động khi có bất kỳ sai sót nào lúc chạy batch. Không được để Partial Update xảy ra.
- [ ] **Phân loại Nợ & Trích lập dự phòng Tự động**: Tích hợp quy tắc tự động phân loại nhóm nợ (1-5) và tính toán trích lập rủi ro (ECL) theo IFRS 9 / NHNN.

---

## Giai đoạn 3: Trải nghiệm Tài chính Số & Tự động hóa (P1)

Mục tiêu giai đoạn này là mang lại trải nghiệm Digital Finance xuất sắc trên nền móng Bank-Grade cốt lõi.

- [x] **Tích hợp Định danh điện tử (eKYC)**: Mở rộng `IEkycService` để gọi API đối tác thực tế xử lý OCR, Liveness Detection và Face-matching với ID.
- [x] **Đánh giá Tín dụng Tự động (Rule Engine & Live CIC)**: Nâng cấp `ApprovalRuleService` thành một bộ Rule Engine động. Tích hợp lấy điểm CIC realtime (`ICicService`) để ra quyết định hạn mức tức thời (Straight-Through Processing).
- [x] **Hợp đồng Điện tử & Ký số**: Module sinh File hợp đồng PDF có mã hóa đóng dấu thời gian (Timestamp) và Chữ ký số điện tử (Digital Signature). Có Audit log bảo vệ tính pháp lý của hợp đồng.

---

## Giai đoạn 4: Thanh toán Số & Đối soát (P1)

Tự động hóa luồng tiền (Inflow & Outflow), giảm thiểu sự phụ thuộc vào kế toán thủ công.

- [x] **Giải ngân Tự động (Auto-disbursement)**: Kết nối với Payment Gateway API (vd: NAPAS) để hệ thống tự động bắn tiền ngay khi khế ước vay được active.
- [x] **Tài khoản định danh ảo (Virtual Account - VA)**: Tích hợp thiết lập VA cho từng khoản vay. Nhận Webhook biến động số dư từ ngân hàng đối tác để tự động gạch nợ (Reconciliation) tự động 100%.

---

## Giai đoạn 5: Hạ tầng Cloud & DevSecOps (Pending / On Hold theo yêu cầu)

Nâng cấp khả năng scale của hệ thống để chịu tải khổng lồ và HA (High Availability). Hiện tại Phase này đang Pending để team tập trung làm phần lõi tài chính trước.

- [ ] **Kubernetes & High Availability**: Viết Kubernetes Manifests (Helm Charts) thay thế docker-compose. Cấu hình Load Balancer và Database Multi-AZ.
- [ ] **Observability (Log/Trace/Metrics)**: Tích hợp OpenTelemetry. Build luồng đẩy log tập trung về ELK / Splunk và monitor qua Prometheus/Grafana.
- [ ] **Tự động hóa DevSecOps**: Tích hợp quét mã nguồn SAST và bảo mật Container DAST (Trivy) vào quy trình CI/CD pipelines.

---
*Tài liệu này được xuất tự động vào project root để team dễ lưu trữ và theo dõi. Các trạng thái (Checkbox) sẽ được tích hợp vào tiến độ làm việc thực tế.*
