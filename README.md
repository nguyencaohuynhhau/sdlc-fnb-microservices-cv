# FnB POS — microservices .NET 10 + React

Hệ thống bán hàng cho quán cà phê: thu ngân gọi món trên POS, bếp thấy đơn tức thì, món chuyển
`Chờ → Đang làm → Xong` và POS thấy lại trong dưới 2 giây. Dự án portfolio, dựng để đối chiếu từng
yêu cầu trong các JD .NET microservices (`docs/.net-micro.txt`) với mã chạy được và test chứng minh.

Làm theo quy trình **AI-Native SDLC** (`intent → spec → plan → build → verify`), xem
[docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md). Toàn bộ quyết định của lát hiện tại nằm ở
[docs/intents/01-260925-fnb-pos-core/](docs/intents/01-260925-fnb-pos-core/).

## Chạy thử

Cần Docker và .NET 10 SDK (cho seeder). Node 24 chỉ cần khi chạy test web.

```bash
cp .env.example .env        # điền POSTGRES_PASSWORD, JWT_SIGNING_KEY (≥ 32 byte), SEED_PASSWORD (≥ 8 ký tự)
docker compose up -d --build --wait
npm --prefix backend run seed
# → Seeded: 3 users, 20 menu items, 1 closed shift, 1 open shift, 3 open orders, 6 paid orders
```

Mở http://localhost:5173, đăng nhập `cashier` (POS) hoặc `kitchen` (bảng bếp), mật khẩu là `SEED_PASSWORD`.
Mở hai trình duyệt cạnh nhau để thấy đơn chạy qua SignalR.

```bash
npm run sdlc:verify -- --all                                  # format + build + unit test + lint
set -a && . ./.env && set +a && npm run sdlc:verify -- --all --e2e   # + Testcontainers + Playwright (cần stack đang chạy)
```

## Kiến trúc

```
 Trình duyệt (POS, bếp)
        │  HTTP + WebSocket
        ▼
 web — nginx :5173 ──► gateway — YARP :8080      JWT · CORS allowlist · /healthz gộp
                           │
         ┌─────────────────┼──────────────────┐
         ▼                 ▼                  ▼
     identity          ordering            cashier
  login, refresh    thực đơn, đơn,       mở / đóng ca
                    hub SignalR
         │                 │   ▲              │
         │                 │   └──── Kafka ───┘   shift-opened / shift-closed (outbox → inbox)
         ▼                 ▼                  ▼
   fnb_identity      fnb_ordering        fnb_cashier      PostgreSQL 17, mỗi dịch vụ một DB
         │                 │
         └───── Redis ─────┘   khoá đăng nhập sai · cache thực đơn
```

- **Mỗi dịch vụ một database.** Không dịch vụ nào đọc database của dịch vụ khác; ordering biết ca đang mở
  **qua sự kiện** của cashier, giữ bản chiếu trong bảng `known_shifts`.
- **Chỉ gateway và web mở cổng** (bind `127.0.0.1`). Ba dịch vụ vẫn tự kiểm JWT — gateway không phải lớp bảo vệ duy nhất.
- Chi tiết: [cấu trúc thư mục](docs/architecture/STRUCTURE.md) · [API](docs/api/endpoints.md) ·
  [schema](docs/database/schema.md) · [design system](docs/design/DESIGN_SYSTEM.md).

## Đối chiếu JD

| Yêu cầu JD | Ở đâu trong repo | Bằng chứng |
|------------|------------------|------------|
| Microservices, service decomposition | 3 dịch vụ + gateway, database riêng (`backend/src/`) | `docker compose ps`: 8 container healthy — `docs/evidence/01-260925-fnb-pos-core/a-compose-ps.png` |
| API Gateway | YARP, route + JWT + CORS allowlist (`backend/src/Gateway/Program.cs`) | `backend/tests/Gateway.Tests/GatewayTests.cs` (21 test, gồm WebSocket qua gateway) |
| Kafka, event-driven | Outbox ghi cùng transaction, publisher nền, inbox khử trùng (`backend/src/Shared/Shared.Messaging/`) | `backend/tests/Ordering.IntegrationTests/OutboxTests.cs`: crash trước khi publish vẫn gửi khi khởi động lại |
| Redis | Cache thực đơn `menu:v1`, đếm đăng nhập sai → khoá 15 phút | `backend/tests/Identity.IntegrationTests/AuthTests.cs` (`Login_TenFailures_Returns423`) |
| JWT / OAuth2 | HS256, access 60 phút, refresh token xoay vòng, dùng lại → thu hồi cả chuỗi | `AuthTests.cs` (`Refresh_ReusedToken_RevokesFamily`) |
| EF Core, PostgreSQL | Migration, `xmin` làm concurrency token, unique partial index `NULLS NOT DISTINCT`, tiền `numeric(18,2)` | `backend/tests/Ordering.IntegrationTests/ConcurrencyTests.cs`, `backend/tests/Cashier.IntegrationTests/ShiftTests.cs` |
| SQL: index, transaction | Index theo truy vấn thật, ràng buộc "một ca mở" ở DB chứ không ở code | `ShiftTests.cs` (`OpenShift_Concurrent_OnlyOneSucceeds`: 10 request song song → đúng 1 thành công) |
| Clean Architecture, SOLID, DI | `Api → Application → Domain`, `Infrastructure` cắm vào qua interface (`backend/src/Ordering/`) | `backend/tests/Ordering.UnitTests/` chạy domain không cần I/O |
| REST API, middleware | ProblemDetails tiếng Việt, `[Authorize]` mặc định, ETag/If-Match (`backend/src/Shared/Shared.Web/`) | 409 thật khi hai người sửa cùng đơn — `a-conflict-409.png` |
| Realtime (SignalR) | Hub `/hubs/orders`, group theo ca, token qua query chỉ ở `/hubs` | `web/e2e/order-to-kitchen.spec.ts`: POS → bếp < 2000ms (`a-latency.log`) |
| Docker, CI/CD | Một `backend/Dockerfile` cho mọi dịch vụ, healthcheck, `.github/workflows/ci.yml` | `docker compose up --wait` từ số 0 |
| ReactJS, TypeScript | React 19, TanStack Router/Query/Form, Zod, Zustand, shadcn/ui (`web/src/`) | `web/e2e/offline-banner.spec.ts`: mất mạng → khoá nút ghi, có mạng → tự hồi phục |
| Kiểm thử trên hạ tầng thật | Testcontainers Postgres/Kafka/Redis, Playwright trên stack compose | `npm run sdlc:verify -- --all --e2e` |
| Idempotency (giao dịch tiền) | _lát B_ — `POST /api/payments` với `Idempotency-Key` | — |
| gRPC | _lát B_ — `Ordering.MarkPaid` nội bộ | — |
| Saga / nhất quán cuối cùng giữa dịch vụ | _lát C_ — trừ kho theo `OrderPaid`, cờ hết hàng về POS | — |
| CQRS read model | _lát D_ — `reporting` dựng từ sự kiện, báo cáo phân trang | — |
| OpenAPI / Swagger | chưa làm | — |
