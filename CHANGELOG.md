# Changelog

> Mục mới được ghi ở **bước 5 của `/sdlc:build`**, trước khi bàn giao sang `/sdlc:verify` —
> xem [docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md). Mục mới nhất ở trên cùng.

## [2026-09-25] — Lát A: POS gọi món, bếp nhận đơn realtime, ca làm việc, trên 3 microservice sau một gateway
### Added
- **backend:** `identity` đăng nhập JWT (access 60 phút, refresh token xoay vòng; dùng lại token cũ thu hồi cả chuỗi; sai 10 lần khoá 15 phút qua Redis).
- **backend:** `ordering` thực đơn (cache Redis), tạo đơn, thêm/huỷ món, bếp chuyển `Chờ → Đang làm → Xong`; mọi lệnh ghi lên đơn cần `If-Match`, lệch phiên bản → 409 tiếng Việt; hub SignalR `/hubs/orders` đẩy đơn theo ca.
- **backend:** `cashier` mở/đóng ca; "chỉ một ca mở" giữ bằng unique index ở DB. Ordering biết ca đang mở qua sự kiện Kafka (outbox → inbox), không gọi đồng bộ.
- **backend:** gateway YARP là cửa vào duy nhất: route, JWT, CORS allowlist, token query chỉ nhận ở `/hubs`, `/healthz` gộp 3 dịch vụ (503 khi một dịch vụ chết).
- **backend:** seeder dữ liệu demo chạy lại bao lần cũng được, từ chối chạy ngoài `Development`.
- **web:** đăng nhập, POS (thanh ca, thực đơn có cờ "Hết", đơn nháp, đơn đang mở), bảng bếp 3 cột realtime, banner mất kết nối khoá mọi nút ghi và tự hồi phục không cần tải lại trang.
- **infra:** `docker compose up` dựng 8 container healthy; chỉ gateway, web và Postgres (cho seeder) mở cổng, đều bind `127.0.0.1`.
- **Tests:** +81 test — 67 backend (unit, Testcontainers Postgres/Kafka/Redis, gateway với dịch vụ giả), 11 vitest, 3 Playwright trên stack thật. Toàn bộ **81/81 pass**. 4 lần đo POS → bếp 534–1253ms, bếp "Xong" → POS 132–169ms (ngân sách 2000ms, `a-latency.log`).

### Notes
- Sửa spec khi build: `current_shift` 0–1 dòng thành `known_shifts` (hai topic ca có thể tới ngược thứ tự, giữ một dòng sẽ sinh ca ma); index "một ca mở" phải có `NULLS NOT DISTINCT`, bản như spec ghi không chặn gì.
- Tiền ban đầu lỡ tạo `numeric(14,2)`; thêm migration `MoneyPrecision18` đưa về `numeric(18,2)` như spec, có test soi `information_schema`.
- Route gateway khai báo trong `Program.cs` thay vì `appsettings.json` để test gateway chạy không cần file cấu hình.
- Cố ý chưa làm: thanh toán + idempotency + gRPC (lát B), tồn kho (lát C), báo cáo CQRS (lát D), OpenAPI. Không có dark mode.
- 4 devDependency web ngoài danh sách đã duyệt (`@types/react`, `@types/react-dom`, `@types/node`, `@testing-library/dom`) đang chờ người điều phối duyệt.
- Intent: `docs/intents/01-260925-fnb-pos-core/`

