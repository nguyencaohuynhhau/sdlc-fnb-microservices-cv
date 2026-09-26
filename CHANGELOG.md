# Changelog

> Mục mới được ghi ở **bước 5 của `/sdlc:build`**, trước khi bàn giao sang `/sdlc:verify` —
> xem [docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md). Mục mới nhất ở trên cùng.

## [2026-09-25] — Lát B: thu tiền idempotent, gRPC MarkPaid, đếm két đối chiếu khi đóng ca
### Added
- **backend:** `cashier` thu tiền (`POST /api/payments`, tiền mặt/chuyển khoản) với `Idempotency-Key` bắt buộc: giữ key, khoá ca, gọi ordering, ghi bút toán trong **một** transaction. Gửi lại cùng key trả nguyên văn phản hồi lần đầu, không ghi bút toán thứ hai; cùng key khác body → 422. Tổng tiền đơn vừa đổi → 409 nêu tổng thật; ordering không trả lời → 503, không ghi gì.
- **backend:** gRPC nội bộ `OrderPayments.MarkPaid` cashier → ordering (cổng 8092, không publish). Ordering kiểm lại tổng, ghi `paid_at` + outbox `fnb.ordering.order-paid.v1` cùng transaction; bếp vừa đổi món (xung đột `xmin`) thì đọc lại và thử lại tối đa 3 lần. Idempotent theo `paymentId = Idempotency-Key`: mất phản hồi rồi bấm lại vẫn thành công.
- **backend:** đóng ca nhận `countedCash` (đếm mù) và trả tổng kết: số đơn đã thu, doanh thu, tiền mặt/chuyển khoản, tiền mặt dự kiến, lệch. Đóng ca chờ mọi payment đang chạy commit xong rồi mới cộng.
- **backend:** đơn đã thu mà bếp chưa xong vẫn ở POS và bảng bếp (`?active=true`); seeder thêm ca hôm qua đã đóng với 6 đơn đã thu.
- **web:** nút "Thu tiền" trên thẻ đơn → chọn phương thức → "Xác nhận thu {tổng}" → biên nhận. Khoá idempotency sinh một lần mỗi lần mở form, bấm lại hay retry sau refresh 401 dùng cùng khoá. Mất mạng → nút xác nhận bị khoá.
- **web:** "Đóng ca" mở form đếm tiền trong két, cảnh báo (không chặn) khi còn đơn chưa thu, rồi hiện tổng kết với "Khớp tiền / Thiếu / Thừa".
- **Tests:** +38 test — 31 backend (idempotency song song, lost reply, gRPC thật qua TestServer, khoá ca khi đóng chen payment), 5 vitest, 2 Playwright trên stack thật. Toàn bộ **119/119 pass**. 4 lần đo "Xác nhận thu" → biên nhận với đơn 20 phần/10 dòng: 201–949ms (ngân sách 2000ms, `b-latency.log`).

### Notes
- Chỉ bút toán `Completed` được lưu — spec có `Pending`/`Failed`, nhưng dòng được INSERT sau khi `MarkPaid` trả `ok` trong cùng transaction nên lỗi nào cũng rollback sạch; cột `status` và unique index partial giữ lại làm chốt chặn thứ hai chống thu hai lần.
- `idempotency_keys.response_body` là `text`, không `jsonb` như plan: `jsonb` sắp lại khoá, phản hồi gửi lại sẽ không còn nguyên văn.
- Mất phản hồi gRPC sau khi ordering đã ghi `Paid` → cashier rollback; thu ngân bấm lại cùng key là khép lại. Nếu họ đóng form (key mới) thì đơn `Paid` không có bút toán — lát D đối chiếu `OrderPaid` với `payments` để phát hiện. Server không tự retry gRPC: retry là người dùng bấm lại cùng key.
- Tiền qua protobuf là chuỗi thập phân invariant (`"45000.00"`), không `double`.
- Playwright chạy `workers: 1`: các spec dùng chung một ca thật và `order-to-payment` đóng ca giữa chừng.
- Đóng ca khi còn đơn chưa thu chỉ cảnh báo ở UI, server không chặn. Không có sự kiện Kafka riêng cho payment: lát D lấy tổng theo phương thức từ `ShiftClosed` (đã thêm `countedCash`/`expectedCash`/`variance`).
- Cố ý chưa làm: tồn kho/trừ kho theo `OrderPaid` (lát C), báo cáo + đối soát (lát D), hoàn tiền, huỷ thanh toán, tách bill, cổng thanh toán thật, in hoá đơn, nhập quỹ đầu ca trên UI.
- Intent: `docs/intents/01-260925-fnb-pos-core/`

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

