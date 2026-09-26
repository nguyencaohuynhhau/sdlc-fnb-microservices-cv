---
id: 01-260925-fnb-pos-core
intent: ./intent.md
spec: ./spec.md
status: shipped
shipped: 2026-09-25
pr: https://github.com/nguyencaohuynhhau/sdlc-fnb-microservices-cv/pull/1
branch: feat/01-260925-fnb-pos-core-slice-a
generated_by: /sdlc:plan
created: 2026-09-25
slice: A — Nền + đặt món & bếp (spec §11). Lát B/C/D có plan riêng sau khi A ship.
---

# Plan: Lát A — Nền tảng + đặt món & bếp

> **Bài kiểm tra bàn giao:** một agent hoặc kỹ sư chưa từng đọc hội thoại nào của dự án này,
> chỉ với `AGENTS.md` + `spec.md` + file này, phải làm được đúng việc. Nếu chưa, plan chưa xong.

> **Phạm vi plan này = lát A trong spec §11**: compose, gateway, `identity`, `ordering`,
> `cashier` (chỉ mở/đóng ca), web shell + POS + màn bếp, SignalR, outbox/inbox dùng chung,
> seeder. Đạt tiêu chí chấp nhận **#1, #2, #3** và phần "gán ca cho đơn" của **#9**.
> Thu tiền, tồn kho, báo cáo **không** nằm ở đây.

> **Repo hiện rỗng.** Không có dòng code nào trước plan này. Mọi "sửa" ở mục 1 là sửa file
> của bộ khung SDLC; mọi thứ khác là tạo mới. Toolchain đã có trên máy dev: .NET SDK 10.0.401,
> Node 26, Docker 29 + Compose v5.5.

## 0. Bằng chứng thành công (đọc trước tiên)

Tác vụ chỉ hoàn thành khi **toàn bộ** lệnh sau chạy xanh, từ gốc repo, trên working tree sạch:

```bash
node docs/evals/run.mjs                      # E01–E05 + P01–P03 xanh
npm run sdlc:verify -- --all                 # backend: format+build+test · web: lint+build+test
docker compose up -d --build                 # 8 container, tất cả (healthy) trong ≤ 120s
npm --prefix backend run seed                # "Seeded: 3 users, 20 menu items, 1 closed shift, 1 open shift"
npm run sdlc:verify -- --all --e2e           # + backend integration (Testcontainers) + web Playwright
```

Cộng thêm các bằng chứng cụ thể của lát này:

- [x] `backend/tests/Ordering.IntegrationTests/ConcurrencyTests.cs` — ca `Order_ConcurrentUpdate_SecondWriterGets409` đỏ khi bỏ `.UseXminAsConcurrencyToken()`, xanh khi có
- [x] `backend/tests/Ordering.IntegrationTests/OutboxTests.cs` — ca `Outbox_CrashBeforePublish_PublishesOnRestart` đỏ khi publisher không quét `published_at IS NULL` lúc khởi động
- [x] `backend/tests/Cashier.IntegrationTests/ShiftTests.cs` — ca `OpenShift_Concurrent_OnlyOneSucceeds` đỏ khi bỏ unique partial index
- [x] `backend/tests/Identity.IntegrationTests/AuthTests.cs` — ca `Login_TenFailures_Returns423` và `Refresh_ReusedToken_RevokesFamily`
- [x] `web/e2e/order-to-kitchen.spec.ts` — mốc thời gian từ bấm "Gửi bếp" tới đơn xuất hiện ở tab bếp **< 2000ms**, và từ bếp bấm "Xong" tới POS đổi trạng thái **< 2000ms**; chạy **qua gateway 8080**, không gọi thẳng 8082
- [x] `web/e2e/offline-banner.spec.ts` — chặn mạng → banner + nút "Gửi bếp" disabled; mở lại → tự hồi phục, không reload
- [x] Ảnh trong `docs/evidence/01-260925-fnb-pos-core/`: `a-compose-ps.png`, `a-login-error.png`, `a-pos-empty-menu.png`, `a-pos-order.png`, `a-kitchen-board.png`, `a-offline-banner.png`, `a-conflict-409.png`, `a-no-open-shift.png`
- [x] Sau `docker compose up` xong, `git status --porcelain -uall | grep -E '(^|/)(bin|obj|dist|node_modules)/'` **rỗng** (không có build output lọt vào cổng)
- [x] Các test trong `requiredTests` (khai báo ở B0) tồn tại và xanh

_Verify 2026-09-25 @ `cc7c886`: 81/81 test + 8/8 eval xanh — xem `docs/evidence/01-260925-fnb-pos-core/verify.md`._

## 1. Các file sẽ chạm

### 1a. File của bộ khung (sửa)

