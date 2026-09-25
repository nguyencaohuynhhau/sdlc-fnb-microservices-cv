---
description: 6️⃣ Rà soát bảo mật → commit → PR
---

# /sdlc:ship — Rà soát và bàn giao

Tham số: `<intent-id>`.

## Cổng vào

`status: verified` và `docs/evidence/<id>/verify.md` tồn tại với mọi cổng xanh.
Chưa verify → **dừng**, chạy `/sdlc:verify` trước.

## Bước 1 — Rà soát bảo mật (Agent 6)

Đọc lại **toàn bộ diff** (`git diff main...HEAD`) và đối chiếu mục 3 của
[AGENTS.md](../../../AGENTS.md). Kiểm cụ thể:

| Kiểm tra | Cách xác minh |
|----------|---------------|
| Không secret trong diff | `git diff main...HEAD` — soi chuỗi giống key/token/URI có mật khẩu |
| `.env` không bị commit | `git diff --name-only main...HEAD \| grep -E '^\.env'` phải rỗng |
| Guard không bị nới | Mọi endpoint mới có `@UseGuards(JwtAuthGuard)`; nếu public, có lý do trong spec |
| DTO đầy đủ | Mọi `@Body()` có DTO `class-validator`, không `any` |
| CORS không mở | Không có `origin: '*'` |
| Không log PII | Không `console.log` số điện thoại / địa chỉ khách |
| Dependency mới | So `package.json` với mục 4 của `plan.md` — có được duyệt không? |
| Toàn vẹn tồn kho | Nếu chạm orders/inventory: thao tác có atomic không? e2e race có xanh không? |

Phát hiện vi phạm → **dừng, không tạo PR**, báo cáo rõ vi phạm nào và ở file/dòng nào.

## Bước 2 — Commit

Cập nhật `docs/api/endpoints.md` và `docs/database/schema.md` nếu có đổi, rồi:

```
<type>(<scope>): <mô tả ngắn bằng tiếng Anh>

<Thân: vì sao thay đổi này tồn tại, không phải nó làm gì.>

Intent: docs/intents/<id>/
```

Nhớ giữ trailer đồng tác giả theo cấu hình của repo.

Hook chặn commit sẽ tự kiểm tra `npm run sdlc:verify` đã chạy và còn tươi. Nếu bị chặn,
đừng đặt biến môi trường để né — chạy lại verify.

## Bước 3 — Pull Request

**Trước khi push, hỏi người dùng.** Push và mở PR là hành động hướng ra ngoài; xác nhận trước.

Thân PR:

```markdown
## Vấn đề
<lấy từ intent.md mục 2 — nỗi đau, bằng ngôn ngữ nghiệp vụ>

## Giải pháp
<lấy từ spec.md mục 1>

## Chuỗi hiện vật
- Intent: `docs/intents/<id>/intent.md`
- Spec:   `docs/intents/<id>/spec.md`
- Plan:   `docs/intents/<id>/plan.md`

## Bằng chứng
<bảng từ docs/evidence/<id>/verify.md + ảnh chụp>

## Rà soát bảo mật
<kết quả bảng ở bước 1>

## Nguy cơ đã lường trước
<mục 5 của plan.md — để reviewer biết cần soi chỗ nào>

## Người review nên tập trung vào
<1-3 điểm cần con người phán xét: đánh đổi kiến trúc, quyết định nghiệp vụ.
KHÔNG liệt kê những thứ CI đã kiểm.>
```

## Bước 4 — Đóng vòng

Đổi `status` thành `shipped`, ghi ngày và link PR vào frontmatter, chạy `/sdlc:triage`
để cập nhật INDEX.md.

Nếu quá trình vừa rồi lộ ra một lỗ hổng trong quy trình (rào cản thiếu, plan hay sai chỗ nào,
verify không bắt được lỗi gì), thêm một ca vào `docs/evals/cases/` để lần sau bắt được.
