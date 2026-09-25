---
id: 01-260925-fnb-pos-core
title: Có một hệ thống F&B chạy thật để chứng minh năng lực microservices khi phỏng vấn
status: building
type: feature
area: [backend, web]
size: L
priority: P1
originator: haunguyen
created: 2026-09-25
related: []
---

# Intent: Có một hệ thống F&B chạy thật để chứng minh năng lực microservices khi phỏng vấn

## 1. Bối cảnh thực tế

Người khởi xướng đang nộp CV cho các vị trí .NET Core / Microservices (bốn mô tả công việc
lưu ở `docs/.net-micro.txt`). Cả bốn mô tả đòi gần như cùng một thứ: kiến trúc microservices,
giao tiếp qua message/event streaming, cache, CQRS + Clean Architecture, giao dịch tiền,
Docker, và khả năng đọc/phối hợp frontend React + TypeScript.

Hiện tại không có hiện vật nào để đưa ra khi người phỏng vấn hỏi *"anh đã tách service thật
chưa, xử lý eventual consistency thế nào, đơn hàng bị trùng thì sao"*. Trả lời bằng lý thuyết
thì mọi ứng viên đều trả lời được như nhau — đó là lý do vòng kỹ thuật thường dừng ở đây.

Nghiệp vụ được chọn làm nền là quán cà phê / nhà hàng nhỏ (F&B), vì nó sinh ra một cách tự
nhiên đủ bốn tình huống mà mô tả công việc đang đòi: luồng realtime (đặt món → bếp), giao dịch
tiền (thanh toán, chốt ca), nhất quán cuối (trừ nguyên liệu theo món bán), và báo cáo đọc nhiều
ghi ít. Không phải chọn F&B vì có cửa hàng thật nào đang đau.

## 2. Nỗi đau (pain point)

- **Ai:** người khởi xướng, với vai ứng viên.
- **Khi nào:** ở vòng phỏng vấn kỹ thuật, khi người phỏng vấn chuyển từ câu hỏi định nghĩa
  sang câu hỏi *"trong hệ thống của anh thì..."*.
- **Hậu quả hiện tại:**
  - Không có gì để mở ra và chỉ vào. Mọi luận điểm về kiến trúc đều là lời nói.
  - Các câu hỏi đào sâu — đơn hàng gửi hai lần thì sao, service tiêu thụ sự kiện chết giữa
    đường thì sao, hai người sửa cùng một đơn thì sao — không có câu trả lời mua bằng kinh
    nghiệm thật, chỉ có câu trả lời đọc được từ blog.
  - Không có số liệu nào của riêng mình để nói về hiệu năng hay độ tin cậy.

## 3. Kết quả mong muốn

Một hệ thống F&B chạy được từ một lệnh duy nhất trên máy sạch, có đủ một ca làm việc thật:
nhân viên mở ca, khách gọi món, bếp nhận món và đánh dấu xong, thu ngân thu tiền, nguyên liệu
tự trừ theo món đã bán, cuối ca chủ quán xem được doanh thu và đối chiếu tiền.

Khi phỏng vấn, người khởi xướng mở được hệ thống ra, đi một đường đơn hàng từ đầu đến cuối,
chỉ ra chỗ nào tách service và vì sao, chỗ nào chấp nhận nhất quán cuối và chỗ nào bắt buộc
nhất quán ngay, và chỉ ra được bằng chứng cho từng dòng yêu cầu trong mô tả công việc.

## 4. Tiêu chí chấp nhận

Người khởi xướng sẽ coi là xong khi:

- [ ] Trên máy sạch, chỉ cần **một lệnh** để dựng toàn hệ thống, và **một lệnh** để nạp dữ
      liệu quán cà phê mẫu (thực đơn ~20 món, nguyên liệu, vài ca đã đóng, đơn lịch sử).
- [ ] Thu ngân tạo đơn 3 món → màn hình bếp hiện đơn đó trong **≤ 2 giây**, không cần tải lại trang.
- [ ] Bếp đánh dấu một món "xong" → màn hình thu ngân đổi trạng thái món đó trong **≤ 2 giây**,
      không cần tải lại trang.
- [ ] Từ lúc bấm Thanh toán tới lúc hiện bill với đơn 20 món **≤ 2 giây**.
- [ ] Gửi lại đúng một lệnh thanh toán lần thứ hai (mạng chập, người dùng bấm đôi) **không**
      tạo thêm bút toán: tổng tiền của ca không đổi.
- [ ] Bán 1 ly cà phê sữa → tồn cà phê và sữa giảm đúng định lượng của công thức, quan sát
      được trên màn hình tồn kho.
- [ ] Khi một nguyên liệu xuống dưới ngưỡng, mọi món cần nguyên liệu đó hiện cảnh báo hết hàng
      trên màn hình thu ngân.
- [ ] Đóng ca → báo cáo ca hiện số đơn, doanh thu, tiền mặt dự kiến; nhân viên nhập được số
      tiền đếm thực tế và hệ thống hiện phần lệch.
