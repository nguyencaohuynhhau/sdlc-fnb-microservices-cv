---
id: <YYMMDD>-<slug>
intent: ./intent.md
status: spec
generated_by: /sdlc:spec
guardrails: ../../../AGENTS.md
created: <YYYY-MM-DD>
---

# Spec: <title>

> Sinh từ [intent.md](./intent.md) dưới ràng buộc của [AGENTS.md](../../../AGENTS.md).
> Mọi quyết định kỹ thuật ở đây phải truy ngược được về một tiêu chí chấp nhận trong intent.

## 1. Tóm tắt giải pháp

<3-5 câu: làm gì, ở đâu trong hệ thống, vì sao chọn cách này.>

## 2. Phạm vi ảnh hưởng

| Workspace | Có đổi? | Nội dung |
|-----------|---------|----------|
| `<ws-1>`  |         |          |
| `<ws-2>`  |         |          |

## 3. Thay đổi dữ liệu

### Schema mới / sửa

```ts
// <đường dẫn file định nghĩa schema>
```

| Trường | Kiểu | Bắt buộc | Index | Ghi chú |
|--------|------|----------|-------|---------|
|        |      |          |       |         |

### Di trú dữ liệu

<Dữ liệu cũ xử lý thế nào? Có cần backfill? Có tương thích ngược không?
Nếu KHÔNG cần migration, ghi rõ "Không cần" — đừng để trống.>

## 4. Hợp đồng API

### `<METHOD> /<path>`

- **Auth:** `JwtAuthGuard` + role `<...>` | public (lý do: ...)
- **Request DTO:**

```ts
class XxxDto {
  @IsString() @IsNotEmpty()
  field: string;
}
```

- **Response:**

```json
{ }
```

- **Mã lỗi:**

| HTTP | Khi nào | Thông báo cho người dùng (tiếng Việt) |
|------|---------|----------------------------------------|
| 400  |         |                                        |
| 403  |         |                                        |
| 409  |         |                                        |

## 5. Luồng xử lý

```mermaid
sequenceDiagram
```

## 6. Giao diện

| Màn hình | App | Component | Trạng thái cần xử lý           |
|----------|-----|-----------|--------------------------------|
|          |     |           | loading / empty / error / success |

Tuân theo [DESIGN_SYSTEM.md](../../design/DESIGN_SYSTEM.md). Không token màu/spacing mới.

## 7. Tính toàn vẹn & tương tranh

<Bắt buộc điền nếu chạm tới đơn hàng, tồn kho, thanh toán, voucher.
Nêu rõ thao tác atomic nào được dùng và test nào chứng minh.>

## 8. Rủi ro bảo mật

| Rủi ro | Mức | Giảm thiểu |
|--------|-----|------------|
|        |     |            |

## 9. Chiến lược kiểm thử

| Loại  | File | Ca kiểm thử |
|-------|------|-------------|
| Unit  |      |             |
| E2E   |      |             |
| Thủ công (có ảnh chụp) | | |

## 10. Ánh xạ về tiêu chí chấp nhận

| Tiêu chí trong intent.md | Được đáp ứng bởi | Được chứng minh bởi |
|--------------------------|------------------|---------------------|
| 1.                       |                  |                     |
| 2.                       |                  |                     |

## 11. Điểm chưa quyết

- [ ] <Nếu còn mục nào ở đây, KHÔNG chuyển sang /sdlc:plan.>
