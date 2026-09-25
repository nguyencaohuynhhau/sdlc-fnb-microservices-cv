---
description: 3️⃣ spec.md → plan.md (kế hoạch + tự chất vấn)
---

# /sdlc:plan — Bản đồ thực thi

Tham số: `<intent-id>`.

**Mục tiêu:** sinh một `plan.md` đủ tự chứa để bàn giao cho một agent hoàn toàn mới —
agent đó không có lịch sử hội thoại này, chỉ có `AGENTS.md` + `spec.md` + `plan.md`.

## Cổng vào

`docs/intents/<id>/spec.md` tồn tại và mục 11 ("Điểm chưa quyết") không còn `- [ ]` treo.
Còn treo → **dừng**, quay lại `/sdlc:spec`.

## Bước 1 — Khảo sát thật

Đọc **từng file** mà spec nói sẽ chạm. Không lập kế hoạch trên file bạn chưa mở.
Với mỗi file, ghi nhận: nó đang làm gì, ai gọi nó, test nào đang phủ nó.

## Bước 2 — Viết plan.md

Copy [_templates/plan.template.md](../../../docs/intents/_templates/plan.template.md) thành
`docs/intents/<id>/plan.md`.

**Mục 0 (Bằng chứng thành công) viết trước tiên.** Trước khi nghĩ về các bước, xác định:
*làm sao biết là xong?* Mỗi mục phải là một lệnh chạy được hoặc một quan sát cụ thể.
❌ "Test pass" → ✅ "`npm test -- orders.service.spec` xanh, kể cả ca `chia hoá đơn lẻ tiền`".

**Mục 1 (file sẽ chạm):** liệt kê đường dẫn chính xác. Đây là hợp đồng — bước build không
được chạm file ngoài danh sách mà không quay lại sửa plan.

**Mục 2 (các bước):** mỗi bước có một dòng **kiểm chứng** riêng. Bước nào không kiểm chứng
được là bước quá to — chẻ nhỏ ra.

**Mục 3 (song song hoá):** chỉ tách worktree khi hai nhóm bước thật sự không đụng chung file.
Ghi rõ lệnh `git worktree add`.

## Bước 3 — TỰ CHẤT VẤN (không được bỏ)

Đây là bước có giá trị cao nhất của cả quy trình. Tự đặt cho mình câu hỏi:

> **"Thay đổi nào trong kế hoạch này có nguy cơ làm hỏng hệ thống hoặc xung đột với
> tính năng hiện có?"**

Trả lời bằng cách **đi tìm bằng chứng trong code**, không đoán:

- Với mỗi hàm/endpoint sẽ sửa, `grep` tìm mọi nơi gọi nó. Ai đang phụ thuộc?
- Với mỗi field schema sẽ đổi, tìm mọi query đọc field đó. Dữ liệu cũ có field này không?
- Thay đổi này có chạm luồng orders / inventory / payment không? Nếu có, race condition ở đâu?
- Có test hiện có nào sẽ đỏ? Nếu có, đó là hồi quy thật hay test cần cập nhật? Nói rõ.
- Nếu deploy giữa ca bán hàng, đơn đang mở dở có sao không?

Điền **ít nhất 3 dòng** vào bảng mục 5. Mỗi dòng phải chỉ ra test nào bắt được nguy cơ đó —
nếu không có test nào bắt được, thêm test đó vào mục 2.

Nếu không tìm ra nguy cơ nào, đó gần như luôn có nghĩa là bạn chưa đọc đủ code. Đọc tiếp.

## Bước 4 — Trình duyệt

```
🗺️  Đã tạo docs/intents/<id>/plan.md

Nhánh   : <branch>
Chạm    : <n> file, <n> bước
Worktree: <có/không>

⚠️  TỰ CHẤT VẤN — <n> nguy cơ tìm được:
  1. <nguy cơ> → phòng bằng <...>, bắt bởi <test>
  2. ...
  3. ...

Tính năng có thể bị ảnh hưởng: <danh sách>

👉 Anh/chị đọc mục 5 trước. Duyệt xong thì đổi status thành `planned` rồi `/sdlc:build <id>`.
```

**Không tự chạy `/sdlc:build`.** Đây là chốt chặn con người thứ hai.
