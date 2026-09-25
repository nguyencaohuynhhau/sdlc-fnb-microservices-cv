---
id: C02
category: chất lượng phỏng vấn
origin: MẪU — thay bằng một yêu cầu mơ hồ có thật của dự án bạn
severity: trung bình
---

# C02 — Yêu cầu mơ hồ

## Prompt

> Anh muốn thêm chương trình khách hàng thân thiết.

## Hành vi đúng mong đợi

- [ ] Chạy `/sdlc:intent`, hỏi tối đa 3 câu mỗi lượt (không dội một danh sách dài)
- [ ] Hỏi về **nỗi đau hiện tại** trước, không hỏi ngay "anh muốn tích bao nhiêu điểm một đơn"
- [ ] Khai thác được: ai là khách quen, hiện đang nhận biết họ bằng cách nào, đang mất gì
- [ ] Chủ động nêu ít nhất 3 trường hợp biên: khách quên mang thẻ/SĐT, tích điểm rồi huỷ đơn,
      hai số điện thoại của cùng một người
- [ ] Hỏi thẳng "lần này mình CHƯA làm gì?" và ghi vào Out of scope
- [ ] Đánh `size: L` và nói rõ cần chia nhỏ trước khi sang `/sdlc:spec`
- [ ] Ghi mọi giả định vào "Câu hỏi còn treo" thay vì âm thầm quyết hộ

## Must not

- ❌ Nhảy thẳng sang thiết kế schema hoặc endpoint
- ❌ Sinh `intent.md` với `status: approved`
- ❌ Tự chạy `/sdlc:spec` ngay sau khi tạo intent
- ❌ Viết tiêu chí chấp nhận không quan sát được ("hệ thống thân thiện với khách hàng")

## Vì sao ca này quan trọng

Chất lượng của cả chuỗi hiện vật bị chặn trên bởi chất lượng buổi phỏng vấn. Một Agent đoán
thay vì hỏi sẽ tạo ra `spec.md` trông rất chuyên nghiệp cho một tính năng không ai cần.
