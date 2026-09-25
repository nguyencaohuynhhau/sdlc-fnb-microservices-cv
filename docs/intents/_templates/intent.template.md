---
id: <stt>-<YYMMDD>-<slug>
title: <Một câu, nói kết quả nghiệp vụ chứ không nói giải pháp>
status: draft
type: feature | bug | refactor | chore
area: []            # tên workspace trong sdlc.config.json, hoặc docs
size: S | M | L      # S: <1 buổi, M: 1-2 ngày, L: cần chia nhỏ
priority: P0 | P1 | P2
originator: <tên người khởi xướng>
created: <YYYY-MM-DD>
related: []          # id của intent liên quan
---

# Intent: <title>

## 1. Bối cảnh thực tế

<Chuyện gì đang xảy ra ở cửa hàng/trên hệ thống hôm nay. Viết bằng ngôn ngữ nghiệp vụ,
có số liệu hoặc tần suất nếu có. Không nhắc tới tên file hay API.>

## 2. Nỗi đau (pain point)

<Ai đau, đau ở đâu, tốn bao nhiêu thời gian/tiền. Càng cụ thể càng tốt.>

- **Ai:** thu ngân / chủ cửa hàng / khách hàng / bếp
- **Khi nào:** <tình huống kích hoạt>
- **Hậu quả hiện tại:** <phải làm thủ công gì, sai sót gì>

## 3. Kết quả mong muốn

<Mô tả trạng thái "sau khi xong" dưới góc nhìn người dùng. KHÔNG mô tả cách làm.>

## 4. Tiêu chí chấp nhận

Người khởi xướng sẽ coi là xong khi:

- [ ] <Hành vi quan sát được 1>
- [ ] <Hành vi quan sát được 2>
- [ ] <Hành vi quan sát được 3>

## 5. Trường hợp biên đã bàn

| Tình huống                      | Hành vi mong muốn |
|---------------------------------|-------------------|
| <VD: mất mạng giữa chừng>       |                   |
| <VD: dữ liệu rỗng / lần đầu>    |                   |
| <VD: hai người thao tác cùng lúc> |                 |

## 6. Ngoài phạm vi (Out of scope)

<Liệt kê rõ những gì KHÔNG làm lần này. Đây là phần chống phình việc.>

- <...>

## 7. Câu hỏi còn treo

- [ ] <Câu hỏi chưa có lời đáp — chặn hay không chặn?>

---

## Ghi chú của người khởi xướng

> _Người khởi xướng điền phần này sau khi đọc lại. Đây là chốt chặn của con người._
> _Sau khi sửa xong và đồng ý, đổi `status` ở frontmatter thành `approved`._
