---
id: <YYMMDD>-<slug>
intent: ./intent.md
spec: ./spec.md
status: planned
branch: <type>/<id>-<slug>
generated_by: /sdlc:plan
created: <YYYY-MM-DD>
---

# Plan: <title>

> **Bài kiểm tra bàn giao:** một agent hoặc kỹ sư chưa từng đọc hội thoại nào của dự án này,
> chỉ với `AGENTS.md` + `spec.md` + file này, phải làm được đúng việc. Nếu chưa, plan chưa xong.

## 0. Bằng chứng thành công (đọc trước tiên)

Tác vụ chỉ hoàn thành khi **toàn bộ** lệnh sau chạy xanh:

```bash
npm run sdlc:verify          # scope-aware: lint + build + unit test cho workspace đã đổi
npm run sdlc:verify -- --e2e # nếu chạm vùng trong e2eTriggers
```

Cộng thêm các bằng chứng cụ thể của tính năng này:

- [ ] `<test file>` — ca `<tên ca>` đỏ trước khi sửa, xanh sau khi sửa
- [ ] Ảnh chụp `docs/evidence/<tên>.png` cho màn hình `<...>`
- [ ] Các test trong `requiredTests` vẫn xanh

## 1. Các file sẽ chạm

| File | Hành động | Mục đích |
|------|-----------|----------|
| `<ws>/src/.../x.service.ts` | sửa | ... |
| `<ws>/src/.../x.dto.ts`     | tạo | ... |

**Không** chạm tới file nào ngoài danh sách này mà không cập nhật plan trước.

## 2. Các bước thực thi

Đánh dấu `[x]` ngay khi xong từng bước, trước khi sang bước sau.

- [ ] **B1.** <mô tả> → kiểm chứng: `<lệnh hoặc quan sát>`
- [ ] **B2.** <mô tả> → kiểm chứng: `<...>`
- [ ] **B3.** Viết test cho B1–B2 → kiểm chứng: test đỏ khi revert logic
- [ ] **B4.** Chạy `npm run sdlc:verify`, lưu log vào `docs/evidence/verify.log`
- [ ] **B5.** Cập nhật `docs/api/endpoints.md` / `docs/database/schema.md` nếu có đổi

## 3. Thứ tự & song song hoá

| Nhóm | Các bước | Có thể chạy song song? | Worktree |
|------|----------|------------------------|----------|
| A    | B1, B2   | không (B2 phụ thuộc B1)| chính    |
| B    | B3       | có, sau nhóm A         | `wt-test`|

Sub-agent chạy song song phải dùng `git worktree add`, không cùng ghi vào working tree chính.

## 4. Ràng buộc kế thừa từ AGENTS.md

<Liệt kê rào cản cụ thể áp cho việc này, để agent không phải tự suy diễn.>

- DTO bắt buộc, `whitelist: true` — không nhận field lạ.
- Endpoint mới mặc định có `JwtAuthGuard`.
- Không thêm dependency mới. <hoặc: cần thêm `<pkg>` — lý do: ..., đã được duyệt bởi ...>

## 5. Tự chất vấn (bắt buộc — do Agent điền)

> Người điều phối hỏi: *"Thay đổi nào trong kế hoạch này có nguy cơ làm hỏng hệ thống
> hoặc xung đột với tính năng hiện có?"*

| # | Nguy cơ | Vì sao có thể xảy ra | Cách phòng | Test nào bắt được |
|---|---------|----------------------|------------|-------------------|
| 1 |         |                      |            |                   |
| 2 |         |                      |            |                   |
| 3 |         |                      |            |                   |

**Tính năng hiện có có thể bị ảnh hưởng:** <liệt kê module, kể cả gián tiếp>

**Nếu phải quay đầu:** <cách rollback — revert commit là đủ, hay cần hoàn dữ liệu?>

## 6. Điều KHÔNG làm trong lần này

- <...>

---

## Duyệt của người điều phối

> _Chốt chặn con người thứ hai. Đọc mục 5 trước tiên._
> _Đồng ý → đổi `status` thành `planned` và chạy `/sdlc:build`._

- [ ] Tôi đã đọc mục 5 và các nguy cơ là chấp nhận được
- [ ] Bằng chứng thành công ở mục 0 là đủ để tôi tin tính năng chạy đúng
