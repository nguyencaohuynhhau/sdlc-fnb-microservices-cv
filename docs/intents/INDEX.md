# Backlog Intent

> Bảng này do `/sdlc:triage` sinh ra. _Cập nhật lần cuối: 2026-09-25._

## Đang làm

| ID | Tiêu đề | type | area | size | priority | status |
|----|---------|------|------|------|----------|--------|
| [01-260925-fnb-pos-core](01-260925-fnb-pos-core/intent.md) | Có một hệ thống F&B chạy thật để chứng minh năng lực microservices khi phỏng vấn | feature | backend, web | L | P1 | building — lát A shipped 2026-09-25; lát B planned (chờ duyệt); lát C/D chưa plan |

## Hàng chờ

| ID | Tiêu đề | type | area | size | priority | status |
|----|---------|------|------|------|----------|--------|
|    |         |      |      |      |          |        |

## Đã ship

| ID | Tiêu đề | type | area | size | priority | status |
|----|---------|------|------|------|----------|--------|
|    |         |      |      |      |          |        |

## Quy ước `priority`

| Mức  | Nghĩa |
|------|-------|
| `P0` | Chặn vận hành thật: người dùng không làm được việc chính, sai tiền, sai dữ liệu. Làm ngay. |
| `P1` | Đau hàng ngày, có cách lách tạm. Sprint này. |
| `P2` | Cải thiện rõ nhưng chờ được. |
| `P3` | Nice-to-have. Chỉ làm khi trống lịch. |

`P0` bất kể `type` — một bug P0 và một tính năng P0 xếp cùng hàng.
