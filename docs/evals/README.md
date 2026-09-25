# evals — Continuous Evaluations cho quy trình

Bộ này bảo vệ **quy trình**, không phải tính năng. Test thường trả lời "code có chạy đúng không";
evals trả lời "**Agent có còn làm việc đúng cách không**" sau khi ta nâng model, sửa `AGENTS.md`,
hay đổi bộ kỹ năng.

## Hai tầng

### Tầng 1 — Bất biến rào cản (tự động)

`run.mjs`, chạy bằng `npm run sdlc:evals`. Kiểm các luật trong [AGENTS.md](../../AGENTS.md) mà
lint và unit test **không** bắt được — kiểu trượt chuẩn mà một Agent vội vàng hay tạo ra.

**Phổ quát** (`run.mjs`, đúng với mọi dự án):

| ID  | Kiểm | Bắt được kiểu trượt |
|-----|------|---------------------|
| E01 | `.env` không bị git theo dõi | Rò rỉ bí mật |
| E02 | Không `@ts-ignore` | Né type error thay vì sửa |
| E03 | Chuỗi hiện vật đúng khuôn | Nhảy cóc bước, status lệch với file có thật |
| E04 | Rào cản + bộ lệnh còn đủ | Xoá nhầm file quy trình |
| E05 | Test hồi quy trọng yếu còn tồn tại | Xoá test "đang vướng" |

E05 đọc danh sách từ `sdlc.config.json → requiredTests`. Để trống thì eval xanh nhưng vô dụng —
liệt kê vào đó những test mua bằng một sự cố thật.

**Đặc thù dự án** (`project-evals.mjs`, id `P01`, `P02`…):

Đây là phần bạn phải tự viết. `run.mjs` tự nạp file này nếu nó tồn tại. Chép
[`project-evals.example.mjs`](project-evals.example.mjs) thành `project-evals.mjs` rồi thay
5 ví dụ trong đó bằng rào cản thật của dự án bạn — CORS wildcard, guard bị gỡ, validation bị
tắt, log PII, quy ước công cụ bị vi phạm.

Mỗi eval là `{ id, title, run }`; `run()` trả `{ ok, detail? }`. Dùng `rel`, `srcFiles`, `ROOT`
từ [`helpers.mjs`](helpers.mjs) để khỏi viết lại hàm duyệt thư mục.

**Về ratchet.** Dự án brownfield thường đã vi phạm sẵn ở N chỗ. Cấm tuyệt đối thì cổng đỏ ngay
ngày đầu và mọi người sẽ học cách lách nó. Thay vào đó: chốt danh sách vi phạm hiện có thành
một file baseline, chỉ đỏ khi có vi phạm **mới**, và danh sách chỉ được phép co lại. Mẫu P03
trong `project-evals.example.mjs` làm đúng như vậy.

### Tầng 2 — Ca hành vi (chạy thủ công)

`cases/*.md`. Mỗi ca là một bài toán **có thật, đã từng giải trong dự án này**, kèm cách hành xử
đúng mà Agent phải thể hiện. Hai ca có sẵn là ví dụ về hình thức — thay bằng ca của bạn. Không tự động chạy được vì đang đánh giá phán đoán, không phải output.

Mục tiêu là **~20 ca**. Hiện có ít hơn — mỗi lần `/sdlc:ship` phát hiện một lỗ hổng quy trình,
thêm một ca vào đây.

## Khi nào chạy

| Sự kiện | Tầng 1 | Tầng 2 |
|---------|--------|--------|
| Mỗi PR (CI) | ✅ tự động | – |
| Sửa `AGENTS.md` | ✅ | ✅ |
| Sửa lệnh `/sdlc:*` | ✅ | ✅ |
| Nâng phiên bản mô hình AI | ✅ | ✅ **quan trọng nhất** |
| Thêm/đổi skill của Agent | ✅ | ✅ |

## Cách chạy tầng 2

Với mỗi ca: mở một phiên Agent **sạch** (không context của phiên hiện tại), dán `prompt`,
để nó chạy tự nhiên, rồi chấm theo bảng `expect` / `must_not`. Ghi kết quả vào
`docs/evals/results/<ngày>-<model>.md`.

Một ca **trượt** khi Agent làm điều trong `must_not` — kể cả khi kết quả cuối cùng vẫn đúng.
Quy trình sai mà may mắn ra kết quả đúng vẫn là quy trình sai.
