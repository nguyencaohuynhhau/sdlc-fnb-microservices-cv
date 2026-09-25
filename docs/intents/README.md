# docs/intents — Chuỗi Hiện Vật (Artifact Chain)

Mỗi đơn vị công việc (tính năng, bug, refactor) sống trong **một thư mục duy nhất**:

```
docs/intents/<YYMMDD>-<slug>/
├── intent.md      # Ý định nghiệp vụ — do người khởi xướng duyệt
├── spec.md        # Đặc tả kỹ thuật — sinh từ intent.md + AGENTS.md
└── plan.md        # Kế hoạch thực thi — đủ chi tiết để bàn giao cho agent lạ

*Lưu ý: Bằng chứng verify (ảnh chụp, kết quả test) lưu tại `docs/evidence/<YYMMDD>-<slug>/`*
```

`INDEX.md` là backlog thông minh — bảng tổng hợp trạng thái mọi intent.

## Nguyên tắc

**Song diện.** Mỗi file vừa đọc được bởi người, vừa xử lý được bởi máy. Frontmatter YAML là
phần dành cho máy (hook và script đọc `status`, `area`, `priority`); phần thân là dành cho người.

**Tự chứa.** `plan.md` phải đủ để một agent hoàn toàn mới bắt tay vào làm mà không cần đọc
lại lịch sử hội thoại. Nếu bạn phải giải thích thêm bằng miệng, `plan.md` chưa xong.

**Một chiều.** intent → spec → plan → code. Khi phát hiện sai ở tầng trên, sửa tầng trên
trước rồi mới sinh lại tầng dưới. Không vá trực tiếp vào code rồi để tài liệu lệch.

## Vòng đời `status`

| status     | Nghĩa                                        | Ai chuyển             |
|------------|----------------------------------------------|-----------------------|
| `draft`    | Vừa phỏng vấn xong, chờ người khởi xướng đọc | Agent → `/sdlc:intent`|
| `approved` | Người khởi xướng đã duyệt ý định              | **Con người**         |
| `spec`     | Đã có `spec.md`                               | `/sdlc:spec`          |
| `planned`  | Đã có `plan.md` và người điều phối đã duyệt   | **Con người**         |
| `building` | Đang viết code                                | `/sdlc:build`         |
| `verified` | Toàn bộ cổng chất lượng xanh, có evidence     | `/sdlc:verify`        |
| `shipped`  | PR đã merge                                   | `/sdlc:ship`          |
| `parked`   | Tạm dừng — ghi lý do trong intent.md          | **Con người**         |

Hai chốt chặn in đậm là nơi con người bắt buộc phải chạm vào: duyệt **ý định** và duyệt **kế hoạch**.

## Bắt đầu

```
/sdlc:intent   <mô tả vấn đề bằng lời thường — không nhắc tên file, không đề xuất giải pháp>
```

Agent sẽ phỏng vấn, rồi tạo `docs/intents/<YYMMDD>-<slug>/intent.md`.
