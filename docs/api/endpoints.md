# Hợp đồng API — lát A + B

Nguồn gốc: `docs/intents/01-260925-fnb-pos-core/spec.md` §4. File này cụ thể hoá phần lát A và B
tới mức hình dạng JSON, để backend và web xây song song mà không lệch nhau.

**Mọi request đi qua gateway `http://localhost:8080`** (web ở `http://localhost:5173` gọi cùng origin, nginx proxy `/api`, `/healthz`, `/hubs` sang gateway). Ba dịch vụ không publish cổng ra host. JSON camelCase. Tiền là số VND
(`decimal`, không phần lẻ trong dữ liệu demo). Thời gian là chuỗi ISO-8601 UTC.

## Quy ước chung

- Mặc định mọi endpoint cần `Authorization: Bearer <accessToken>`. Public duy nhất:
  `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /healthz`.
- Lỗi luôn là `application/problem+json`:
  `{ "type", "title", "status", "detail" }` — `detail` là câu tiếng Việt hiển thị thẳng cho người dùng.
  Lỗi validation 400 có thêm `errors: { field: [msg] }`.
- Vai trò: `Owner`, `Cashier`, `Kitchen`. Sai vai trò → `403`.

### Phiên bản đơn (optimistic concurrency)

- Mỗi `Order` trả về có trường `version` (số nguyên, là `xmin` của PostgreSQL) và header
  `ETag: "<version>"` khi trả một đơn.
- Mọi lệnh **ghi lên một đơn đã có** phải gửi `If-Match: "<version>"` (chấp nhận cả không ngoặc kép).
  - Thiếu header → `428` "Thiếu phiên bản đơn. Tải lại rồi thử lại nhé."
  - Lệch phiên bản → `409` "Đơn vừa được người khác cập nhật. Tải lại rồi thử lại nhé."

### Kiểu dữ liệu

```ts
type Role = 'Owner' | 'Cashier' | 'Kitchen';
type OrderStatus = 'Open' | 'Paid' | 'Cancelled';
type OrderItemStatus = 'Pending' | 'Preparing' | 'Done' | 'Cancelled';

type AuthResponse = { accessToken: string; refreshToken: string; role: Role; expiresIn: number };
type MenuItem   = { id: string; name: string; price: number; isAvailable: boolean };
type Shift      = { id: string; openedBy: string; openedAt: string; closedAt: string | null;
                    openingFloat: number; countedCash: number | null; expectedCash: number | null;
                    variance: number | null };
type CurrentShift = { shiftId: string; openedAt: string };
type OrderItem  = { id: string; menuItemId: string; name: string; unitPrice: number; qty: number;
                    status: OrderItemStatus };
type Order      = { id: string; code: number; shiftId: string; status: OrderStatus; total: number;
                    createdAt: string; paidAt: string | null; version: number; items: OrderItem[] };

type PaymentMethod  = 'Cash' | 'Transfer';
type PaymentReceipt = { paymentId: string; orderId: string; shiftId: string; amount: number;
                        method: PaymentMethod; paidAt: string };
type ShiftSummary   = Shift & { orderCount: number; revenue: number; cashTotal: number;
                                transferTotal: number };
```

`total` = tổng `unitPrice × qty` của các món **không** `Cancelled`.

## identity

| Method | Path | Vai trò | Body | 2xx | Lỗi |
|--------|------|---------|------|-----|-----|
| POST | `/api/auth/login` | public | `{ username: 3–50, password: 8–100 }` | `200 AuthResponse` | `401` "Tên đăng nhập hoặc mật khẩu không đúng." · `423` "Tài khoản tạm khoá 15 phút vì đăng nhập sai quá nhiều lần." |
| POST | `/api/auth/refresh` | public | `{ refreshToken }` | `200 AuthResponse` (refresh token **mới**; cái cũ hết hiệu lực) | `401` "Phiên đăng nhập đã hết hạn. Đăng nhập lại nhé." — gửi lại token đã dùng thì thu hồi cả chuỗi |

Access token 60 phút (`expiresIn: 3600`). Refresh token 12 giờ. Claim: `sub` (userId),
`unique_name` (username), `role`.

