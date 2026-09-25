---
description: 🏷️ Gán nhãn + xếp ưu tiên backlog intent
---

# /sdlc:triage — Backlog thông minh

Quét toàn bộ `docs/intents/*/intent.md`, tự phân loại, xếp ưu tiên, và dựng lại
[INDEX.md](../../../docs/intents/INDEX.md).

## Bước 1 — Thu thập

Đọc frontmatter của mọi `docs/intents/*/intent.md` (bỏ qua `_templates/`). Với mỗi intent lấy:
`id, title, status, type, area, size, priority, originator, created`.

## Bước 2 — Điền chỗ trống

Với intent nào thiếu `type` / `area` / `size` / `priority`, tự suy ra từ nội dung và **ghi
ngược vào frontmatter của file đó**:

- **`type`** — có từ "lỗi/sai/không chạy" → `bug`; "thêm/muốn có" → `feature`;
  "dọn/gộp/tách/đổi tên" → `refactor`; còn lại `chore`.
- **`area`** — suy từ nơi nỗi đau xảy ra, dùng đúng tên workspace trong `sdlc.config.json`
  (dữ liệu/tính toán/API thường là workspace backend). Có thể nhiều giá trị.
- **`size`** — `S` nếu 1 workspace và không đổi schema; `M` nếu ≤2 workspace hoặc có đổi schema;
  `L` nếu đụng ≥3 workspace, hoặc đổi schema có migration, hoặc >6 tiêu chí chấp nhận.
- **`priority`** — theo quy ước ở cuối INDEX.md. Chặn vận hành thật (người dùng không làm
  được việc chính, sai tiền, sai dữ liệu) → `P0` bất kể type.

## Bước 3 — Cảnh báo

Nêu rõ trong báo cáo, không im lặng bỏ qua:

- Intent `size: L` mà `status` đã qua `approved` → **phải chia nhỏ trước khi spec**.
- Intent `status: draft` quá 7 ngày → người khởi xướng đang tắc, cần nhắc.
- Intent `status: building` mà nhánh git tương ứng không tồn tại → mồ côi.
- Hai intent có `area` trùng và đều `planned`/`building` → nguy cơ xung đột merge.

## Bước 4 — Dựng lại INDEX.md

Ghi đè `docs/intents/INDEX.md`, giữ nguyên hai mục quy ước ở cuối file. Ba bảng:

- **Đang làm** — status ∈ {approved, spec, planned, building, verified}
- **Hàng chờ** — status ∈ {draft, parked}
- **Đã ship** — status = shipped

Sắp xếp: `priority` (P0 trước) → `created` (cũ trước). Cập nhật dòng "_Cập nhật lần cuối_".

## Bước 5 — Đề xuất

Kết thúc bằng đề xuất **3 việc nên làm tiếp**, mỗi việc một dòng kèm lý do một câu.
Ưu tiên: P0 đang tắc > intent đã `planned` chưa build > intent `draft` chờ duyệt lâu nhất.
