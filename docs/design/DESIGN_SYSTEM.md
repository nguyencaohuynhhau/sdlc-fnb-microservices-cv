# Design system — web

Nguồn duy nhất: `web/src/index.css`. Tailwind v4 qua `@tailwindcss/vite`; **không có `tailwind.config.*`**.
Token là bộ màu mặc định của shadcn/ui (base `neutral`, CSS variables), khai báo ở `:root` rồi nối vào
Tailwind bằng `@theme inline`. Ảnh thật: `docs/evidence/01-260925-fnb-pos-core/a-*.png`.

## Token đang dùng

| Token | Giá trị | Dùng cho |
|-------|---------|----------|
| `background` / `foreground` | trắng / gần đen | nền trang, chữ |
| `card` / `card-foreground` | trắng / gần đen | thẻ món, thẻ đơn, cột bếp |
| `primary` / `primary-foreground` | gần đen / gần trắng | nút chính ("Gửi bếp", "Mở ca", "Bắt đầu làm", "Xong"), badge `Xong` |
| `secondary`, `muted`, `accent` | xám rất nhạt | badge `Chờ`/`Đang làm`, nền hover |
| `muted-foreground` | xám | chữ phụ, trạng thái rỗng ("Thực đơn trống — chạy seed", "Chưa có đơn nào") |
| `destructive` | đỏ `oklch(0.577 0.245 27.325)` | banner mất kết nối, "Chưa mở ca làm việc…", badge `Hết`, nút "Huỷ đơn", lỗi form |
| `border`, `input`, `ring` | xám nhạt | viền, ô nhập, focus ring |
| `radius` | `0.625rem` | sinh `radius-sm/md/lg/xl` |

Không có dark mode ở lát A (POS chạy trên màn quầy sáng).

## Component

Chỉ dùng component shadcn/ui nằm trong `web/src/components/ui/`: `badge`, `button`, `card`, `input`,
`label`, `sonner`. Cần thêm thì chạy `npx shadcn add <tên>` — không tự viết component UI trùng vai.

## Quy tắc

- **Không thêm token, không mã màu cứng** (`#…`, `rgb(…)`, `bg-red-500`). Cần màu mới → sửa file này trước, rồi `index.css`.
- Trạng thái món: `Chờ` và `Đang làm` = badge `secondary`; `Xong` = badge `default`; `Đã huỷ` = badge `secondary`, tên món chữ `muted-foreground` gạch ngang.
- Lỗi từ server hiện nguyên `detail` của ProblemDetails qua toast `sonner` (đặt ở `queryClient.ts`), không tự viết lại câu.
- Tiền hiển thị bằng `formatVnd` (`Intl.NumberFormat('vi-VN', { currency: 'VND' })`), số dùng `tabular-nums`.
- Mọi chữ trên giao diện là tiếng Việt; nút ghi dữ liệu bị `disabled` khi mất kết nối.