## cashier

| Method | Path | Vai trò | Body | 2xx | Lỗi |
|--------|------|---------|------|-----|-----|
| GET | `/api/shifts/current` | mọi vai trò | — | `200 Shift` · `204` khi không có ca mở | |
| POST | `/api/shifts/open` | Cashier, Owner | `{ openingFloat: 0–1e9 }` | `201 Shift` | `409` "Đang có ca mở. Đóng ca hiện tại trước." |
| POST | `/api/shifts/{id}/close` | Cashier, Owner | `{ countedCash: 0–1e9 }` (bắt buộc) | `200 ShiftSummary` | `404` "Không tìm thấy ca." · `409` "Ca này đã đóng." · `400` thiếu `countedCash` |
| POST | `/api/payments` | Cashier, Owner | `{ orderId, expectedTotal: 0.01–1e9, method: PaymentMethod }` + header `Idempotency-Key: <uuid>` | `200 PaymentReceipt` | xem bảng dưới |

### Đóng ca — đếm mù

Thu ngân nhập tiền đếm được **trước** khi thấy số dự kiến. Server khoá ca (`FOR UPDATE`), cộng các
payment của ca rồi trả:

- `expectedCash = openingFloat + cashTotal`, `variance = countedCash − expectedCash` (âm = thiếu).
- `orderCount`, `revenue = cashTotal + transferTotal` — chỉ tính payment `Completed`.

Thu tiền giữ ca bằng `FOR SHARE` suốt transaction, nên đóng ca chen vào sẽ **chờ** payment đang chạy
commit xong rồi mới cộng — không có payment nào lọt ra sau khi ca đã đóng.

### Thu tiền — idempotency

- POS sinh `Idempotency-Key` (UUID) **một lần mỗi lần mở form thu**; bấm lại, mất mạng, retry sau
  refresh 401 đều gửi **cùng** key. Đóng form rồi mở lại = key mới.
- Cùng key + cùng body → trả **nguyên văn** phản hồi 200 lần đầu, không ghi bút toán thứ hai. Body
  được băm sau khi chuẩn hoá tiền về 2 số lẻ (`45000` ≡ `45000.00`).
- Chỉ lần **thành công** được lưu. Lỗi ở bất kỳ bước nào rollback cả key → bấm lại cùng key là chạy lại từ đầu.
- Server không log giá trị key.

| Status | `detail` | Khi nào |
|--------|----------|---------|
| `400` | "Yêu cầu không hợp lệ." | thiếu `Idempotency-Key` hoặc không phải UUID |
| `400` | "Dữ liệu gửi lên không hợp lệ." + `errors` | body sai (thiếu trường, `expectedTotal` ngoài khoảng, `method` lạ) |
| `404` | "Không tìm thấy đơn." | |
| `409` | "Chưa mở ca làm việc." | không có ca mở |
| `409` | "Đơn vừa thay đổi, tổng tiền hiện tại là {tổng}đ. Kiểm tra lại rồi thu tiền." | `expectedTotal` lệch tổng thật (có người thêm/huỷ món) |
| `409` | "Đơn này đã được thanh toán." | đơn đã thu bằng key khác |
| `409` | "Đơn đã bị huỷ, không thu tiền được." | |
| `422` | "Yêu cầu không khớp với lần gửi trước." | cùng key, body khác |
| `503` | "Không kết nối được dịch vụ đơn hàng. Thử lại sau giây lát." | gRPC tới ordering lỗi / quá 3s / ordering hết lượt thử lại vì xung đột. Không có gì được ghi |

## ordering

