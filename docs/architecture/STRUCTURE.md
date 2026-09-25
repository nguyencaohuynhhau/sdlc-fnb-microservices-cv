# Cấu trúc thư mục

> **MẪU.** Điền cấu trúc thật của dự án. File này tồn tại để Agent khỏi `find` mò mỗi lần —
> [AGENTS.md](../../AGENTS.md) §1 bắt buộc cập nhật nó khi thêm module hoặc workspace mới.
> Mô tả **vị trí và trách nhiệm**, không liệt kê từng file.

## Gốc repo

- `<ws-1>/` — <vai trò>
- `<ws-2>/` — <vai trò>
- `scripts/sdlc/` — script cổng chất lượng và hook (thuộc bộ khung, hiếm khi sửa)
- `docs/` — hiện vật quy trình:
  - `intents/` — chuỗi `intent.md → spec.md → plan.md` của từng đơn vị công việc
  - `evidence/` — ảnh chụp và log do `/sdlc:verify` sinh ra
  - `evals/` — bộ đánh giá hồi quy cho chính quy trình
  - `architecture/` — cấu trúc mã nguồn (chính file này)

## `<ws-1>/`

```
src/
├── <domain>/          # <trách nhiệm>
└── <domain>/          # <trách nhiệm>
```

## `<ws-2>/`

```
src/
└── ...
```
