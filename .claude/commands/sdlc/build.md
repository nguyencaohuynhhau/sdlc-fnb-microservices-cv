---
description: 4️⃣ Thực thi plan.md (auto mode + worktree)
---

# /sdlc:build — Thực thi

Tham số: `<intent-id>`.

## Cổng vào

- `docs/intents/<id>/plan.md` tồn tại, `status: planned`, và mục "Duyệt của người điều phối"
  đã tick đủ 2 ô. Chưa duyệt → **dừng**, không code.
- Working tree sạch (`git status`). Bẩn → hỏi người dùng xử lý trước.

## Bước 1 — Nhánh / worktree

```bash
git checkout -b <branch trong frontmatter plan.md>
```

Nếu mục 3 của plan có nhóm song song, tạo worktree cho từng nhóm:

```bash
git worktree add ../<tên-repo>-<nhóm> -b <branch>-<nhóm>
```

Mỗi sub-agent làm việc trong đúng worktree của nó. Không có hai agent cùng ghi vào một
working tree — đó là nguồn gốc của xung đột không thể debug.

## Bước 2 — Thực thi từng bước

Chạy tuần tự các bước ở mục 2 của `plan.md`. Với mỗi bước:

1. Làm đúng bước đó, không làm trước bước sau.
2. Chạy dòng **kiểm chứng** của bước.
3. Tick `[x]` vào `plan.md` **ngay**, trước khi sang bước kế.

`plan.md` là bộ nhớ ngoài của bạn. Nếu context bị nén hoặc phiên bị ngắt, ai đọc file này
cũng biết đang ở đâu. Đừng để nó lệch với thực tế.

## Bước 3 — Khi thực tế khác kế hoạch

Chuyện này bình thường. Điều **không** bình thường là im lặng làm khác đi.

- Cần chạm file ngoài mục 1 → cập nhật mục 1 kèm một câu lý do, rồi làm tiếp.
- Phát hiện plan sai về mặt kỹ thuật → **dừng**, báo người điều phối, đề xuất sửa plan.
  Không tự thiết kế lại giữa chừng.
- Cần thêm dependency mà plan không nêu → **dừng và hỏi**. Đây là rào cản trong `AGENTS.md`.
- Test có sẵn đỏ lên → điều tra xem là hồi quy thật hay test cần cập nhật. **Không bao giờ**
  sửa/xoá test để nó xanh mà không giải thích được vì sao kỳ vọng cũ nay sai.

## Bước 4 — Kỷ luật code

Áp dụng [AGENTS.md](../../../AGENTS.md) mục 2 và 3. Nhắc lại vài điểm hay bị bỏ sót:

- Code mới **đi kèm test trong cùng commit**. Không có "test sau".
- Viết test xong, cố tình phá logic để xác nhận test đỏ, rồi khôi phục. Test không fail được
  là test vô dụng.
- Comment nghiệp vụ tiếng Việt, tên định danh tiếng Anh.
- Không `@ts-ignore`, không `any` để né type error.
- Viết code khớp phong cách file xung quanh: cùng mật độ comment, cùng lối đặt tên.

## Bước 5 — Ghi `CHANGELOG.md`

Trước khi bàn giao, thêm **một mục mới ở đầu** `CHANGELOG.md` (ngay dưới dòng `# Changelog`):

```markdown
## [YYYY-MM-DD] — <một câu mô tả thay đổi, tiếng Việt>
### Added | Changed | Fixed | Removed
- **<workspace>:** <thay đổi ở mức hành vi, không phải danh sách file>
- **<workspace khác>:** <tương tự>
- **Tests:** +N test <cái gì>. Toàn bộ **N/N pass**.

### Notes
- <đánh đổi đã chọn, phạm vi MVP cố ý bỏ, thứ để lại cho sau>
- Intent: `docs/intents/<id>/`
```

Quy tắc:

- Viết **cái gì đổi về mặt hành vi**, không chép `git diff`. Người đọc muốn biết hệ thống giờ
  làm gì khác đi, `git log` đã lo phần file nào đổi.
- Mục **Notes là phần đáng giá nhất**: vì sao chọn cách này, cái gì cố ý *không* làm. Đây là
  thứ duy nhất trong file mà git không tự sinh lại được.
- Chỉ trỏ tới `docs/intents/<id>/` — **không** trỏ tới `docs/specs/`, `docs/plans/`,
  `docs/briefs/`, `docs/designs/`; những thư mục đó đã gỡ khỏi repo.
- Thay đổi không đổi hành vi (đổi tên biến, sửa chính tả, bump dep) → bỏ qua bước này.

## Bước 6 — Bàn giao sang verify

Khi tick hết các bước, **tự chạy `/sdlc:verify <id>` ngay**. Không dừng lại hỏi người dùng —
kiểm thử tự trị là trách nhiệm của bạn, không phải của họ.

Đổi `status` thành `building` khi bắt đầu.