| Method | Path | Vai trò | Body | 2xx | Lỗi |
|--------|------|---------|------|-----|-----|
| GET | `/api/menu` | Cashier, Owner | — | `200 MenuItem[]` (cache Redis `menu:v1`, 60s) | |
| GET | `/api/orders/current-shift` | mọi vai trò | — | `200 CurrentShift` · `204` khi ordering chưa biết ca nào mở | |
| GET | `/api/orders?active=true` | Cashier, Owner | — | `200 Order[]` của ca hiện hành, mới nhất trước. `active=true`: đơn `Open`, **hoặc** `Paid` mà bếp còn món `Pending`/`Preparing`. `?status=` vẫn dùng được | |
| GET | `/api/orders/{id}` | Cashier, Owner, Kitchen | — | `200 Order` + `ETag` | `404` "Không tìm thấy đơn." |
| POST | `/api/orders` | Cashier, Owner | `{ items: [{ menuItemId, qty: 1–99 }] }` (1–100 dòng) | `201 Order` + `ETag` | `409` "Chưa mở ca làm việc. Mở ca trước khi nhận đơn." · `409` "Món {tên} vừa hết hàng, vui lòng bỏ khỏi đơn." · `400` "Món không có trong thực đơn." |
| POST | `/api/orders/{id}/items` | Cashier, Owner | `{ menuItemId, qty: 1–99 }` + `If-Match` | `200 Order` | `409` "Đơn đã đóng, không thêm món được." · như trên |
| DELETE | `/api/orders/{id}/items/{itemId}` | Cashier, Owner | `If-Match` | `200 Order` (món → `Cancelled`) | `409` "Món này bếp đã làm, cần bếp xác nhận mới huỷ được." · `409` "Đơn đã đóng, không sửa được nữa." · `404` "Không tìm thấy món trong đơn." |
| POST | `/api/orders/{id}/cancel` | Cashier, Owner | `If-Match` | `200 Order` (→ `Cancelled`) | `409` "Chỉ huỷ được đơn chưa thanh toán." |
| PATCH | `/api/orders/{id}/items/{itemId}/status` | Kitchen, Owner | `{ status: 'Preparing' \| 'Done' }` + `If-Match` | `200 Order` | `409` "Món phải chuyển lần lượt Chờ → Đang làm → Xong." · `409` "Đơn đã đóng, không sửa được nữa." · `404` "Không tìm thấy món trong đơn." |
| GET | `/api/kitchen/orders` | Kitchen, Owner | — | `200 Order[]` đơn còn việc của ca hiện hành (cùng phạm vi `active`), cũ nhất trước — thu tiền trước khi bếp xong thì đơn vẫn ở bảng bếp | |

## SignalR — `/hubs/orders`

- Kết nối: `/hubs/orders?access_token=<accessToken>` (trình duyệt không đặt được header cho WebSocket).
- Sau khi kết nối, client gọi `JoinShift(shiftId)` để vào group `shift:{shiftId}`.
- Sự kiện server → client, payload đều là `Order` đầy đủ:
  `orderCreated`, `orderUpdated` (thêm/huỷ món, **đã thu tiền**), `orderItemStatusChanged`, `orderCancelled`.

## gRPC nội bộ — `OrderPayments.MarkPaid`

Đường gọi đồng bộ duy nhất giữa các dịch vụ: cashier → ordering, cổng `8092` (HTTP/2 cleartext,
**chỉ** trong mạng Docker, không publish). Hợp đồng: `backend/src/Shared/Protos/order_payments.proto`.

- Cashier chuyển tiếp `Authorization` của thu ngân; ordering đòi vai trò Cashier/Owner.
- Tiền là **chuỗi** thập phân invariant (`"45000.00"`) — protobuf không có decimal.
- `payment_id` = `Idempotency-Key`: gọi lại cùng id sau khi đã thu → `ok`; id khác → `already_paid`.
- Kết quả `oneof`: `ok` · `total_mismatch { actual_total }` · `already_paid` · `order_cancelled`; đơn không tồn tại → status `NOT_FOUND`.
- Ordering ghi `paid_at`, `paid_payment_id` và outbox `fnb.ordering.order-paid.v1` trong **một** transaction; xung đột `xmin` (bếp vừa đổi món) → đọc lại, kiểm lại tổng, thử tối đa 3 lần rồi `ABORTED`.

## Hạ tầng

| Method | Path | Vai trò | 2xx | Lỗi |
|--------|------|---------|-----|-----|
| GET | `/healthz` | public | `200 { identity, ordering, cashier: "ok" }` — gateway gọi `/healthz` của từng dịch vụ (timeout 2s) | `503` cùng hình dạng, dịch vụ chết mang giá trị `"down"` |