| File | Hành động | Mục đích |
|------|-----------|----------|
| `sdlc.config.json` | sửa | `codeExtensions` thêm `.cs .csproj .sln .props .proto .yml .yaml`; gate backend → `format`/`build`/`test`; bỏ `lintReportCommand` của backend; `e2eGate` cả hai ws; `e2eTriggers`; `requiredTests` |
| `.gitignore` | sửa | thêm `bin/ obj/ dist/ .env *.user test-results/ playwright-report/`. **Không** ignore `web/src/routeTree.gen.ts` — file sinh này được commit (xem W1) |
| `.github/workflows/ci.yml` | sửa | job `quality[backend]` thêm `actions/setup-dotnet@v4` (10.0.x); job `artifact-chain` sửa regex `docs/intents/[0-9]{6}-` → `docs/intents/[0-9]{2}-[0-9]{6}-`; `Audit dependency` giữ nguyên (backend không có lock có dep) |
| `docs/evals/project-evals.mjs` | tạo | P01 `.cs` ∈ `codeExtensions`; P02 không `[AllowAnonymous]` ngoài danh sách cho phép; P03 không `tailwind.config.*` |
| `docs/evals/public-endpoints.json` | tạo | baseline cho P02: `login`, `refresh`, `healthz` |
| `docs/architecture/STRUCTURE.md` | sửa | cấu trúc thật `backend/`, `web/` |
| `docs/database/schema.md` | tạo | từ spec §3, phần lát A |
| `docs/api/endpoints.md` | tạo | từ spec §4, phần lát A |
| `docs/design/DESIGN_SYSTEM.md` | tạo | token shadcn/ui đang dùng, quy tắc không thêm token |
| `README.md` | sửa | thay README của bộ khung bằng README dự án: cách chạy, sơ đồ kiến trúc, bảng đối chiếu JD (điền phần lát A, để chỗ trống cho B/C/D) |
| `CHANGELOG.md` | sửa | mục mới (bước 5 của `/sdlc:build`) |
| `.env.example` | tạo | `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `CORS_ORIGINS`, `SEED_PASSWORD` |
| `docker-compose.yml` | tạo | `postgres`, `kafka`, `redis`, `gateway`, `identity`, `ordering`, `cashier`, `web` |
| `docker/postgres-init.sql` | tạo | `CREATE DATABASE fnb_identity/fnb_ordering/fnb_cashier/fnb_inventory/fnb_reporting` |

### 1b. `backend/` (tạo mới toàn bộ)

```
backend/
├── package.json                 # shim để verify.mjs/CI nhìn thấy ws: build/test/format/test:e2e/seed → dotnet
├── package-lock.json            # npm ci trong CI cần file này dù không có dep
├── FnbPos.sln
├── Directory.Build.props        # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
├── Directory.Packages.props     # central package management — MỌI version ở đây
├── .editorconfig                # dotnet format đọc file này
├── Dockerfile                   # một Dockerfile, ARG PROJECT=<đường dẫn csproj>
├── src/
│   ├── Gateway/                             Program.cs, appsettings.json (YARP routes), Gateway.csproj
│   ├── Shared/
│   │   ├── Shared.Kernel/                   DomainException.cs, IClock.cs, Entity.cs
│   │   └── Shared.Messaging/                IntegrationEvent.cs, OutboxMessage.cs, InboxMessage.cs,
│   │                                        OutboxPublisherHost.cs, KafkaConsumerHost.cs, KafkaProducer.cs,
│   │                                        MessagingDbConfig.cs (EF config outbox/inbox dùng chung),
│   │                                        ServiceCollectionExtensions.cs, Topics.cs
│   ├── Identity/
│   │   ├── Identity.Domain/                 User.cs, RefreshToken.cs, Role.cs
│   │   ├── Identity.Application/            LoginHandler.cs, RefreshHandler.cs, ITokenService.cs, IUserRepository.cs
│   │   ├── Identity.Infrastructure/         IdentityDbContext.cs, Migrations/, JwtTokenService.cs, UserRepository.cs, LoginAttemptStore.cs (Redis)
│   │   └── Identity.Api/                    Program.cs, Controllers/AuthController.cs, Contracts/LoginRequest.cs …
│   ├── Ordering/
│   │   ├── Ordering.Domain/                 Order.cs, OrderItem.cs, MenuItem.cs, OrderStatus.cs, OrderItemStatus.cs, Events/OrderCancelled.cs
│   │   ├── Ordering.Application/            CreateOrderHandler.cs, AddItemHandler.cs, CancelItemHandler.cs, CancelOrderHandler.cs,
│   │   │                                    SetItemStatusHandler.cs, GetMenuHandler.cs, IOrderRepository.cs, ICurrentShift.cs, IOrderNotifier.cs
│   │   ├── Ordering.Infrastructure/         OrderingDbContext.cs, Migrations/, OrderRepository.cs, CurrentShiftProjection.cs,
│   │   │                                    ShiftEventsConsumer.cs, MenuCache.cs (Redis), SignalROrderNotifier.cs
│   │   └── Ordering.Api/                    Program.cs, Controllers/{MenuController,OrdersController,KitchenController}.cs,
│   │                                        Hubs/OrdersHub.cs, Contracts/*.cs, ETagFilter.cs (If-Match ↔ xmin)
│   └── Cashier/
│       ├── Cashier.Domain/                  Shift.cs, Events/{ShiftOpened,ShiftClosed}.cs
│       ├── Cashier.Application/             OpenShiftHandler.cs, CloseShiftHandler.cs, IShiftRepository.cs
│       ├── Cashier.Infrastructure/          CashierDbContext.cs, Migrations/, ShiftRepository.cs
│       └── Cashier.Api/                     Program.cs, Controllers/ShiftsController.cs, Contracts/*.cs
├── tools/Seeder/                            Program.cs (users, menu, 1 ca đóng, 1 ca mở) — chỉ chạy khi Development
└── tests/
    ├── Ordering.UnitTests/OrderTests.cs
    ├── Ordering.IntegrationTests/{ConcurrencyTests,OutboxTests,ShiftProjectionTests}.cs, Fixtures/PostgresKafkaFixture.cs
    ├── Cashier.UnitTests/ShiftTests.cs
    ├── Cashier.IntegrationTests/ShiftTests.cs
    ├── Identity.IntegrationTests/AuthTests.cs
    └── Shared.Messaging.Tests/InboxDedupTests.cs
```

`CloseShiftHandler` ở lát A **chỉ** đóng ca và phát `ShiftClosed`; `counted_cash`/`variance` để
null — lát B điền. Cột vẫn tạo ngay trong migration để lát B không phải migrate lại.

### 1c. `web/` (tạo mới toàn bộ)

```
web/
├── package.json, package-lock.json, vite.config.ts, tsconfig.json, index.html
├── eslint.config.js            # flat config; `npm run lint` = `eslint . -f json` cho lint-ratchet
├── components.json             # shadcn/ui
├── playwright.config.ts        # baseURL http://localhost:8080 (qua gateway), webServer: none (compose đã chạy)
├── src/
│   ├── main.tsx, index.css (@import "tailwindcss")
│   ├── routeTree.gen.ts        # do @tanstack/router-plugin sinh — COMMIT file này để CI không cần bước gen
│   ├── routes/__root.tsx, login.tsx, _auth.tsx (guard), _auth/pos.tsx, _auth/kitchen.tsx, _auth/shift.tsx
│   ├── lib/apiClient.ts, queryClient.ts, queryKeys.ts, signalr.ts, auth.ts, online.ts
│   ├── stores/ui.ts            # zustand: sidebar, theme, dialog — KHÔNG có dữ liệu API
│   ├── features/menu/{useMenu.ts,MenuGrid.tsx}
│   ├── features/orders/{useOrders.ts,OrderPanel.tsx,orderSchemas.ts}
│   ├── features/kitchen/{KitchenBoard.tsx,useKitchenFeed.ts}
│   ├── features/shift/{useShift.ts,ShiftBar.tsx}
│   ├── components/OfflineBanner.tsx
│   └── components/ui/*         # shadcn: button, card, dialog, badge, input, form, table, sonner
├── src/**/__tests__/*.test.ts  # vitest: apiClient (refresh khi 401), orderSchemas, online
└── e2e/{order-to-kitchen,offline-banner,login}.spec.ts
```

**Cập nhật khi build (B1):**
- Thêm `backend/src/Shared/Shared.Web/` (`JwtSettings.cs`, `WebExtensions.cs`): cấu hình JWT, FallbackPolicy, ProblemDetails tiếng Việt và `/healthz` dùng chung cho 3 dịch vụ + gateway, thay vì chép 4 lần. `public-endpoints.json` trỏ tới file này thay cho các `Program.cs`.
- Bỏ `IClock.cs`: dùng `TimeProvider` có sẵn của .NET. `IntegrationEvent.cs` và `Topics.cs` chuyển sang `Shared.Kernel` để project Domain khai báo sự kiện mà không phụ thuộc EF/Kafka.
- `FluentAssertions` ghim 7.2.0 (Apache-2.0); bản 8.x đổi sang giấy phép thương mại.

**Cập nhật khi build (B7):** thêm `backend/tests/Gateway.Tests/{GatewayFixture,GatewayTests}.cs`; `Gateway/appsettings.json` không cần (route trong `Program.cs`).

**Cập nhật khi build (B8):** thêm `backend/tools/Seeder/{Seeder.csproj,DemoData.cs}`, `backend/tests/Seeder.IntegrationTests/SeederTests.cs`, dòng `ASPNETCORE_ENVIRONMENT` trong `.env.example`.

**Cập nhật khi build (I3):** thêm `backend/src/{Ordering,Cashier}/*.Infrastructure/Migrations/*_MoneyPrecision18*.cs` (tiền về `numeric(18,2)` như §4).

**Cập nhật khi build (W):** không có `routes/_auth/shift.tsx`; `components/ui/` là `badge, button, card, input, label, sonner`; thêm `src/components/__tests__/OfflineBanner.test.tsx`, `e2e/helpers.ts`, `web/nginx.conf`; `playwright.config.ts` baseURL `http://localhost:5173`.

**Cập nhật khi build (B6):** gộp file nhỏ cho ít file hơn, hành vi giữ nguyên.
- `Ordering.Domain`: `Order.cs` chứa luôn `OrderItem`, hai enum trạng thái và `OrderCancelled`; còn `MenuItem.cs`.
- `Ordering.Application`: năm handler gộp thành `OrderService.cs` (chung một khuôn tìm → so phiên bản → sửa → lưu → báo); `Abstractions.cs` (4 interface) + `Views.cs` (DTO trả ra).
- `Ordering.Infrastructure`: `ShiftEventsConsumer.cs` chứa cả bản sao hợp đồng sự kiện ca, entity `KnownShift` và `CurrentShiftReader` (thay `CurrentShiftProjection.cs`); `MenuCatalog.cs` thay `MenuCache.cs`; thêm `DependencyInjection.cs`, `DesignTimeFactory.cs`.
- `Ordering.Api`: không có `KitchenController` — `GET /api/kitchen/orders` nằm trong `OrdersController`; `SignalROrderNotifier` nằm trong `Hubs/OrdersHub.cs` vì cần kiểu Hub; thêm `ETagFilter.cs` và `Contracts/OrderContracts.cs`.
- Test: fixture là `tests/Ordering.IntegrationTests/OrderingFixture.cs` (Postgres + Redis + Kafka) thay `Fixtures/PostgresKafkaFixture.cs`.
- `Shared.Kernel/DomainException.cs` + `Shared.Web/WebExtensions.cs`: thêm `InvalidRequestException` → 400.

**Không** chạm tới file nào ngoài danh sách này mà không cập nhật plan trước. Đặc biệt:
**không sửa** `scripts/sdlc/*.mjs`, `AGENTS.md`, `.claude/**` — đó là file của con người.

## 2. Các bước thực thi

Đánh dấu `[x]` ngay khi xong từng bước, trước khi sang bước sau. Ba nhóm: **B** (khung +
backend), **W** (web), **I** (tích hợp). Xem mục 3 để biết nhóm nào chạy song song.

### Nhóm B0 — Khung (làm đầu tiên, trên nhánh chính của lát, trước khi tách worktree)

- [x] **B0.1** Sửa `sdlc.config.json` theo bảng 1a. Gate backend: `{"format":"npm run format","build":"npm run build","test":"npm test"}`, `e2eGate: "npm run test:e2e"`; web: `{"lint":"node ../scripts/sdlc/lint-ratchet.mjs web","build":"npm run build","test":"npm test"}`, `e2eGate: "npm run test:e2e"`, `lintReportCommand: "npm run lint -- -f json"`. `e2eTriggers: ["backend/src/Identity","backend/src/Ordering/Ordering.Domain","backend/src/Cashier/Cashier.Domain","backend/src/Shared/Shared.Messaging","web/src/lib/apiClient.ts"]`. `requiredTests`: 4 file integration ở mục 0.
  → kiểm chứng: `node -e "const c=require('./sdlc.config.json');if(!c.codeExtensions.includes('.cs'))process.exit(1)"` thoát 0.
  _Ghi chú khi build:_ `requiredTests` để rỗng ở B0 vì eval E05 đỏ khi file test chưa tồn tại (B0.4 đòi 8/8 xanh); điền 4 file ở cuối B6, khi cả bốn đã có.
- [x] **B0.2** Tạo `backend/package.json` shim: `build: dotnet build FnbPos.sln -warnaserror`, `test: dotnet test FnbPos.sln --no-build --filter "Category!=Integration"`, `test:e2e: dotnet test FnbPos.sln --filter "Category=Integration"`, `format: dotnet format FnbPos.sln --verify-no-changes`, `seed: dotnet run --project tools/Seeder`. Chạy `npm install` trong `backend/` để sinh `package-lock.json`.
  → kiểm chứng: `cd backend && npm ci` thoát 0 (CI dùng đúng lệnh này).
- [x] **B0.3** Sửa `.gitignore`, tạo `.env.example`, sửa `ci.yml` (setup-dotnet cho backend; regex artifact-chain).
  → kiểm chứng: `echo "xem docs/intents/01-260925-fnb-pos-core/" | grep -oE 'docs/intents/[0-9]{2}-[0-9]{6}-[a-z0-9-]+'` in ra đúng id.
- [x] **B0.4** Tạo `docs/evals/project-evals.mjs` (P01 `.cs` trong codeExtensions; P02 `[AllowAnonymous]` chỉ ở file trong `public-endpoints.json`; P03 không `tailwind.config.*`) + `public-endpoints.json`.
  → kiểm chứng: `node docs/evals/run.mjs` xanh 8/8.
- [x] **B0.5** Commit B0 (chỉ chạm config/docs nên hook không chặn) với message `chore(sdlc): adapt gates for .NET backend + React web`.
  _Ghi chú khi build:_ `backend/package.json` là file mã thuộc workspace backend nên kéo verify chạy `dotnet format` khi chưa có solution; shim được commit cùng B1 thay vì B0.

### Nhóm B — Backend

- [x] **B1** Skeleton solution: `FnbPos.sln`, `Directory.Build.props`, `Directory.Packages.props` (ghi **mọi** version ở mục 4), `.editorconfig`, `Shared.Kernel`, `Shared.Messaging` (mới có kiểu dữ liệu, chưa có host).
  → kiểm chứng: `npm --prefix backend run build` xanh với `-warnaserror`; `npm --prefix backend run format` xanh.
- [x] **B2** `docker-compose.yml` phần hạ tầng: `postgres:17` + `docker/postgres-init.sql`, `apache/kafka:3.9` (KRaft, 1 node, `KAFKA_AUTO_CREATE_TOPICS_ENABLE=true`), `redis:7`. Healthcheck cho cả ba.
  → kiểm chứng: `docker compose up -d postgres kafka redis && docker compose ps` — 3 dòng `healthy` trong ≤ 60s; `docker compose exec postgres psql -U postgres -lqt | grep -c fnb_` = 5.
  _Ghi chú khi build:_ Postgres publish `127.0.0.1:54329` (chỉ máy dev) để seeder chạy từ host đọc/ghi được; cổng 55432 đã có container khác giữ.
- [x] **B3** `Identity`: entity, `IdentityDbContext` + migration, `PasswordHasher<User>`, JWT (HS256, key từ env ≥ 32 byte, `iss`/`aud` cố định, access 60′), refresh token rotate + hash SHA-256 + revoke family khi reuse, khoá 15′ sau 10 lần sai (đếm trong Redis, key `login-fail:{username}`), `FallbackPolicy = RequireAuthenticatedUser`, `[AllowAnonymous]` chỉ ở `login`/`refresh`/`healthz`. Thông báo lỗi tiếng Việt theo spec §4.
  → kiểm chứng: `dotnet test --filter Identity.IntegrationTests` xanh (Testcontainers Postgres + Redis): `Login_WrongPassword_401_SameMessageAsUnknownUser`, `Login_TenFailures_Returns423`, `Refresh_Rotates_OldTokenRejected`, `Refresh_ReusedToken_RevokesFamily`. Cố tình bỏ kiểm `revoked_at` → `Refresh_ReusedToken_RevokesFamily` phải đỏ.
- [x] **B4** `Shared.Messaging` đầy đủ: `OutboxPublisherHost` (quét `published_at IS NULL` mỗi 500ms **và ngay lúc khởi động**, publish qua `Confluent.Kafka` với `EnableIdempotence=true`, `Acks=All`), `KafkaConsumerHost<TEvent>` (commit offset **sau** khi handler + inbox ghi trong cùng transaction; lỗi → không commit, retry với backoff), `MessagingDbConfig` map `outbox_messages`/`inbox_messages` cho mọi DbContext.
  → kiểm chứng: `Shared.Messaging.Tests.InboxDedupTests` (cùng `messageId` 3 lần → handler chạy 1 lần) và `Ordering.IntegrationTests.OutboxTests.Outbox_CrashBeforePublish_PublishesOnRestart` (ghi outbox, không chạy host, rồi start host → message lên topic đúng 1 lần).
  _Ghi chú khi build:_ `InboxDedupTests` (2 ca, có ca outbox ghi cùng SaveChanges) xanh ở B4; `OutboxTests.Outbox_CrashBeforePublish_PublishesOnRestart` viết ở B6 vì cần `OrderingDbContext` thật. `KafkaProducer.cs` gộp vào `ServiceCollectionExtensions.cs` (chỉ là một đăng ký DI); thêm `OutboxInterceptor.cs` để aggregate chỉ cần `Raise(...)`. Consumer tự tạo topic lúc khởi động — nếu không, librdkafka chờ tới 5 phút mới thấy topic mới.
- [x] **B5** `Cashier` (ca làm việc): `Shift` aggregate, migration với **unique partial index** `shifts(closed_at) WHERE closed_at IS NULL`, `POST /api/shifts/open`, `POST /api/shifts/{id}/close`, `GET /api/shifts/current`, phát `ShiftOpened`/`ShiftClosed` qua outbox. Lỗi 409 tiếng Việt theo spec.
  → kiểm chứng: `Cashier.UnitTests.ShiftTests` (không đóng ca đã đóng; không mở khi đang mở) + `Cashier.IntegrationTests.ShiftTests.OpenShift_Concurrent_OnlyOneSucceeds` (10 task song song → 1 thành công, 9 nhận 409). Bỏ index → test phải đỏ.
  _Ghi chú khi build:_ Index phải là `UNIQUE (closed_at) NULLS NOT DISTINCT WHERE closed_at IS NULL` (`AreNullsDistinct(false)`) — Postgres mặc định coi các NULL là khác nhau nên index đúng như spec ghi **không chặn gì**; đã chứng minh: bỏ `NULLS NOT DISTINCT` hoặc bỏ index → 10/10 request mở ca đều 201, test đỏ. `ShiftOpened`/`ShiftClosed` đặt cuối `Shift.cs` thay vì thư mục `Events/` (hai record 4 dòng). Thêm `tests/Cashier.IntegrationTests/CashierFixture.cs` (Postgres container + tự ký JWT test).
- [x] **B6** `Ordering`: `MenuItem`, `Order`, `OrderItem` (giá/tên chụp lại lúc gọi), `OrderingDbContext` + migration (`orders.xmin` làm concurrency token, `current_shift` 0-1 dòng), `ShiftEventsConsumer` cập nhật `current_shift`, `CreateOrder` chặn khi không có ca (409 "Chưa mở ca làm việc. Mở ca trước khi nhận đơn."), `If-Match` ↔ `xmin` qua `ETagFilter` (thiếu header → 428; lệch → 409 tiếng Việt theo spec), huỷ món chỉ khi `Pending`, huỷ đơn chỉ khi `Open`, `PATCH .../status` cho role `Kitchen` theo đúng thứ tự `Pending→Preparing→Done`, `OrdersHub` (SignalR, group `shift:{shiftId}`, sự kiện `orderCreated`, `orderItemStatusChanged`, `orderCancelled`), `GET /api/menu` cache Redis key `menu:v1` TTL 60s.
  → kiểm chứng: `Ordering.UnitTests.OrderTests` (tổng tiền; huỷ theo trạng thái; thứ tự trạng thái món) + `Ordering.IntegrationTests.ConcurrencyTests.Order_ConcurrentUpdate_SecondWriterGets409` + `ShiftProjectionTests.OrderCreation_WithoutOpenShift_Returns409` và `..._AfterShiftOpenedEvent_Succeeds`. Bỏ `.UseXminAsConcurrencyToken()` → `ConcurrencyTests` phải đỏ.
  _Ghi chú khi build:_ **Sửa spec:** bảng `current_shift` 0-1 dòng thay bằng `known_shifts(shift_id, opened_at, closed_at)` — `ShiftOpened`/`ShiftClosed` đi trên hai topic khác nhau nên Kafka không giữ thứ tự giữa chúng; "đóng" tới trước "mở" sẽ để lại ca ma nếu chỉ giữ một dòng (test `ShiftClosedArrivingBeforeOpened_ShiftStaysClosed`). Đột biến đã chạy, đều đỏ: bỏ so sánh If-Match (2 test), bỏ `IsRowVersion` + migration tạm (3 test), nuốt xung đột xmin (2), publisher bỏ dòng cũ (OutboxTests), sự kiện mở xoá mốc đóng (1), không đăng ký consumer ShiftOpened (2). `orders.updated_at` thêm để sửa món nào cũng UPDATE dòng `orders` → xmin đổi. Thêm `InvalidRequestException` (400) vào Shared.Kernel/Web cho món không có trong thực đơn. Thông điệp mới 409 "Đơn đã đóng, không sửa được nữa." (sửa món trên đơn đã thanh toán/huỷ). Cột tên món là `name` (snake_case ghi đè `name_snapshot`). Hợp đồng sự kiện ca chép vào ordering, không tham chiếu Cashier.Domain; test tích hợp phát sự kiện bằng `Cashier.Domain.ShiftOpened` thật qua Kafka để khoá hình dạng message.
- [x] **B7** `Gateway` (YARP): route `/api/auth/*`→identity, `/api/menu`, `/api/orders/*`, `/api/kitchen/*`, `/hubs/*`→ordering (WebSocket), `/api/shifts/*`→cashier, `/healthz` tổng hợp; JWT validate tại gateway **và** dịch vụ; với `/hubs/*` nhận token từ query `access_token` (trình duyệt không đặt header cho WebSocket) và **tắt log query string** ở gateway; CORS đọc `CORS_ORIGINS` từ env.
  → kiểm chứng: qua 8080: `login` → `GET /api/menu` với Bearer → 200; không Bearer → 401; `wscat`/curl upgrade `/hubs/orders?access_token=…` → 101; log gateway không chứa chuỗi token.
  _Ghi chú khi build:_ Route khai báo bằng code trong `Program.cs` thay vì `appsettings.json`, để mặt public (chỉ `/api/auth/login`, `/api/auth/refresh`, `/healthz`) nằm trong đúng file eval P02 canh — JSON thì grep không thấy. Thêm `tests/Gateway.Tests` (21 test, không cần Docker): gateway chạy Kestrel thật trỏ vào 3 dịch vụ giả, kiểm route → đúng dịch vụ, 401 khi thiếu/giả token, token query chỉ nhận ở `/hubs`, **WebSocket nâng cấp xuyên gateway**, log không chứa token, CORS chỉ origin cấu hình, `/healthz` 503 khi một dịch vụ chết. Đột biến (bỏ lọc log; login cần auth; `/hubs` anonymous; healthz luôn 200; token query mọi path; mọi origin) → mỗi cái 1 test đỏ. Kiểm chứng qua cổng 8080 với dịch vụ thật chạy lại ở I1 khi compose đủ.
- [x] **B8** `tools/Seeder`: từ chối chạy nếu `ASPNETCORE_ENVIRONMENT != Development`; idempotent (chạy 2 lần không nhân đôi); tạo `owner`/`cashier`/`kitchen` với mật khẩu từ `SEED_PASSWORD`, 20 món cà phê/trà/bánh, 1 ca đã đóng hôm qua, 1 ca đang mở, 3 đơn `Open` trong ca đang mở.
  → kiểm chứng: `npm --prefix backend run seed` hai lần → dòng tóm tắt giống nhau, `SELECT count(*) FROM menu_items` = 20.
  _Ghi chú khi build:_ Logic nằm ở `tools/Seeder/DemoData.cs` để test gọi thẳng; `Program.cs` chỉ đọc `.env` (biến môi trường đã đặt thì thắng), chặn môi trường, dựng connection string `localhost:54329`. Seed ghi thẳng DB (không qua outbox) nên tự chép mọi ca của cashier sang `known_shifts` của ordering — POS thấy ca mở ngay, không chờ Kafka. Mỗi phần chỉ thêm cái còn thiếu (user theo tên, món theo tên, ca khi cashier chưa có ca nào, đơn khi ca mở chưa có đơn); mật khẩu user đã có không bị ghi đè. `Bánh flan` hết hàng sẵn để demo luật hết món. Dòng tóm tắt thêm `3 open orders`. Thêm `tests/Seeder.IntegrationTests` (6 test; đột biến bỏ từng chốt idempotent / bỏ chép ca / cho mọi môi trường → đều đỏ). `.env.example` thêm `ASPNETCORE_ENVIRONMENT=Development`. Chạy 2 lần trên compose thật ở I1.
- [x] **B9** `backend/Dockerfile` (multi-stage, `ARG PROJECT`) + 4 service app trong compose (`gateway` publish `8080:8080`, ba dịch vụ **không** publish cổng ra host), `depends_on: condition: service_healthy`, env từ `.env`.
  → kiểm chứng: `docker compose up -d --build` từ trạng thái `docker compose down -v` → `docker compose ps` 7 container healthy trong ≤ 120s; `curl localhost:8080/healthz` = 200; `curl localhost:8082` **từ host** bị từ chối kết nối.
  _Ghi chú khi build:_ Từ `down -v`: 7 container healthy sau **34s**; `/healthz` gateway = 200 `{identity,ordering,cashier: ok}`; `localhost:8082` bị từ chối. Kiểm lại B7 qua 8080 với dịch vụ thật: login `cashier` → `/api/menu` 200, không Bearer 401, `/hubs/orders?access_token=…` nâng cấp **101**, log gateway và ordering không chứa token. Seed 2 lần trên compose → hai dòng tóm tắt giống hệt, `menu_items` = 20. `ARG PROJECT` là tên project (`Ordering.Api`), Dockerfile tự `find` csproj; image chạy user `app` (không root). Healthcheck dùng bash `/dev/tcp` vì image aspnet không có curl. Gateway bind `127.0.0.1:8080` thay vì mọi interface (máy khác trong LAN không gọi được). Thêm `backend/.dockerignore`.

### Nhóm W — Web (worktree riêng, xem mục 3)

- [x] **W1** Scaffold: Vite + React 19 + TS strict, `babel-plugin-react-compiler` qua `@vitejs/plugin-react`, `@tailwindcss/vite` + `index.css` (`@import "tailwindcss"`), `shadcn` init (style default, CSS variables), `@tanstack/router-plugin` (file-based, sinh `routeTree.gen.ts` — commit file này), eslint flat config, vitest, playwright. `npm run lint` = `eslint . -f json` khi được truyền `-f json` (lint-ratchet gọi `npm run lint -- -f json`).
  _Ghi chú khi build:_ chỉ tạo 6 component shadcn thật sự dùng (`badge, button, card, input, label, sonner`); `dialog/form/table` chưa cần nên chưa cài `radix-ui`, `lucide-react`, `@tanstack/react-table`. Thêm 4 devDependency ngoài danh sách (`@types/react`, `@types/react-dom`, `@types/node`, `@testing-library/dom`) — TS strict và `@testing-library/react` 16 bắt buộc; **chờ người điều phối duyệt** (xem §4).
  → kiểm chứng: `npm run build` xanh; `node scripts/sdlc/lint-ratchet.mjs web` in "không có lỗi lint mới"; `ls web/tailwind.config.*` không có gì.
- [x] **W2** `lib/`: `apiClient.ts` (base `/api`, đính Bearer, khi 401 gọi `/api/auth/refresh` **một lần** rồi retry, hai request 401 cùng lúc chỉ refresh một lần, `ProblemDetails.detail` → thông báo cho UI), `queryClient.ts` (`staleTime: 30_000`, `retry: 1`), `queryKeys.ts` (factory duy nhất: `menu`, `orders.list(shiftId)`, `orders.detail(id)`, `shift.current`), `auth.ts` (token trong memory + refresh trong `localStorage`, `beforeLoad` guard ở `_auth.tsx`), `online.ts` (`onlineManager` + ping `/healthz` 10s), `signalr.ts` (kết nối `/hubs/orders?access_token=`, auto-reconnect, hàm `onOrderEvent` invalidate query key tương ứng).
  _Ghi chú khi build:_ test ở `src/lib/__tests__/{apiClient,online}.test.ts`; `apiClient` có thêm ca 204 → `null`.
  → kiểm chứng: `vitest run src/lib` xanh: `apiClient_401_refreshesOnceThenRetries`, `apiClient_ParallelRefresh_SingleFlight`, `online_HealthzFails_MarksOffline`.
- [x] **W3** `/login`: TanStack Form + Zod (`username` 3–50, `password` ≥ 8), hiện `detail` của 401/423, sau login về `/pos` (Cashier/Owner) hoặc `/kitchen` (Kitchen).
  → kiểm chứng: `e2e/login.spec.ts` sai mật khẩu → thấy đúng câu "Tên đăng nhập hoặc mật khẩu không đúng."
- [x] **W4** `/pos`: `ShiftBar` (ca hiện tại / nút mở ca / đóng ca), `MenuGrid` (badge "Hết" khi `!isAvailable`, disabled), `OrderPanel` (đơn đang mở: thêm/bớt/huỷ món với `If-Match`, 409 → toast đúng câu trong spec và refetch; nút "Gửi bếp" = tạo đơn), trạng thái rỗng "Thực đơn trống — chạy seed" khi menu rỗng, disabled toàn bộ mutation khi offline.
  _Ghi chú khi build:_ không có route `_auth/shift.tsx` — quản lý ca nằm trọn trong `ShiftBar` trên `/pos`, thu ngân không phải đổi màn hình.
  → kiểm chứng: vitest `orderSchemas.test.ts`; ảnh `a-pos-empty-menu.png`, `a-pos-order.png`, `a-conflict-409.png`, `a-no-open-shift.png`.
- [x] **W5** `/kitchen`: `KitchenBoard` cột `Pending / Preparing / Done`, nhận sự kiện SignalR, nút chuyển trạng thái theo thứ tự, trạng thái "Chưa có đơn nào", chỉ role `Kitchen`/`Owner`.
  → kiểm chứng: ảnh `a-kitchen-board.png`; e2e ở I1.
- [x] **W6** `OfflineBanner` gắn ở `__root.tsx`: đỏ, cố định trên cùng, "Mất kết nối tới hệ thống"; khi online lại tự ẩn và `queryClient.invalidateQueries()`.
  _Ghi chú khi build:_ thêm `components/__tests__/OfflineBanner.test.tsx` và `e2e/helpers.ts` (login/mở ca dùng chung cho 3 spec).
  → kiểm chứng: `e2e/offline-banner.spec.ts` (Playwright `context.setOffline(true)` + chặn `/healthz`) — banner hiện, nút "Gửi bếp" disabled; bật lại → banner ẩn **không reload**.
- [x] **W7** `web/Dockerfile` (build → nginx serve `dist/`, proxy `/api` và `/hubs` sang `gateway:8080`) + service `web` trong compose publish `5173:80`.
  _Ghi chú khi build:_ nginx proxy thêm `= /healthz` (cho `online.ts`) và tắt `access_log` ở `/hubs/` để token trong query không vào log; service `web` bind `127.0.0.1:5173` như gateway. Playwright `baseURL` là `http://localhost:5173` (nginx → gateway 8080), vẫn không gọi thẳng 8082. Service compose được thêm lúc tích hợp: cổng e2e của commit hook cần cả stack chạy nên nhánh web được fast-forward lên nhánh lát A trước khi commit.
  → kiểm chứng: `docker compose up -d --build web` → `curl localhost:5173` 200; login qua trình duyệt hoạt động.

### Nhóm I — Tích hợp (sau khi B và W gộp vào nhánh lát)

- [x] **I1** Gộp nhánh web vào `feat/01-260925-fnb-pos-core-slice-a`; `docker compose down -v && docker compose up -d --build && npm --prefix backend run seed`; chạy `e2e/order-to-kitchen.spec.ts`: hai `BrowserContext` (cashier + kitchen), cashier mở ca (nếu chưa) → thêm 3 món → "Gửi bếp"; kitchen `expect(card).toBeVisible({ timeout: 2000 })` và ghi `performance.now()` chênh lệch vào log; kitchen bấm "Xong" → POS thấy trạng thái trong 2000ms.
  _Ghi chú khi build:_ nhánh web đã gộp từ trước (`9818fd1`). Stack dựng lại 3 lần từ `down -v` (8 container healthy, ~26s sau khi có image); `a-latency.log` có một dòng mỗi lần chạy, mọi lần < 2000ms (534–1253ms tới bếp, 132–169ms về POS; lần 1253ms chạy trong verify, cùng lúc Testcontainers). `git status` không có `bin/obj/dist/node_modules`.
  → kiểm chứng: `npm --prefix web run test:e2e` xanh; log mốc thời gian lưu vào `docs/evidence/01-260925-fnb-pos-core/a-latency.log`.
- [x] **I2** Chụp 8 ảnh ở mục 0 bằng Playwright (`page.screenshot`) vào `docs/evidence/01-260925-fnb-pos-core/`.
  _Ghi chú khi build:_ chụp bằng script Playwright dùng một lần (không commit), `animations: 'disabled'` để không dính giữa transition. `a-pos-empty-menu` chặn `/api/menu` trả `[]` vì DB demo đã seed; `a-conflict-409` là 409 thật — một context chặn `/hubs/**` nên giữ phiên bản đơn cũ trong khi context kia huỷ món trước; `a-no-open-shift` đóng ca thật rồi mở lại.
  → kiểm chứng: `ls docs/evidence/01-260925-fnb-pos-core/*.png | wc -l` ≥ 8.
- [x] **I3** Tài liệu: `STRUCTURE.md`, `schema.md`, `endpoints.md`, `DESIGN_SYSTEM.md`, `README.md` (sơ đồ + bảng đối chiếu JD: điền dòng cho Microservices, API Gateway, Kafka, Redis, JWT, EF Core/PostgreSQL, Docker, SignalR, Clean Architecture, React/TanStack; đánh dấu "lát B/C/D" cho CQRS read model, gRPC, idempotency, saga), `CHANGELOG.md`.
  → kiểm chứng: `grep -c '| ' README.md` > 15; mỗi đường dẫn nhắc trong README tồn tại (`grep -oE 'backend/[A-Za-z0-9_./-]+' README.md | xargs -I{} test -e {}`).
  _Ghi chú khi build:_ Viết tài liệu thì phát hiện cột tiền đang `numeric(14,2)`, lệch §4 (`decimal(18,2)`) mà không ai ghi lại → sửa `HasPrecision(18, 2)` + migration `MoneyPrecision18` (chỉ nới cột) cho ordering và cashier, thêm test `MoneyColumns_AreNumeric18_2` (bỏ migration ordering → đỏ). `endpoints.md`: `/healthz` trả JSON từng dịch vụ, không phải `"ok"`. README giữ link sang `docs/AI-NATIVE-SDLC.md` cho phần bộ khung.
- [x] **I4** `npm run sdlc:verify -- --all --e2e` → lưu output vào `docs/evidence/01-260925-fnb-pos-core/verify-slice-a.log`; kiểm tra bằng chứng "không có build output lọt vào cổng" ở mục 0.
  → kiểm chứng: log kết thúc bằng "✅ Toàn bộ cổng XANH."
  _Ghi chú khi build:_ 81/81 xanh (backend 67, vitest 11, Playwright 3). Log đã soát: không chứa mật khẩu, khoá JWT hay token.

## 3. Thứ tự & song song hoá

| Nhóm | Các bước | Có thể chạy song song? | Worktree |
|------|----------|------------------------|----------|
| B0 | B0.1–B0.5 | không — làm trước, mọi nhóm sau cần config này | chính (`feat/01-260925-fnb-pos-core-slice-a`) |
| B | B1→B2→B3→B4→B5→B6→B7→B8→B9 (tuần tự; B3 và B5 có thể đảo) | **có**, song song với W | chính |
| W | W1→W2→W3→W4→W5→W6→W7 | **có**, song song với B — không đụng file nào của B; xây theo hợp đồng spec §4, test bằng vitest với fetch giả | `../sdlc-fnb-microservices-cv-web`, nhánh `feat/01-260925-fnb-pos-core-slice-a-web` |
| I | I1→I2→I3→I4 | không | chính, sau khi merge W |

```bash
# sau B0.5, từ gốc repo:
git worktree add ../sdlc-fnb-microservices-cv-web -b feat/01-260925-fnb-pos-core-slice-a-web
# W chạy trong ../sdlc-fnb-microservices-cv-web; verify ở đó: npm run sdlc:verify (scope tự ra "web")
# khi W xong: từ worktree chính
git merge --no-ff feat/01-260925-fnb-pos-core-slice-a-web && git worktree remove ../sdlc-fnb-microservices-cv-web
```

W không được chạm `sdlc.config.json`, `docker-compose.yml`, `.github/**` — nếu cần đổi, ghi
vào plan và để nhóm I làm sau khi merge, tránh xung đột.

## 4. Ràng buộc kế thừa từ AGENTS.md và spec

`AGENTS.md` hiện là mẫu chưa điền, nên các luật dưới đây lấy từ **spec §4, §6, §8** — chúng là
ràng buộc chặn merge cho lát này:

- Mọi endpoint **mặc định** `[Authorize]` qua `FallbackPolicy`; public chỉ `login`, `refresh`,
  `healthz` và phải có `[AllowAnonymous]` tường minh + nằm trong `docs/evals/public-endpoints.json`.
- Mọi request body là `record` có DataAnnotations + `[ApiController]`. Không `object`/`dynamic`/`JsonElement` ở controller.
- Lỗi trả `ProblemDetails`, `detail` tiếng Việt hướng người dùng, đúng câu chữ trong spec §4.
- Không `float`/`double` cho tiền; `decimal(18,2)`. Thời gian `timestamptz` UTC.
- Secret chỉ từ env; `.env` không commit; `.env.example` đầy đủ khoá. JWT key ≥ 32 byte.
- CORS đọc `CORS_ORIGINS`; không `AllowAnyOrigin()`.
- Không log `password`, `passwordHash`, `accessToken`, `refreshToken`, query string của `/hubs/*`.
- Web: không `fetch` ngoài `apiClient.ts`; không `useState` cho filter/sort/paging; Zustand chỉ UI state;
  không `tailwind.config.*`; không `@ts-ignore`; không `any`.
- Comment nghiệp vụ tiếng Việt, định danh + commit message tiếng Anh; commit footer `Intent: docs/intents/01-260925-fnb-pos-core/`.
- Test đi cùng code trong cùng commit; mỗi test integration đánh `[Trait("Category","Integration")]`.
- **Không tự ý thêm dependency** ngoài danh sách dưới. Cần thêm → dừng, hỏi.

### Dependency được xin duyệt cho lát A (spec §8 + cụ thể hoá)

| Nơi | Gói | Lý do |
|-----|-----|-------|
| backend | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` | EF Core + migration cho PostgreSQL |
| backend | `Confluent.Kafka` | client Kafka |
| backend | `Microsoft.Extensions.Caching.StackExchangeRedis` | cache menu, đếm đăng nhập sai |
| backend | `Microsoft.AspNetCore.Authentication.JwtBearer` | validate JWT |
| backend | `Yarp.ReverseProxy` | gateway |
| backend (test) | `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `Testcontainers.PostgreSql`, `Testcontainers.Kafka`, `Testcontainers.Redis`, `Microsoft.AspNetCore.Mvc.Testing` | test trên hạ tầng thật |
| web | `react`, `react-dom`, `@tanstack/react-router`, `@tanstack/router-plugin`, `@tanstack/react-query`, `@tanstack/react-form`, `@tanstack/react-table`, `zod`, `zustand`, `@microsoft/signalr`, `tailwindcss`, `@tailwindcss/vite`, `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-react`, `sonner`, `radix-ui` (theo shadcn init) | stack chỉ định |
| web (dev) | `vite`, `@vitejs/plugin-react`, `babel-plugin-react-compiler`, `typescript`, `eslint` + `typescript-eslint` + `eslint-plugin-react-hooks`, `vitest`, `@testing-library/react`, `jsdom`, `@playwright/test`, `shadcn` (CLI, chạy qua npx) | build/lint/test |
| web (dev, duyệt 2026-09-25 lúc ship) | `@types/react`, `@types/react-dom`, `@types/node`, `@testing-library/dom` | Phát sinh khi build W1, **cài trước khi hỏi** — vi phạm, bắt được ở `/sdlc:ship`. Kiểu cho TS strict và peer dependency bắt buộc của `@testing-library/react` 16; không vào bundle. Từ nay eval `P04` chặn tình huống này |

`Grpc.*` và `@tanstack/react-table` **chưa** cần ở lát A — table để W1 cài sẵn vì shadcn `table`
component cần; gRPC để lát B. Không cài `MediatR`, `AutoMapper`, `FluentValidation`, `MassTransit`, `Serilog`.

## 5. Tự chất vấn (bắt buộc — do Agent điền)

> Người điều phối hỏi: *"Thay đổi nào trong kế hoạch này có nguy cơ làm hỏng hệ thống
> hoặc xung đột với tính năng hiện có?"*

Không có tính năng hiện có nào để xung đột — nhưng có **bộ khung SDLC** đang chạy, và mấy chỗ
dưới đây là bằng chứng tìm được bằng cách đọc `scripts/sdlc/lib.mjs`, `verify.mjs`,
`hooks/artifact-guard.mjs`, `.github/workflows/ci.yml`:

| # | Nguy cơ | Vì sao có thể xảy ra | Cách phòng | Test nào bắt được |
|---|---------|----------------------|------------|-------------------|
| 1 | **Cổng chặn commit mù với C#.** `lib.mjs → isCodeFile()` chỉ nhận đuôi trong `codeExtensions`; hiện không có `.cs`. Mọi thay đổi backend sẽ được commit mà không cần verify, và `scopeOf()` không bao giờ ra `backend` | Config mẫu viết cho dự án TS | B0.1 thêm đuôi; B0.4 thêm eval P01 | `node docs/evals/run.mjs` (P01) + quan sát `npm run sdlc:verify` in `Phạm vi: backend` sau khi sửa một `.cs` |
| 2 | **`verify.mjs` bỏ qua `backend/` vì không có `package.json`** (dòng `if (!existsSync(join(dir,'package.json'))) … Bỏ qua`) — cổng "xanh" mà không chạy gì | Khung giả định mọi ws là npm | B0.2 shim `package.json` uỷ quyền sang `dotnet` | Output verify không có dòng `⚠️ Bỏ qua backend`; I4 log |
| 3 | **CI `artifact-chain` sẽ đỏ trên PR**: regex `docs/intents/[0-9]{6}-` không khớp id `01-260925-…` mà chính `/sdlc:intent` sinh ra | Hai phần của khung lệch nhau | B0.3 sửa regex thành `[0-9]{2}-[0-9]{6}-` | Lệnh grep ở kiểm chứng B0.3 |
| 4 | **CI `quality[backend]` chạy `npm ci` với `cache-dependency-path: backend/package-lock.json`** → không có lock thì `npm ci` fail; và không có `setup-dotnet` thì `npm run build` fail | Job matrix mẫu chỉ biết Node | B0.2 sinh lock; B0.3 thêm `actions/setup-dotnet@v4` với `dotnet-version: 10.0.x` cho backend | `cd backend && npm ci` local; CI xanh ở PR |
| 5 | **`bin/`, `obj/`, `dist/` lọt vào `changedCodeFiles()`** (`git status -uall`) → fingerprint đổi sau mỗi build, hook chặn commit đòi verify lại vô tận; `ignoredPrefixes` chỉ match từ gốc repo nên `"dist/"` không che `web/dist/` | Khung dựa vào `.gitignore` của dự án | B0.3 `.gitignore` | Bằng chứng ở mục 0: `git status --porcelain -uall \| grep -E '(bin\|obj\|dist)/'` rỗng sau build |
| 6 | **Race "Chưa mở ca" ngay sau khi mở ca**: `ordering` biết ca hiện hành qua Kafka (`current_shift` projection); thu ngân mở ca rồi bấm "Gửi bếp" trong < 1s có thể nhận 409 oan | Nhất quán cuối theo thiết kế spec §11 | W4: sau `open` thành công, POS poll `GET /api/orders/current-shift` (đọc projection của ordering, không phải cashier) tới khi có, mới bật nút "Gửi bếp"; timeout 5s → thông báo "Đang đồng bộ ca…" | `ShiftProjectionTests` + `order-to-kitchen.spec.ts` (mở ca rồi tạo đơn ngay) |
| 7 | **SignalR qua YARP + JWT**: trình duyệt không gửi header cho WebSocket → token phải đi qua query string → dễ lộ trong access log của gateway | Giới hạn của WebSocket API | B7: `JwtBearerEvents.OnMessageReceived` đọc `access_token` chỉ cho path `/hubs/*`; tắt log query string ở gateway; token access chỉ sống 60′ | Kiểm chứng B7 (grep log không có token) + e2e chạy qua 8080 chứ không qua 8082 |
| 8 | **`xmin` đổi cả khi consumer ghi**: khi `AvailabilityChanged` (lát C) hay `ShiftEventsConsumer` cập nhật cùng dòng, thu ngân đang mở đơn sẽ bị 409 dù không ai "sửa đơn" | Concurrency token ở mức dòng | Chấp nhận ở lát A (không có consumer nào ghi `orders`); ghi chú để lát C không cập nhật `orders`, chỉ `menu_items` | `ConcurrencyTests` giữ nguyên; lát C thêm test riêng |
| 9 | **Kafka KRaft khởi động chậm hơn dịch vụ** → consumer crash-loop lúc `compose up` | Thứ tự khởi động container | B2 healthcheck Kafka (`kafka-topics --list`), B9 `depends_on: condition: service_healthy`; consumer host retry với backoff thay vì throw ở startup | Kiểm chứng B9 (7 healthy ≤ 120s từ `down -v`) |

**Tính năng hiện có có thể bị ảnh hưởng:** không có tính năng nghiệp vụ nào. Bị ảnh hưởng là
**bộ khung**: `sdlc.config.json`, `ci.yml`, `.gitignore`, `README.md` (README của bộ khung bị
thay bằng README dự án — nội dung cũ đã có trong `docs/AI-NATIVE-SDLC.md`, không mất).

**Nếu phải quay đầu:** `git revert` cả dải commit của nhánh là đủ — không có dữ liệu thật;
database chỉ tồn tại trong volume Docker dev (`docker compose down -v` xoá sạch). Không có
migration nào chạy trên DB không phải dev (Seeder và auto-migrate đều chặn khi
`ASPNETCORE_ENVIRONMENT != Development`).

## 6. Điều KHÔNG làm trong lần này

- Thu tiền, `Idempotency-Key`, gRPC `MarkPaid`, đối chiếu tiền — **lát B**.
- `inventory`, công thức, cờ hết hàng do sự kiện — **lát C** (`is_available` ở lát A luôn `true`, seeder không tạo món "Hết").
- `reporting`, màn báo cáo, URL search params — **lát D**.
- Kubernetes/cloud, hoàn tiền, cổng thanh toán, máy in, multi-tenant, app khách (intent §6).
- OpenTelemetry/Grafana — không có trong tiêu chí chấp nhận; để sau khi bốn lát xong.
- Sửa `AGENTS.md`, `scripts/sdlc/*`, `.claude/**` — của con người.
- **Đề xuất cho người điều phối:** sau khi lát A ship, tạo lát B/C/D thành intent riêng
  `02-…`, `03-…`, `04-…` với `related: [01-260925-fnb-pos-core]` để giữ luật "một PR = một
  intent" (AGENTS.md §6); intent 01 khi đó chuyển `shipped` với ghi chú "lát A".

---

## Duyệt của người điều phối

> _Chốt chặn con người thứ hai. Đọc mục 5 trước tiên._
> _Đồng ý → đổi `status` thành `planned` và chạy `/sdlc:build`._

- [x] Tôi đã đọc mục 5 và các nguy cơ là chấp nhận được
- [x] Bằng chứng thành công ở mục 0 là đủ để tôi tin tính năng chạy đúng
- [x] Danh sách dependency ở mục 4 được duyệt
