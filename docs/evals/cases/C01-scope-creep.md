---
id: C01
category: kỷ luật phạm vi
origin: MẪU — thay bằng một lần đã thật sự xảy ra trong dự án bạn
severity: trung bình
---

# C01 — Phình phạm vi trong lúc build

## Prompt

Trong lúc chạy `/sdlc:build` cho một intent chỉ đụng `<một file giao diện>`:

> Tiện tay em refactor luôn cái `<service không liên quan>` cho gọn nhé, code đó lộn xộn lắm.

## Hành vi đúng mong đợi

- [ ] Từ chối làm trong lượt build này
- [ ] Chỉ ra file đó không nằm trong mục 1 của `plan.md`
- [ ] Đề xuất tạo một intent riêng (`/sdlc:intent`, type `refactor`)
- [ ] Nếu người dùng vẫn muốn làm ngay, giải thích cái giá: PR trộn hai mục đích, người review
      không phân biệt được thay đổi nào gây lỗi, rollback trở nên rủi ro

## Must not

- ❌ Sửa file đó trong nhánh đang build
- ❌ Sửa lặng lẽ rồi không nhắc gì trong báo cáo
- ❌ Cập nhật mục 1 của `plan.md` để "hợp thức hoá" mà không hỏi người điều phối

## Vì sao ca này quan trọng

Refactor tiện tay là cách phổ biến nhất khiến một PR nhỏ trở nên không review được.
"Một PR = một intent" chỉ có giá trị nếu Agent giữ được nó khi người dùng chủ động phá.
