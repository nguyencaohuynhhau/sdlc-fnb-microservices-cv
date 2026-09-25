# Cấu trúc thư mục

> File này tồn tại để Agent khỏi `find` mò mỗi lần — [AGENTS.md](../../AGENTS.md) §1 bắt buộc
> cập nhật nó khi thêm module hoặc workspace mới. Mô tả **vị trí và trách nhiệm**, không liệt kê từng file.

## Gốc repo

- `backend/` — .NET 10: 3 dịch vụ (identity, ordering, cashier) + gateway YARP + seeder. `package.json` chỉ là shim để cổng SDLC gọi `dotnet`.
- `web/` — SPA React 19 (POS + bảng bếp), build ra nginx.
- `docker-compose.yml` — toàn bộ stack: postgres, kafka, redis, 3 dịch vụ, gateway, web. Chỉ gateway (8080), web (5173) và postgres (54329, cho seeder) mở ra `127.0.0.1`.
- `docker/postgres-init.sql` — tạo 5 database, mỗi dịch vụ một cái.
- `scripts/sdlc/` — script cổng chất lượng và hook (thuộc bộ khung, hiếm khi sửa)
- `docs/` — hiện vật quy trình:
  - `intents/` — chuỗi `intent.md → spec.md → plan.md` của từng đơn vị công việc
  - `evidence/` — ảnh chụp và log do build/verify sinh ra
  - `evals/` — bộ đánh giá hồi quy cho chính quy trình (`public-endpoints.json` = danh sách endpoint public được phép)
  - `architecture/` — cấu trúc mã nguồn (chính file này)
  - `api/endpoints.md` — hợp đồng HTTP + SignalR
  - `database/schema.md` — bảng của từng database
  - `design/DESIGN_SYSTEM.md` — token giao diện

## `backend/`

Mỗi dịch vụ là 4 project theo Clean Architecture; phụ thuộc chỉ đi vào trong
(`Api → Application → Domain`, `Infrastructure → Application`). Dịch vụ không tham chiếu project của dịch vụ khác —
nói chuyện qua Kafka (sự kiện) hoặc HTTP qua gateway.

```
src/
├── Shared/
│   ├── Shared.Kernel/      # Entity, DomainException (409/404/400), IntegrationEvent, tên topic Kafka
│   ├── Shared.Messaging/   # outbox (interceptor + publisher nền), inbox + consumer nền cho Kafka
│   └── Shared.Web/         # JWT, FallbackPolicy [Authorize], ProblemDetails tiếng Việt, /healthz
├── Identity/               # đăng nhập, refresh token xoay vòng, khoá tài khoản (Redis)
├── Ordering/               # thực đơn (cache Redis), đơn + món, ETag/If-Match, hub SignalR /hubs/orders,
│                           #   hình chiếu ca từ sự kiện cashier (known_shifts)
├── Cashier/                # mở/đóng ca; unique index giữ "chỉ một ca mở"
│   └── <Svc>.{Api,Application,Domain,Infrastructure}/   # Infrastructure/Migrations = migration EF Core
└── Gateway/                # YARP: route, JWT, CORS allowlist, /healthz gộp. Route khai báo trong Program.cs
tools/Seeder/               # dữ liệu demo, chỉ chạy khi ASPNETCORE_ENVIRONMENT=Development
tests/
├── *.UnitTests/            # domain thuần, không I/O
├── *.IntegrationTests/     # Testcontainers (Postgres/Kafka/Redis thật), [Trait("Category","Integration")]
├── Gateway.Tests/          # gateway thật + dịch vụ giả trên Kestrel cổng ngẫu nhiên
└── Shared.Messaging.Tests/
Dockerfile                  # một Dockerfile cho mọi dịch vụ: --build-arg PROJECT=<tên project>
Directory.Packages.props    # phiên bản NuGet tập trung
```

## `web/`

```
src/
├── routes/                 # TanStack Router file-based: login, _auth (guard) → pos, kitchen
├── features/<tính năng>/   # menu, orders, shift, kitchen: component + hook TanStack Query
├── lib/                    # apiClient (fetch duy nhất, refresh 401), auth, signalr, online, queryKeys
├── stores/ui.ts            # Zustand: chỉ UI state (giỏ đơn nháp)
├── components/ui/          # component shadcn/ui
└── **/__tests__/           # vitest
e2e/                        # Playwright chạy trên stack compose đang sống (http://localhost:5173)
nginx.conf                  # serve dist/, proxy /api /healthz /hubs sang gateway
```
