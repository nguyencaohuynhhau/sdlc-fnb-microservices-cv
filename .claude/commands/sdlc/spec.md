---
description: 2️⃣ intent.md → spec.md (đặc tả kỹ thuật)
---

# /sdlc:spec — Biên dịch ý định thành đặc tả

Tham số: `<intent-id>` (ví dụ `/sdlc:spec 260902-split-bill`).
Nếu không truyền, liệt kê các intent có `status: approved` và hỏi chọn.

## Cổng vào — kiểm tra trước khi làm bất cứ gì

1. `docs/intents/<id>/intent.md` tồn tại.
2. `status: approved`. Nếu vẫn là `draft` → **dừng**, báo:
   *"Intent này chưa được duyệt. Anh/chị đọc lại và đổi status thành `approved` trước nhé."*
3. Mục "Câu hỏi còn treo" không còn dòng `- [ ]` nào chưa đánh dấu. Nếu còn → **dừng**, liệt kê ra.

Không tự ý duyệt hộ. Không "giả định là đã duyệt".

## Bước 1 — Nạp rào cản

Đọc theo đúng thứ tự:

1. [AGENTS.md](../../../AGENTS.md) — ràng buộc kỹ thuật + chính sách bảo mật
2. [docs/database/schema.md](../../../docs/database/schema.md)
3. [docs/api/endpoints.md](../../../docs/api/endpoints.md)
4. [docs/design/DESIGN_SYSTEM.md](../../../docs/design/DESIGN_SYSTEM.md)

Rồi khảo sát code thật ở các module liên quan — **đừng thiết kế dựa trên trí nhớ về schema**.
Với thiết kế DB/API sâu hoặc mockup UI, gọi lại bộ lệnh chuyên dụng của dự án nếu có.

## Bước 2 — Viết spec.md

Copy [_templates/spec.template.md](../../../docs/intents/_templates/spec.template.md) thành
`docs/intents/<id>/spec.md` và điền đủ. Kỷ luật:

- **Mục 10 (ánh xạ) là bắt buộc.** Mỗi tiêu chí chấp nhận trong `intent.md` phải có đúng một
  dòng, chỉ ra thành phần kỹ thuật nào đáp ứng và test nào chứng minh. Tiêu chí không ánh xạ
  được → hoặc bạn thiếu thiết kế, hoặc tiêu chí mơ hồ; nêu ra thay vì lấp liếm.
- **Mục 7 (toàn vẹn & tương tranh)** bắt buộc điền nếu chạm orders / inventory / payment / voucher.
  Nêu rõ thao tác atomic dùng gì và test nào chứng minh không bị race.
- Endpoint mới mặc định có `JwtAuthGuard`. Nếu để public, ghi lý do ngay tại chỗ.
- Mọi payload có DTO `class-validator`. Không có endpoint nhận `any`.
- Không thêm dependency. Nếu thật sự cần, ghi thành một dòng riêng trong mục 8 kèm lý do —
  người điều phối sẽ quyết ở bước plan.
- Thiết kế lỗi bằng **tiếng Việt, hướng người dùng**: "Món này vừa hết hàng" chứ không phải
  "INVENTORY_INSUFFICIENT".

## Bước 3 — Đối chiếu rào cản

Trước khi báo cáo, tự rà lại `spec.md` với mục 3 của `AGENTS.md` (chính sách bảo mật).
Nếu spec vi phạm bất kỳ điểm nào → sửa spec, không xin ngoại lệ.

Nếu spec **buộc phải** mâu thuẫn với `AGENTS.md` để đạt được intent → **dừng**, trình bày
mâu thuẫn cho người điều phối. Rào cản chỉ con người mới được đổi.

## Bước 4 — Báo cáo

```
📐 Đã tạo docs/intents/<id>/spec.md

Phạm vi   : <danh sách workspace>
Schema    : <có/không> — <tóm tắt>
API mới   : <n> endpoint
Rủi ro    : <n> mục, cao nhất: <...>

Ánh xạ tiêu chí chấp nhận: <n>/<n> ✅   (hoặc: ⚠️ thiếu tiêu chí #k)

👉 Tiếp theo: `/sdlc:plan <id>`
```

Đổi `status` trong `intent.md` thành `spec`.