- [ ] Ca mở 18:00 và đóng 02:00 hôm sau: đơn lúc 01:30 nằm trong **ca đó**, không nhảy sang ngày mới.
- [ ] Màn hình báo cáo: khoảng thời gian, sắp xếp, phân trang nằm **hết trong URL** — dán URL
      sang tab khác cho ra đúng kết quả đó.
- [ ] `README` có sơ đồ kiến trúc và một bảng đối chiếu: **mỗi dòng yêu cầu trong mô tả công
      việc → chỗ nào trong repo đáp ứng nó**. Đây là thứ mở ra khi phỏng vấn.
- [ ] Có bằng chứng chạy được (ảnh chụp / log) trong `docs/evidence/01-260925-fnb-pos-core/`
      cho từng luồng: đặt món, thanh toán, trừ kho, chốt ca.

## 5. Trường hợp biên đã bàn

| Tình huống | Hành vi mong muốn |
|---|---|
| Mất mạng / mất điện giữa ca | Màn hình hiện banner "mất kết nối" và **chặn** tạo đơn mới; khi có mạng lại thì tự hồi phục, không cần tải lại trang. Không làm offline-first. |
| Hai người sửa cùng một đơn cùng lúc | Ai ghi sau trên bản cũ thì **bị từ chối**, màn hình hiện "đơn vừa thay đổi, tải lại" — không âm thầm đè mất thao tác của người kia. |
| Lần đầu chạy, chưa có dữ liệu | Có lệnh nạp dữ liệu quán cà phê mẫu; mở ra là thấy hệ thống hoạt động ngay (phục vụ mục đích demo). |
| Khách đổi ý trước khi bếp làm | Huỷ được từng món khi món chưa vào bếp, và huỷ được cả đơn khi đơn chưa thanh toán. Món đã vào bếp thì phải bếp xác nhận mới huỷ. |
| Đã thanh toán rồi khách mới đòi hoàn tiền | **Chưa làm lần này** (xem Ngoài phạm vi). Đơn đã thanh toán là chốt. |
| Ca đêm vắt qua nửa đêm | Doanh thu tính theo **ca làm việc** (mốc mở/đóng ca), không cắt theo 00:00. Báo cáo ngày = tổng các ca đóng trong ngày đó. |
| Dịch vụ xử lý sự kiện chết giữa đường (đơn đã thu tiền nhưng kho chưa trừ) | Không được mất việc: khi dịch vụ sống lại, phần việc còn treo được xử lý tiếp và không bị trừ kho hai lần. |

## 6. Ngoài phạm vi (Out of scope)

- **Kubernetes và triển khai cloud (Azure/AWS).** Chỉ chạy container trên máy local. Đây là
  intent riêng sau này.
- **Hoàn tiền sau khi đã thanh toán** và nghiệp vụ bù trừ kéo theo (trả lại tồn kho, sửa báo
  cáo ca đã đóng).
- Tích hợp cổng thanh toán thật và driver máy in bill vật lý — thanh toán chỉ ghi nhận trong
  hệ thống (tiền mặt / chuyển khoản xác nhận thủ công).
- Những thứ trong mô tả công việc nhưng không có chỗ dùng tự nhiên trong nghiệp vụ này:
  tin nhắn SMPP, tìm kiếm toàn văn quy mô lớn. Không nhét vào cho đủ bộ.

## 7. Câu hỏi còn treo

- [x] Giả định: lần này chỉ **một cửa hàng duy nhất**, không có khái niệm nhiều chi nhánh /
      nhiều tenant xuyên hệ thống — nếu cần thì tách intent riêng. Đúng không ạ? Đúng
- [x] Giả định: chỉ có màn hình **nhân viên** (thu ngân, bếp, chủ quán). Không có luồng khách
      tự quét QR đặt món. Đúng không ạ? Đúng
- [x] Giả định: có **đăng nhập và phân quyền theo vai** (thu ngân / bếp / chủ quán) ngay từ
      đầu, vì cả bốn mô tả công việc đều đòi phần này và nó chặn được cả một nhóm câu hỏi
      phỏng vấn. Đúng không ạ? Đúng
- [x] `size: L` — việc này quá lớn cho một lần. Đề xuất chia thành các intent nhỏ hơn
      (đặt món & bếp → thanh toán & chốt ca → tồn kho → báo cáo), làm tuần tự, mỗi phần tự
      chạy được. Anh/chị chốt giúp thứ tự này, hay muốn đổi? Đúng
- [x] Khi phỏng vấn, anh/chị sẽ demo **trên máy mình** hay cần một đường link công khai cho
      người phỏng vấn tự bấm? Câu trả lời "cần link" sẽ kéo phần triển khai cloud trở lại
      trong phạm vi. Đúng

---

## Ghi chú của người khởi xướng

> _Người khởi xướng điền phần này sau khi đọc lại. Đây là chốt chặn của con người._
> _Sau khi sửa xong và đồng ý, đổi `status` ở frontmatter thành `approved`._
