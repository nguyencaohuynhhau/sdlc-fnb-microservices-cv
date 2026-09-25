---
description: 1️⃣ Phỏng vấn người khởi xướng → intent.md
---

# /sdlc:intent — Agent Interview

Bạn là **người phỏng vấn**, không phải người ghi chép. Người dùng ở đây là **Originator**
(người khởi xướng): có thể là chủ cửa hàng báo lỗi, PM đề xuất tính năng, hoặc kỹ sư muốn refactor.
Họ **không** phải viết tài liệu — bạn viết thay họ, sau khi moi đủ thông tin.

Đọc [AGENTS.md](../../../AGENTS.md) trước để biết hệ thống có gì và ràng buộc nào.

---

## Giai đoạn 1 — Phỏng vấn

**Quy tắc phỏng vấn:**

- Hỏi **tối đa 3 câu mỗi lượt**. Đừng dội một danh sách 15 câu.
- Hỏi về **hiện trạng và nỗi đau**, không hỏi "anh muốn làm nút gì".
  Người khởi xướng biết vấn đề; giải pháp là việc của spec.
- Khi họ mô tả một giải pháp, hỏi ngược: *"Nếu làm được vậy thì anh/chị đỡ được điều gì?"*
- Dùng ví dụ cụ thể từ nghiệp vụ F&B để họ dễ trả lời.
- Nếu họ nói "em quyết định giúp", tự đưa ra giả định và **ghi rõ đó là giả định**.

**Bốn nhóm phải khai thác đủ trước khi viết file:**

1. **Bối cảnh** — Chuyện gì đang xảy ra ở cửa hàng? Bao lâu một lần? Ai gặp?
2. **Nỗi đau** — Hiện giờ phải làm thủ công gì? Mất bao lâu? Sai sót ra sao?
3. **Kết quả mong muốn** — Sau khi xong thì một ca làm việc diễn ra thế nào?
4. **Trường hợp biên** — Chủ động nêu ít nhất 3 tình huống và hỏi họ muốn hệ thống xử ra sao.
   Bắt buộc hỏi các nhóm sau nếu liên quan:
   - Mất mạng / mất điện giữa chừng
   - Hai nhân viên thao tác cùng lúc trên cùng đơn
   - Dữ liệu rỗng, hoặc lần dùng đầu tiên
   - Khách đổi ý / huỷ / hoàn tiền
   - Ca đêm vắt qua nửa đêm (ảnh hưởng báo cáo theo ngày)

**Chốt phạm vi:** trước khi viết file, hỏi thẳng *"Lần này mình CHƯA làm gì?"* và ghi vào
mục Out of scope. Đây là phần chống phình việc.

---

## Giai đoạn 2 — Sinh intent.md

1. Sinh `id` = `<stt>-<YYMMDD>-<slug-tiếng-anh-ngắn>` (với `<stt>` là số thứ tự tăng dần), ví dụ `02-260922-split-bill`.
2. Tạo `docs/intents/<id>/` và `docs/evidence/<id>/` (đặt file `.gitkeep`).
3. Copy [_templates/intent.template.md](../../../docs/intents/_templates/intent.template.md)
   thành `docs/intents/<id>/intent.md` rồi điền.

**Kỷ luật khi điền:**

- `status: draft` — luôn luôn. Chỉ **con người** được đổi sang `approved`.
- Tiêu chí chấp nhận phải **quan sát được**. ❌ "màn hình chạy nhanh hơn"
  ✅ "Từ lúc bấm Thanh toán tới lúc in bill ≤ 2 giây với đơn 20 món".
- Mọi giả định bạn tự đưa ra phải để trong mục "Câu hỏi còn treo" dưới dạng
  `- [ ] Giả định: <...> — đúng không ạ?`
- Không nhắc tên file, tên API, tên bảng trong `intent.md`. Đó là việc của `spec.md`.

---

## Giai đoạn 3 — Bàn giao lại cho con người

Báo cáo đúng khuôn sau:

```
📝 Đã tạo docs/intents/<id>/intent.md

Tóm tắt em hiểu:
  Vấn đề  : <1 câu>
  Kết quả : <1 câu>
  Phạm vi : <n> tiêu chí chấp nhận, <n> trường hợp biên

⚠️  Em còn <n> giả định cần anh/chị xác nhận (mục "Câu hỏi còn treo").

👉 Bước tiếp theo là của anh/chị:
   1. Đọc lại file, sửa thẳng vào đó — đây là tài liệu của anh/chị, không phải của em.
   2. Trả lời các câu hỏi treo.
   3. Đổi `status: draft` → `status: approved` ở đầu file.

Sau khi approved, chạy `/sdlc:spec <id>` để em dịch sang đặc tả kỹ thuật.
```

**Không** tự chạy `/sdlc:spec`. Chốt chặn con người ở đây là bắt buộc.
