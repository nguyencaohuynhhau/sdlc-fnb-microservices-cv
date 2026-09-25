# Hợp đồng API — lát A

Nguồn gốc: `docs/intents/01-260925-fnb-pos-core/spec.md` §4. File này cụ thể hoá phần lát A
tới mức hình dạng JSON, để backend và web xây song song mà không lệch nhau.

**Mọi request đi qua gateway `http://localhost:8080`.** JSON camelCase. Tiền là số VND
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
                    createdAt: string; version: number; items: OrderItem[] };
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
| POST | `/api/shifts/{id}/close` | Cashier, Owner | — | `200 Shift` | `404` "Không tìm thấy ca." · `409` "Ca này đã đóng." |

Lát A: `countedCash`/`expectedCash`/`variance` luôn `null`; lát B thêm body `{ countedCash }`.

## ordering

| Method | Path | Vai trò | Body | 2xx | Lỗi |
|--------|------|---------|------|-----|-----|
| GET | `/api/menu` | Cashier, Owner | — | `200 MenuItem[]` (cache Redis `menu:v1`, 60s) | |
| GET | `/api/orders/current-shift` | mọi vai trò | — | `200 CurrentShift` · `204` khi ordering chưa biết ca nào mở | |
| GET | `/api/orders?status=Open` | Cashier, Owner | — | `200 Order[]` của ca hiện hành, mới nhất trước; `status` tuỳ chọn | |
| GET | `/api/orders/{id}` | Cashier, Owner, Kitchen | — | `200 Order` + `ETag` | `404` "Không tìm thấy đơn." |
| POST | `/api/orders` | Cashier, Owner | `{ items: [{ menuItemId, qty: 1–99 }] }` (1–100 dòng) | `201 Order` + `ETag` | `409` "Chưa mở ca làm việc. Mở ca trước khi nhận đơn." · `409` "Món {tên} vừa hết hàng, vui lòng bỏ khỏi đơn." · `400` "Món không có trong thực đơn." |
| POST | `/api/orders/{id}/items` | Cashier, Owner | `{ menuItemId, qty: 1–99 }` + `If-Match` | `200 Order` | `409` "Đơn đã đóng, không thêm món được." · như trên |
| DELETE | `/api/orders/{id}/items/{itemId}` | Cashier, Owner | `If-Match` | `200 Order` (món → `Cancelled`) | `409` "Món này bếp đã làm, cần bếp xác nhận mới huỷ được." |
| POST | `/api/orders/{id}/cancel` | Cashier, Owner | `If-Match` | `200 Order` (→ `Cancelled`) | `409` "Chỉ huỷ được đơn chưa thanh toán." |
| PATCH | `/api/orders/{id}/items/{itemId}/status` | Kitchen, Owner | `{ status: 'Preparing' \| 'Done' }` + `If-Match` | `200 Order` | `409` "Món phải chuyển lần lượt Chờ → Đang làm → Xong." |
| GET | `/api/kitchen/orders` | Kitchen, Owner | — | `200 Order[]` đơn `Open` của ca hiện hành, cũ nhất trước | |

## SignalR — `/hubs/orders`

- Kết nối: `/hubs/orders?access_token=<accessToken>` (trình duyệt không đặt được header cho WebSocket).
- Sau khi kết nối, client gọi `JoinShift(shiftId)` để vào group `shift:{shiftId}`.
- Sự kiện server → client, payload đều là `Order` đầy đủ:
  `orderCreated`, `orderUpdated` (thêm/huỷ món), `orderItemStatusChanged`, `orderCancelled`.

## Hạ tầng

| Method | Path | Vai trò | 2xx | Lỗi |
|--------|------|---------|-----|-----|
| GET | `/healthz` | public | `200 "ok"` khi gateway **và** identity, ordering, cashier đều sống | `503` |
