---
description: 5️⃣ Kiểm thử tự trị + thu thập bằng chứng
---

# /sdlc:verify — Kiểm thử tự trị

Tham số: `<intent-id>` (không bắt buộc — không có thì verify thay đổi hiện tại).

**Nguyên tắc:** Agent hoàn thành tối đa quy trình kiểm định **trước khi** bất kỳ con người nào
chạm vào code. Người review nên xem logic và kiến trúc, không phải bắt lỗi cú pháp.

## Bước 1 — Cổng tự động

```bash
npm run sdlc:verify
```

Script này scope-aware: chỉ chạy lint/build/test cho workspace thật sự có thay đổi.
Nếu chạm bất kỳ đường dẫn nào trong `e2eTriggers` của [sdlc.config.json](../../../sdlc.config.json)
thì script tự bật e2e. Ép chạy bằng tay:

```bash
npm run sdlc:verify -- --e2e     # cần dịch vụ ngoài (DB, queue…) đang chạy
```

Kết quả ghi vào `.brain/sdlc-state.json` — hook chặn commit đọc file này.

## Bước 2 — Khi cổng đỏ

Sửa, đừng né. Theo thứ tự:

1. Đọc lỗi đầu tiên, không phải lỗi cuối cùng. Lỗi sau thường là hệ quả.
2. Sửa nguyên nhân gốc. Không nới lint rule, không thêm `@ts-ignore`,
   không `skip` test, không nới guard.
3. Chạy lại `npm run sdlc:verify`.
4. Tắc quá 2 vòng → dừng và báo người điều phối kèm log.

Đặc biệt: nếu một test trong `requiredTests` (xem `sdlc.config.json`) đỏ, đó là **hồi quy ở
vùng đã từng có sự cố thật**. Dừng ngay, báo cáo, không đi tiếp — không sửa test cho nó xanh.

## Bước 3 — Bằng chứng trực quan (khi có thay đổi UI)

Với mọi thay đổi chạm workspace có giao diện:

1. Khởi chạy app liên quan (`npm run dev...`).
2. Dùng Playwright hoặc trình duyệt để đi qua đúng luồng
   trong tiêu chí chấp nhận của `intent.md` — thao tác như người dùng thật.
3. Chụp màn hình **từng trạng thái**: empty, loading, success, error.
4. Lưu vào `docs/evidence/<id>/<mô-tả>.png`.

Ảnh chụp một màn hình đẹp ở trạng thái success không phải bằng chứng. Trạng thái lỗi mới là
chỗ tính năng hay hỏng.

## Bước 4 — Đối chiếu tiêu chí chấp nhận

Mở `intent.md`, đi từng dòng trong "Tiêu chí chấp nhận". Với mỗi dòng, chỉ ra bằng chứng
cụ thể: tên test, hoặc tên file ảnh. Tiêu chí nào **không** có bằng chứng → chưa xong,
quay lại `/sdlc:build`.

Lưu kết quả vào `docs/evidence/<id>/verify.md`:

```markdown
# Verify — <id>
Ngày: <YYYY-MM-DD HH:mm>   Commit: <sha ngắn>

## Cổng tự động
| Cổng | Workspace | Kết quả |
|------|-----------|---------|

## Tiêu chí chấp nhận
| # | Tiêu chí | Bằng chứng | ✅/❌ |
|---|----------|------------|-------|

## Test hồi quy
- orders-inventory-race.e2e-spec.ts: <pass/fail/không chạy — lý do>

## Điều chưa kiểm được
- <trung thực. "Không có" cũng là một câu trả lời hợp lệ, nhưng phải là sự thật.>
```

## Bước 5 — Báo cáo

Báo trung thực. Cổng đỏ thì nói là đỏ, kèm output. Bước bỏ qua thì nói là bỏ qua, kèm lý do.
Chỉ khi **mọi** cổng xanh và **mọi** tiêu chí có bằng chứng mới đổi `status` thành `verified`
và đề xuất `/sdlc:ship <id>`.
