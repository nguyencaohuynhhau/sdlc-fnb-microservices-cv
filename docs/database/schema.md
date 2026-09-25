# Schema — lát A

Nguồn: spec `docs/intents/01-260925-fnb-pos-core/spec.md` §3, **đối chiếu với migration thật**
(`backend/src/*/*.Infrastructure/Migrations/`). Chỗ nào lệch spec có ghi lý do.

Một container PostgreSQL 17, **mỗi dịch vụ một database**: `fnb_identity`, `fnb_ordering`, `fnb_cashier`
(`fnb_inventory`, `fnb_reporting` đã tạo sẵn, bảng thuộc lát sau). Không connection string nào trỏ sang
database của dịch vụ khác. Tên bảng/cột snake_case (`UseSnakeCaseNames()` trong `Shared.Messaging`).

- **Tiền:** `numeric(18,2)`, `decimal` trong C#. Test `MoneyColumns_AreNumeric18_2` soi `information_schema` sau migrate.
- **Thời gian:** `timestamptz`, lưu UTC.
- **Tương tranh:** cột hệ thống `xmin` làm concurrency token (không có cột `version` tự thêm). API trả nó thành `version`/`ETag`.
- **Migration:** chạy lúc khởi động dịch vụ khi `ASPNETCORE_ENVIRONMENT=Development`. Dữ liệu demo do `backend/tools/Seeder` nạp, không nằm trong migration.

## `fnb_identity`

| Bảng | Cột | Index | Ghi chú |
|------|-----|-------|---------|
| `users` | `id`, `username` varchar(50), `password_hash`, `role` varchar(20), `is_active`, `xmin` | unique `ix_users_username` | Hash bằng `PasswordHasher<T>` (PBKDF2) |
| `refresh_tokens` | `id`, `user_id`, `token_hash` varchar(64), `expires_at`, `revoked_at`, `replaced_by`, `xmin` | unique `(token_hash)`, `(user_id)` | Chỉ lưu SHA-256 của token. Dùng lại token đã xoay → thu hồi cả chuỗi |

Đếm đăng nhập sai nằm ở Redis (khoá 15 phút sau 10 lần), không có bảng.

## `fnb_ordering`

| Bảng | Cột | Index | Ghi chú |
|------|-----|-------|---------|
| `menu_items` | `id`, `name` varchar(100), `price`, `is_active`, `is_available` | `(is_active)` | Đọc qua cache Redis `menu:v1` (60s) |
| `orders` | `id`, `code` int, `shift_id`, `status` varchar(20), `total`, `created_at`, `updated_at`, `xmin` | unique `(code)`, `(shift_id)`, `(status)` | `updated_at` đổi ở mọi lần sửa món → dòng `orders` luôn bị UPDATE → `xmin` đổi. `paid_at`/`paid_payment_id` thuộc lát B |
| `order_items` | `id`, `order_id`, `line`, `menu_item_id`, `name`, `unit_price`, `qty`, `status` varchar(20) | `(order_id)` | Tên và giá **chụp lại** lúc gọi món. `Pending → Preparing → Done`, hoặc `Cancelled` |
| `known_shifts` | `shift_id` (pk), `opened_at`, `closed_at` | — | Hình chiếu ca từ sự kiện của cashier. **Lệch spec:** spec ghi `current_shift` 0–1 dòng; `ShiftOpened`/`ShiftClosed` đi hai topic nên có thể tới ngược thứ tự — giữ mọi ca để "đóng" tới trước "mở" không sinh ca ma |
| `outbox_message` | `id`, `type`, `topic`, `key`, `payload` jsonb, `occurred_at`, `published_at` | `(occurred_at) WHERE published_at IS NULL` | Ghi cùng transaction nghiệp vụ; publisher nền quét dòng chưa gửi, kể cả lúc khởi động lại |
| `inbox_message` | `message_id` (pk), `handled_at` | pk | Khử sự kiện Kafka trùng |

## `fnb_cashier`

| Bảng | Cột | Index | Ghi chú |
|------|-----|-------|---------|
| `shifts` | `id`, `opened_by` varchar(50), `opened_at`, `closed_at`, `opening_float`, `counted_cash`, `expected_cash`, `variance`, `xmin` | `ux_shifts_single_open`: unique `(closed_at) NULLS NOT DISTINCT WHERE closed_at IS NULL`; `(closed_at)` | **Chỉ một ca mở** — ràng buộc ở DB. **Lệch spec:** phải có `NULLS NOT DISTINCT`, vì Postgres mặc định coi các NULL là khác nhau và index như spec ghi không chặn gì |
| `outbox_message` / `inbox_message` | như ordering | | Phát `ShiftOpened`, `ShiftClosed` |

`payments`, `idempotency_keys` thuộc lát B.

## Topic Kafka (lát A)

| Topic | Phát | Nhận |
|-------|------|------|
| `fnb.cashier.shift-opened.v1` | cashier | ordering → `known_shifts` |
| `fnb.cashier.shift-closed.v1` | cashier | ordering → `known_shifts` |
| `fnb.ordering.order-cancelled.v1` | ordering | (lát B: inventory hoàn kho) |
