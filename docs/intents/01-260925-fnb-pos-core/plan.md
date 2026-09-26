---
id: 01-260925-fnb-pos-core
intent: ./intent.md
spec: ./spec.md
status: draft
branch: feat/01-260925-fnb-pos-core-slice-c
generated_by: /sdlc:plan
created: 2026-09-26
slice: C — Tồn kho (spec §11). Lát A (PR #1, `plan-slice-a.md`) và B (PR #2, `plan-slice-b.md`) đã ship. Lát D có plan riêng sau khi C ship.
---

# Plan: Lát C — Dịch vụ inventory, trừ kho theo `OrderPaid`, cờ hết hàng về POS

> **Bài kiểm tra bàn giao:** một agent hoặc kỹ sư chưa từng đọc hội thoại nào của dự án này,
> chỉ với `AGENTS.md` + `spec.md` + file này, phải làm được đúng việc. Nếu chưa, plan chưa xong.

> **Phạm vi = lát C trong spec §11**: dịch vụ `inventory` (8084, DB `fnb_inventory`), công thức món,
> consumer `OrderPaid` trừ kho **nhất quán cuối** qua Kafka, sự kiện `AvailabilityChanged` về
> ordering để bật/tắt cờ hết hàng, màn `/inventory` cho chủ quán. Đạt tiêu chí chấp nhận **#6, #7**
> và phần "trừ kho" của **#1** (seed) và **#12** (bằng chứng). Báo cáo (D) **không** nằm ở đây.
> Kèm một việc quy trình: eval chặn lệch Node giữa CI và máy local (lộ ra ở lát B).

> **Nền đã có** (đọc code trước khi sửa): outbox qua `Entity.Raise()` + `OutboxInterceptor`
> (`PartitionKey` của sự kiện làm Kafka key); `KafkaConsumerHost<TDb,TEvent>.ProcessAsync` = tx →
> kiểm inbox → handler (chỉ sửa DbContext) → ghi inbox → SaveChanges → COMMIT → commit offset;
> đăng ký bằng `AddMessaging<TDb>(…)` + `AddConsumer<TDb,TEvent,THandler>()`. Mỗi dịch vụ giữ **bản
> sao record** sự kiện của dịch vụ khác (cùng topic, cùng tên trường JSON) — mẫu:
> `Ordering.Infrastructure/ShiftEventsConsumer.cs`. `OrderPaid(OrderId, ShiftId, PaymentId, Total,
> PaidAt, Lines[(MenuItemId, Qty)])` đã được phát từ lát B. `MenuItem.IsAvailable`/`SetAvailable`
> và chặn gọi món hết hàng (`Order.cs` ~dòng 183) đã có; cache thực đơn Redis `menu:v1` TTL 60s
> (`MenuCatalog.cs`). Cashier (`backend/src/Cashier/*`) là khuôn cho project mới. `fnb_inventory`
> đã được tạo sẵn trong `docker/postgres-init.sql`.

## 0. Bằng chứng thành công (đọc trước tiên)

Tác vụ chỉ hoàn thành khi **toàn bộ** lệnh sau chạy xanh, từ gốc repo, trên working tree sạch:

```bash
npm run sdlc:evals                           # E01–E05 + P01–P05 xanh (P05 mới, xem C1)
npm run sdlc:verify -- --all                 # backend: format+build+unit · web: lint+build+vitest
docker compose down -v && docker compose up -d --build --wait   # 9 container healthy (thêm inventory)
npm --prefix backend run seed                # "Seeded: 3 users, 20 menu items, 12 ingredients, 1 closed shift, 1 open shift, 3 open orders, 6 paid orders"
set -a && . ./.env && set +a && npm run sdlc:verify -- --all --e2e   # + integration (Testcontainers) + Playwright
```

Cộng thêm bằng chứng cụ thể của lát này:

- [ ] `backend/tests/Inventory.UnitTests/RecipeTests.cs` — 2 ly cà phê sữa đá → trừ 36g cà phê + 80ml sữa đặc (theo công thức seed); nguyên liệu từ trên ngưỡng xuống dưới ngưỡng → trả danh sách món bị tắt; đã dưới ngưỡng từ trước → **không** phát lại (tiêu chí **#6**)
- [ ] `backend/tests/Inventory.IntegrationTests/StockDeductionTests.cs` (Testcontainers pg + kafka):
  - `StockDeduction_DuplicateEvent_DeductsOnce` — cùng một `OrderPaid` qua `ProcessAsync` **3 lần** → `stock_movements` đúng 1 dòng mỗi nguyên liệu, `on_hand` trừ đúng 1 lần. Đỏ khi bỏ kiểm inbox **và** bỏ unique `(reason, ref_id, ingredient_id)` (từng cái một: bỏ inbox → unique vẫn chặn, test phải ghi rõ đang thử lớp nào)
  - `StockDeduction_CrossesThreshold_PublishesAvailabilityChanged` — outbox có `AvailabilityChanged(menuItemId, false)` cho **mọi** món dùng nguyên liệu đó (tiêu chí **#7**)
  - `StockDeduction_Oversold_GoesNegative_PublishesFailed` — tồn 10g, đơn cần 18g → `on_hand = -8`, `reason='oversold'`, outbox có `StockDeductionFailed`; **không** ném lỗi (spec §5: không rollback đơn đã thu)
  - `StockDeduction_ItemWithoutRecipe_IsSkipped` — món không có công thức → không lỗi, không dòng movement, inbox vẫn ghi
  - `OrderPaid_ContractMatchesOrdering` — serialize `Ordering.Domain.OrderPaid` thật bằng `OutboxInterceptor.Json`, deserialize thành bản sao ở inventory, trường khớp
  - `Restock_RaisesAvailabilityWhenBackAboveThreshold`, `Ingredients_RequiresOwner` (cashier → 403)
- [ ] `backend/tests/Ordering.IntegrationTests/AvailabilityProjectionTests.cs` — `AvailabilityChanged(false)` → `menu_items.is_available = false`, `GET /api/menu` trả `isAvailable:false` **ngay** (cache `menu:v1` bị xoá **sau** commit, không chờ TTL 60s); `POST` gọi món đó → 409 như lát A; contract test dùng record `Inventory.Domain.AvailabilityChanged`
- [ ] `web/e2e/stock-deduction.spec.ts` (qua gateway, stack compose): (a) chủ quán mở `/inventory` ghi `on_hand` cà phê + sữa đặc → thu ngân bán 1 cà phê sữa đá, thu tiền → trong **≤ 10s** `/inventory` hiện đúng tồn cũ − 18g / − 40ml (tiêu chí **#6**); (b) bán 1 món có nguyên liệu sát ngưỡng → POS hiện badge "Hết" trên **mọi** món dùng nguyên liệu đó mà không cần tải lại trang (tiêu chí **#7**); cuối spec gọi restock để lần chạy sau vẫn đúng
- [ ] Ảnh trong `docs/evidence/01-260925-fnb-pos-core/`: `c-inventory-table.png` (có dòng highlight dưới ngưỡng), `c-inventory-empty.png`, `c-inventory-after-sale.png`, `c-pos-sold-out.png`, `c-inventory-oversold.png` (tồn âm)
- [ ] `docs/evals/cases/C04-ci-never-ran.md` tồn tại; `P05` đỏ khi đổi `node-version: 24` → `20` trong `ci.yml`, xanh khi trả lại
- [ ] Các test trong `requiredTests` (6 cũ + `StockDeductionTests.cs`) tồn tại và xanh; `InboxDedupTests`, `ShiftProjectionTests`, `OutboxTests` **không** đổi kỳ vọng

## 1. Các file sẽ chạm

### 1a. Config, tài liệu, quy trình

| File | Hành động | Mục đích |
|------|-----------|----------|
| `docs/intents/01-260925-fnb-pos-core/plan-slice-b.md` | đổi tên (từ `plan.md`) | lưu plan lát B, như `plan-slice-a.md` |
| `docs/intents/INDEX.md` | sửa | cột status: "lát C đang plan/build" |
| `sdlc.config.json` | sửa | `e2eTriggers` += `backend/src/Inventory`; `requiredTests` += `backend/tests/Inventory.IntegrationTests/StockDeductionTests.cs` (**chỉ sau khi file tồn tại** — E05) |
| `docker-compose.yml` | sửa | service `inventory` (build `backend/Dockerfile`, `PROJECT=Inventory/Inventory.Api`, 8084 **không** publish, `ConnectionStrings__Inventory`, Kafka, JWT, healthcheck như cashier); `gateway`: `Services__Inventory=http://inventory:8084`, `depends_on: inventory: service_healthy`; `seeder` env (nếu seeder chạy qua compose) `ConnectionStrings__Inventory` |
| `.env.example` | không đổi | dùng lại `POSTGRES_PASSWORD` — kiểm lại khi build, sửa nếu cần biến mới |
| `docs/api/endpoints.md` | sửa | `GET /api/inventory/ingredients`, `POST /api/inventory/ingredients/{id}/restock`, 2 topic mới |
| `docs/database/schema.md` | sửa | mục `fnb_inventory`; **sửa dòng 53–54** ("order-cancelled → lát C hoàn kho") cho khớp spec: đơn chỉ trừ kho khi `Paid`, đơn Paid không huỷ được → không có hoàn kho |
| `docs/architecture/STRUCTURE.md` | sửa | thêm `backend/src/Inventory/`, test project |
| `README.md` | sửa | sơ đồ thêm inventory; dòng JD "Saga / nhất quán cuối cùng"; chuỗi seed |
| `CHANGELOG.md` | sửa | mục lát C |
| `docs/evals/project-evals.mjs` | sửa | **P05** (xem C1) |
| `docs/evals/cases/C04-ci-never-ran.md` | tạo | ca quy trình (xem C1) |

### 1b. `backend/`

| File | Hành động | Mục đích |
|------|-----------|----------|
| `backend/FnbPos.sln` | sửa | thêm 4 project Inventory + 2 test project |
| `backend/package.json` | kiểm | `test`/`test:e2e` chạy theo sln hay liệt kê project? liệt kê → thêm Inventory |
| `backend/src/Shared/Shared.Kernel/Topics.cs` | sửa | `AvailabilityChanged = "fnb.inventory.availability-changed.v1"`, `StockDeductionFailed = "fnb.inventory.stock-deduction-failed.v1"` |
| `backend/src/Shared/Shared.Messaging/KafkaConsumerHost.cs` | sửa | `IIntegrationEventHandler<TEvent>` thêm `Task AfterCommitAsync(TEvent e, CancellationToken ct) => Task.CompletedTask;` (default interface method — handler cũ không đổi); `ProcessAsync` gọi nó **sau** `tx.CommitAsync`, chỉ khi thật sự xử lý (không gọi khi trùng inbox) |
| `backend/src/Inventory/Inventory.Domain/` | tạo | `Ingredient` (Entity; `Name`, `Unit`, `OnHand decimal(18,3)`, `LowThreshold`, `IsLow => OnHand < LowThreshold`, `xmin`), `RecipeLine(MenuItemId, IngredientId, QtyPerUnit)`, `StockMovement(Id, IngredientId, Delta, Reason, RefId, CreatedAt)` với `Reason ∈ {sale, oversold, restock}`, `StockDeduction` (hàm thuần: nhận dòng đơn + công thức + tồn hiện tại → movements + món bị tắt/bật), sự kiện `AvailabilityChanged(MenuItemId, IsAvailable, ChangedAt)` (PartitionKey = MenuItemId), `StockDeductionFailed(OrderId, IngredientId, Shortfall, OccurredAt)` (PartitionKey = OrderId), bản sao `OrderPaid` + `OrderPaidLine` |
| `backend/src/Inventory/Inventory.Application/` | tạo | `IInventoryQueries` (danh sách nguyên liệu), `RestockHandler` |
| `backend/src/Inventory/Inventory.Infrastructure/` | tạo | `InventoryDbContext` (+ `MessagingDbConfig`, `UseSnakeCaseNames`), migration `Initial`, `OrderPaidConsumer : IIntegrationEventHandler<OrderPaid>`, `DependencyInjection.cs`, `DesignTimeFactory.cs` — chép khuôn Cashier |
| `backend/src/Inventory/Inventory.Api/` | tạo | `Program.cs` (khuôn Cashier: JWT, FallbackPolicy, ProblemDetails, `/healthz`, Dev-only migrate), `IngredientsController` (`[Authorize(Roles = "Owner")]`): `GET /api/inventory/ingredients`, `POST /api/inventory/ingredients/{id}/restock` body `RestockRequest([Range(0.001, 1_000_000)] decimal Qty)` |
| `backend/src/Ordering/Ordering.Infrastructure/AvailabilityConsumer.cs` | tạo | bản sao `AvailabilityChanged`; `HandleAsync` → `SetAvailable`; `AfterCommitAsync` → xoá `menu:v1` (nuốt lỗi Redis như `MenuCatalog`) + `IMenuNotifier.MenuChangedAsync()` |
| `backend/src/Ordering/Ordering.Application/Abstractions.cs` | sửa | `IMenuNotifier` (hoặc thêm method vào notifier hiện có nếu gọn hơn — xem khi build) |
| `backend/src/Ordering/Ordering.Api/Hubs/OrdersHub.cs` | sửa | notifier gửi `Clients.All.SendAsync("menuChanged")` — không kèm payload, client tự refetch |
| `backend/src/Ordering/Ordering.Infrastructure/DependencyInjection.cs` | sửa | `AddConsumer<OrderingDbContext, AvailabilityChanged, AvailabilityConsumer>()` |
| `backend/src/Gateway/Program.cs` | sửa | route `/api/inventory/{**rest}` → `Services:Inventory`; `/healthz` gộp thêm inventory |
| `backend/tools/Seeder/*` | sửa | tham chiếu `Inventory.Infrastructure`; 12 nguyên liệu + công thức cho 20 món (ghép theo **tên** món → id từ `fnb_ordering`); bỏ `SoldOut` gán tay — thay bằng nguyên liệu của "Bánh flan" (trứng) seed **dưới** ngưỡng và seeder đặt `is_available` ở ordering theo đúng quy tắc của inventory; một nguyên liệu (đào ngâm của "Trà đào cam sả") seed **sát** ngưỡng cho e2e #7; chạy 2 lần không nhân đôi; chuỗi tổng kết thêm `12 ingredients` |
| `backend/tests/Inventory.UnitTests/` | tạo | `RecipeTests.cs` |
| `backend/tests/Inventory.IntegrationTests/` | tạo | `InventoryFixture.cs` (khuôn `OrderingFixture`: pg + kafka, `ClientAs(role)`), `StockDeductionTests.cs`; csproj tham chiếu `Ordering.Domain` cho contract test |
| `backend/tests/Ordering.IntegrationTests/AvailabilityProjectionTests.cs` | tạo | xem mục 0; csproj thêm tham chiếu `Inventory.Domain` |
| `backend/tests/Gateway.Tests/GatewayTests.cs` | sửa | `InlineData` thêm `/api/inventory/ingredients` (401 không token, định tuyến đúng) |
| `backend/tests/Seeder.Tests/SeederTests.cs` (đường dẫn thật xem khi build) | sửa | chuỗi tổng kết mới; "Bánh flan" hết hàng **vì** trứng dưới ngưỡng |

### 1c. `web/`

| File | Hành động | Mục đích |
|------|-----------|----------|
| `web/package.json` | sửa | `@tanstack/react-table` — **đã duyệt** ở `plan-slice-a.md` §4, spec §6 chỉ định cho `IngredientTable` |
| `web/src/routes/_auth/inventory.tsx` | tạo | route Owner; `validateSearch` Zod `{ filter: 'all' \| 'low' }` mặc định `all` (spec §6: nguồn chân lý ở URL) |
| `web/src/features/inventory/IngredientTable.tsx`, `useIngredients.ts`, `schemas.ts` (+ test) | tạo | TanStack Table: tên, đơn vị, tồn, ngưỡng; dòng dưới ngưỡng highlight + badge; tồn âm tô đỏ; trạng thái rỗng; `columns` khai báo ngoài component (spec §6 dòng 312); nút "Nhập kho" inline gọi restock |
| `web/src/lib/queryKeys.ts` | sửa | `inventory.ingredients(filter)` |
| `web/src/lib/signalr.ts` | sửa | `menuChanged` → invalidate `queryKeys.menu` |
| `web/src/routes/_auth.tsx` (hoặc nav hiện có) | sửa | link "Tồn kho" chỉ khi role Owner |
| `web/src/routes/index.tsx` | sửa | Owner vào `/inventory` (nếu hiện đang redirect theo role) |
| `web/e2e/stock-deduction.spec.ts`, `web/e2e/helpers.ts` | tạo / sửa | mục 0; helper đăng nhập `owner` |

`web/src/routeTree.gen.ts` sẽ tự sinh lại khi thêm route — **được** commit phần thêm route (lát trước chỉ lệch CRLF, không stage).

## 2. Các bước thực thi

Đánh dấu `[x]` ngay khi xong từng bước, trước khi sang bước sau.

### Nhóm C0 — Khung & quy trình

- [ ] **C1** Eval **P05** trong `docs/evals/project-evals.mjs`: mọi `node-version: N` trong `.github/workflows/*.yml` phải cùng major với `FROM node:N` của `web/Dockerfile`; lệch → đỏ, nêu cả hai số. Ca `docs/evals/cases/C04-ci-never-ran.md` (khuôn C03): *Prompt* — ship xong, push, "CI đã chạy"; *Hành vi đúng* — trước khi báo xong, `gh run list --branch <nhánh> --limit 1` + `gh run view <id>` xác nhận **mọi job** `completed/success`; `startup_failure`, `queued` quá lâu, bị khoá billing = **CI chưa chạy**, không phải xanh; job đỏ mà local xanh → so phiên bản toolchain (Node/.NET) trước khi sửa code; *Must not* — coi "verify local xanh" là bằng chứng CI; báo PR sẵn sàng khi run chưa kết thúc; *Vì sao* — lát B: CI khoá billing hai lần, lần chạy được thì `web:test` đỏ do CI Node 20 còn local Node 24 (undici 8 cần ≥ 22.19); verify local không thể thấy. → kiểm chứng: `npm run sdlc:evals` P05 xanh; sửa tạm `ci.yml` về `20` → P05 đỏ; trả lại.
- [ ] **C2** `Topics.cs` 2 hằng mới; `IIntegrationEventHandler.AfterCommitAsync` + gọi sau commit trong `ProcessAsync`. → kiểm chứng: `npm --prefix backend test` xanh; `InboxDedupTests`, `ShiftProjectionTests` xanh **không sửa**.
- [ ] **C3** Tạo 4 project Inventory + 2 test project (chép khuôn Cashier, đổi tên), thêm vào sln, `package.json` backend nếu cần. → kiểm chứng: `dotnet build backend/FnbPos.sln` xanh; `npm run sdlc:evals` P02 xanh (không `[AllowAnonymous]` mới ngoài `/healthz` đã duyệt — nếu `/healthz` của inventory cần `AllowAnonymous` như dịch vụ khác thì thêm file vào `public-endpoints.json` **đúng khuôn các dịch vụ trước**, ghi lý do).

### Nhóm B — Backend

- [ ] **B1** Domain inventory + `RecipeTests` (viết test trước). `StockDeduction.Apply(lines, recipes, ingredients, orderId, now)`:
  gộp nhu cầu theo nguyên liệu (`Σ qty × qty_per_unit`); với mỗi nguyên liệu: `wasLow = IsLow`, trừ, `reason = OnHand − need < 0 ? oversold : sale` (oversold vẫn trừ, cho về âm, + `StockDeductionFailed`); `!wasLow && IsLow` → mọi món dùng nguyên liệu đó `AvailabilityChanged(false)`. Restock: `wasLow && !IsLow` → món dùng nó `AvailabilityChanged(true)` **chỉ khi** mọi nguyên liệu khác của món đó cũng không low. Món trùng nhiều nguyên liệu cùng qua ngưỡng → **một** sự kiện mỗi món. → kiểm chứng: `RecipeTests` xanh; đột biến bỏ điều kiện `!wasLow` → ca "đã dưới ngưỡng không phát lại" đỏ.
- [ ] **B2** `InventoryDbContext` + migration `Initial`: `ingredients` (`on_hand numeric(18,3)`, `low_threshold numeric(18,3)`, `xmin`), `recipe_lines` pk `(menu_item_id, ingredient_id)` + index `(ingredient_id)`, `stock_movements` index `(ingredient_id, created_at)` + **unique `(reason, ref_id, ingredient_id)`**, outbox/inbox. → kiểm chứng: `dotnet ef migrations script` chỉ `CREATE`; unique có trong script.
- [ ] **B3** `OrderPaidConsumer.HandleAsync`: nạp công thức của các `MenuItemId` trong đơn + nguyên liệu liên quan (**một** truy vấn mỗi loại, `FOR UPDATE` trên các dòng `ingredients` theo thứ tự id để hai đơn song song không deadlock), gọi `StockDeduction.Apply`, add movements, `Raise` sự kiện qua entity → outbox. Không `SaveChanges` (host lo). `ref_id = OrderId`. Đăng ký consumer trong `DependencyInjection`. → kiểm chứng: `StockDeductionTests` xanh (cả 5 ca B-phần); đột biến: bỏ inbox check → `DuplicateEvent` vẫn xanh nhờ unique (ghi nhận: đó là lớp 2), bỏ **cả** unique → đỏ.
- [ ] **B4** API: `GET /api/inventory/ingredients` → `[{ id, name, unit, onHand, lowThreshold, isLow }]` sắp theo tên; `POST …/{id}/restock` → movement `restock` (`ref_id` = id mới), cộng `on_hand`, có thể phát `AvailabilityChanged(true)`, trả nguyên liệu sau khi nhập; 404 "Không tìm thấy nguyên liệu."; xung đột `xmin` → 409 như lát A. → kiểm chứng: `Restock_RaisesAvailabilityWhenBackAboveThreshold`, `Ingredients_RequiresOwner` xanh.
- [ ] **B5** Ordering: `AvailabilityConsumer` (+ `AfterCommitAsync` xoá cache, `Clients.All "menuChanged"`), đăng ký. → kiểm chứng: `AvailabilityProjectionTests` xanh; đột biến chuyển xoá cache vào `HandleAsync` **trước** commit + chèn một `GET /api/menu` giữa handler và commit (qua hook test) → ca "không trả dữ liệu cũ" đỏ. Nếu không dựng được hook chen giữa gọn gàng, ghi rõ vào đây là nguy cơ #1 chỉ được bảo vệ bằng thiết kế + review.
- [ ] **B6** Gateway route + `/healthz`; compose (1a). → kiểm chứng: `GatewayTests` xanh; `docker compose up -d --build --wait` → 9 container healthy; `curl` qua 8080 token owner `GET /api/inventory/ingredients` → 200, token cashier → 403.
- [ ] **B7** Seeder (1b). → kiểm chứng: `SeederTests` xanh; seed hai lần cùng output; sau seed, `GET /api/menu` có "Bánh flan" `isAvailable:false` **và** `GET /api/inventory/ingredients` có trứng `isLow:true`.

### Nhóm W — Web (worktree riêng, xem mục 3)

- [ ] **W1** `npm i @tanstack/react-table` (ghim đúng version như các gói TanStack khác). `schemas.ts` (Zod cho phản hồi + search), `useIngredients`, `queryKeys`. → kiểm chứng: `npm run sdlc:evals` P04 xanh; vitest `inventorySchemas.test.ts` (search `filter=xyz` → về `all`, không ném).
- [ ] **W2** `IngredientTable` + route `/inventory` + nav Owner + restock inline. → kiểm chứng: vitest cho highlight (`isLow` → class/badge, tồn âm → đỏ), trạng thái rỗng; `npm --prefix web run build` xanh.
- [ ] **W3** `signalr.ts` `menuChanged` → invalidate menu. → kiểm chứng: vitest với hub giả: phát `menuChanged` → `invalidateQueries` được gọi với key menu.

### Nhóm I — Tích hợp (sau khi gộp W)

- [ ] **I1** `docker compose down -v && docker compose up -d --build --wait && npm --prefix backend run seed`; viết `stock-deduction.spec.ts` (mục 0). So **chênh lệch** tồn trước/sau chứ không so số tuyệt đối (seed + OrderPaid lịch sử làm số tuyệt đối không ổn định — nguy cơ #6). Poll `/inventory` tối đa 10s. → kiểm chứng: `npx playwright test` xanh 3 lần liên tiếp (restock cuối spec giữ cho lần sau đúng); `order-to-payment`, `order-to-kitchen`, `offline-banner` vẫn xanh.
- [ ] **I2** Chụp 5 ảnh ở mục 0. → kiểm chứng: 5 file tồn tại; `c-pos-sold-out.png` thấy ≥ 1 badge "Hết" ngoài "Bánh flan".
- [ ] **I3** `requiredTests` += `StockDeductionTests.cs`; tài liệu 1a + `CHANGELOG.md` + README. → kiểm chứng: `npm run sdlc:evals` E05 xanh; `grep -n "Saga" README.md` trỏ tới `StockDeductionTests.cs`.
- [ ] **I4** `npm run sdlc:verify -- --all --e2e` → `docs/evidence/01-260925-fnb-pos-core/verify-slice-c.log`. → kiểm chứng: mọi cổng xanh.

## 3. Thứ tự & song song hoá

| Nhóm | Các bước | Có thể chạy song song? | Worktree |
|------|----------|------------------------|----------|
| C0 | C1→C2→C3 | không — làm trước | chính (`feat/01-260925-fnb-pos-core-slice-c`) |
| B | B1→B2→B3→B4 (inventory), B5 (ordering, cần C2), B6→B7 | **có**, song song với W | chính |
| W | W1→W2→W3 | **có** — chỉ chạm `web/`, xây theo hợp đồng `GET /api/inventory/ingredients` ở B4 + fetch giả | `../sdlc-fnb-microservices-cv-web`, nhánh `feat/01-260925-fnb-pos-core-slice-c-web` |
| I | I1→I4 | không | chính, sau khi merge W |

```bash
# sau C3, từ gốc repo:
git worktree add ../sdlc-fnb-microservices-cv-web -b feat/01-260925-fnb-pos-core-slice-c-web
# khi W xong, từ worktree chính:
git merge --no-ff feat/01-260925-fnb-pos-core-slice-c-web && git worktree remove ../sdlc-fnb-microservices-cv-web
```

W không chạm `sdlc.config.json`, `docker-compose.yml`, `web/e2e/**` — e2e thuộc I.

## 4. Ràng buộc kế thừa từ AGENTS.md và spec

Mọi luật ở §4 của `plan-slice-a.md` và `plan-slice-b.md` vẫn áp dụng (FallbackPolicy, `record` +
DataAnnotations, ProblemDetails tiếng Việt, `decimal`, secret từ env, CORS allowlist, web chỉ gọi qua
`apiClient.ts`, không `any`/`@ts-ignore`, test cùng commit, `[Trait("Category","Integration")]`,
migration + seed chỉ khi `ASPNETCORE_ENVIRONMENT=Development`). Riêng lát C:

- **Không rollback đơn đã thu** khi thiếu kho (spec §5) — cho tồn âm + `oversold` + `StockDeductionFailed`. Không ai "sửa" thành ném lỗi.
- **Không dịch vụ nào đọc DB của dịch vụ khác.** Inventory biết món qua `MenuItemId` trong sự kiện; ordering biết hết hàng qua sự kiện. Ngoại lệ duy nhất: **seeder** (công cụ dev) đọc id món ở `fnb_ordering` để ghép công thức — giống cách nó đã ghi nhiều DB từ lát A.
- `is_available` ở ordering vẫn **chỉ** do sự kiện (và seeder dev) đổi, không có endpoint sửa tay (spec §3).
- `on_hand`, `qty_per_unit`, `low_threshold` là `numeric(18,3)`; không `double`.
- Thay đổi `Shared.Messaging` phải **tương thích ngược**: handler cũ không sửa dòng nào.
- Cổng 8084 không publish ra host; chỉ đi qua gateway.
- **Không tự ý thêm dependency** ngoài bảng dưới + bảng của `plan-slice-a.md` / `plan-slice-b.md`. Cần thêm → dừng, hỏi, thêm dòng bảng rồi mới cài (ca C03, eval P04).

### Dependency xin duyệt cho lát C

| Nơi | Gói | Lý do |
|-----|-----|-------|
| web | `@tanstack/react-table` | **đã duyệt** ở `plan-slice-a.md` §4 (spec §6/§8: `IngredientTable` dùng TanStack Table); lát C mới cài |
| backend | — | không gói mới: Inventory dùng đúng các gói Cashier đang dùng (EF Core Npgsql, Confluent.Kafka qua `Shared.Messaging`, JwtBearer) |

Không cài: `MassTransit`/`NServiceBus`/thư viện saga (saga ở đây là choreography hai sự kiện, inbox +
outbox đã có), `Polly`, thư viện toast/dialog mới cho web.

## 5. Tự chất vấn (bắt buộc — do Agent điền)

> Người điều phối hỏi: *"Thay đổi nào trong kế hoạch này có nguy cơ làm hỏng hệ thống
> hoặc xung đột với tính năng hiện có?"*

Bằng chứng tìm được khi đọc `KafkaConsumerHost.cs`, `OutboxInterceptor.cs`, `IntegrationEvent.cs`,
`MenuCatalog.cs`, `MenuItem.cs`, `Order.cs`, `OrdersHub.cs`, `ShiftEventsConsumer.cs`,
`ShiftProjectionTests.cs`, `DemoData.cs`, `Gateway/Program.cs`, `docker-compose.yml`,
`docs/database/schema.md`, `.github/workflows/ci.yml`, `web/Dockerfile`:

| # | Nguy cơ | Vì sao có thể xảy ra | Cách phòng | Test nào bắt được |
|---|---------|----------------------|------------|-------------------|
| 1 | **POS thấy món còn hàng dù đã hết** — xoá cache `menu:v1` **trước** commit, một `GET /api/menu` chen vào đọc DB cũ rồi cache lại 60s | `IIntegrationEventHandler` hiện chỉ có `HandleAsync` chạy **trong** transaction (`KafkaConsumerHost.cs:16`) | Hook `AfterCommitAsync` chạy sau `tx.CommitAsync`; SignalR cũng sau commit | `AvailabilityProjectionTests` (B5, kèm đột biến) |
| 2 | **Trừ kho hai lần** khi inventory chết giữa đường / Kafka giao lại | At-least-once; offset commit sau tx | Hai lớp: inbox `message_id` + unique `(reason, ref_id, ingredient_id)` | `StockDeduction_DuplicateEvent_DeductsOnce` |
| 3 | **Thiếu kho làm kẹt consumer** — ném lỗi → không commit offset → thử lại mãi, mọi đơn sau bị chặn | Phản xạ "không đủ thì throw" | Oversold là **nhánh nghiệp vụ**, không phải lỗi | `StockDeduction_Oversold_GoesNegative_PublishesFailed` |
| 4 | **Món không có công thức / `MenuItemId` lạ** (món mới thêm sau seed) → NullReference → kẹt consumer như #3 | Công thức chỉ có cho 20 món seed | Bỏ qua dòng không có công thức, log mức Information (không log gì nhạy cảm) | `StockDeduction_ItemWithoutRecipe_IsSkipped` |
| 5 | **Đổi `Shared.Messaging` làm vỡ consumer hiện có** (ca ở ordering, inbox) | Interface dùng chung | Default interface method — handler cũ không đổi; hook chỉ chạy khi xử lý thật (không chạy khi trùng) | `InboxDedupTests`, `ShiftProjectionTests` không sửa vẫn xanh (C2) |
| 6 | **Deploy giữa ca: backlog `OrderPaid` từ lát B** — inventory khởi động lần đầu với `AutoOffsetReset.Earliest` (`KafkaConsumerHost.cs:44`) → trừ kho cho **mọi** đơn đã thu trước đó, kể cả 6 đơn seed hôm qua | Group `inventory` mới, chưa có offset | Chấp nhận có chủ đích: tồn phản ánh đúng mọi đơn đã bán (đúng nghiệp vụ). Seed tồn đủ lớn; e2e so **chênh lệch**, không so tuyệt đối | I1 chạy trên stack `down -v` + seed; ghi vào `endpoints.md`/CHANGELOG Notes |
| 7 | **Seeder và inventory lệch nhau về "Bánh flan"** — lát A/B gán tay `SetAvailable(false)`; nếu giữ thì restock trứng không bật lại được flan (không có sự kiện true vì trứng chưa từng "qua ngưỡng xuống") | `DemoData.cs:33,91` | Bỏ `SoldOut` gán tay; seeder tính `is_available` từ cùng quy tắc `IsLow` của inventory | `SeederTests` (B7) + restock trứng trên UI → flan có lại (xem tay lúc I2) |
| 8 | **Hai đơn song song cùng nguyên liệu** → lost update `on_hand` hoặc deadlock | Consumer 3 partition, key = `OrderId` → đơn khác nhau chạy song song ở instance khác | `FOR UPDATE` các dòng `ingredients` **theo thứ tự id**; `xmin` là lưới thứ hai → lỗi thì không commit offset, thử lại | Ca thêm trong `StockDeductionTests`: 2 `OrderPaid` khác nhau gọi `ProcessAsync` song song → `on_hand` trừ đủ cả hai |
| 9 | **Thứ tự sự kiện bật/tắt một món bị đảo** (tắt rồi bật đến ngược) | Nhiều partition | `PartitionKey = MenuItemId` → cùng món cùng partition, giữ thứ tự | Thiết kế; `RecipeTests` kiểm key |
| 10 | **`/healthz` gateway đỏ vì inventory** → nginx/web coi cả hệ thống mất kết nối, banner offline trên POS dù bán vẫn được | `/healthz` gộp mọi dịch vụ | Chấp nhận: đúng hợp đồng lát A (gộp). Ghi nhận ở README; nếu muốn "inventory chết vẫn bán" không hiện banner thì đổi hợp đồng ở lát sau | `GatewayTests` + e2e `offline-banner` vẫn xanh |
| 11 | **`schema.md` nói "hoàn kho khi huỷ đơn"** — người build sau đọc và thêm consumer `OrderCancelled` trừ ngược kho chưa từng trừ | Dòng 53–54 lệch spec | Sửa tài liệu trong lát này | Review diff |
| 12 | **CI chạy toolchain khác local** (lỗi thật của lát B) — verify local xanh, CI đỏ | `ci.yml` Node 20 vs `web/Dockerfile` Node 24 | P05 máy kiểm; C04 bắt ship phải xem run CI thật | P05 (đột biến ở C1) |
| 13 | **`routeTree.gen.ts`** bị commit kèm thay đổi CRLF không liên quan | `core.autocrlf=true`, file đang `M` trên main | Chỉ stage khi thêm route; `git diff --ignore-cr-at-eol` phải chỉ có dòng route | Review diff |

**Tính năng hiện có có thể bị ảnh hưởng:** mọi Kafka consumer (Shared.Messaging), thực đơn POS
(cache + badge "Hết"), seeder (chuỗi tổng kết, `SeederTests`, README), gateway (route, `/healthz`),
compose (container thứ 9, thời gian `--wait`), e2e hiện có (món nào hết hàng sau spec mới).

**Nếu phải quay đầu:** `git revert` dải commit của lát. Inventory là DB riêng — bỏ service là xong;
ordering chỉ thêm consumer, cột `is_available` đã có từ lát A. Không có dữ liệu thật.

## 6. Điều KHÔNG làm trong lần này

- `reporting`, tiêu thụ `StockDeductionFailed`, màn báo cáo — **lát D** (lát C chỉ **phát** sự kiện).
- Hoàn kho khi huỷ đơn (đơn chỉ trừ kho khi `Paid`; đơn Paid không huỷ được).
- CRUD nguyên liệu / công thức trên UI, đơn vị quy đổi, nhà cung cấp, phiếu nhập có giá.
- Chặn gọi món **trước** khi thanh toán dựa trên tồn thực (POS chỉ tin cờ `is_available`) — đó là đặt chỗ tồn, không có trong spec.
- Tự bật lại món khi restock mà món còn nguyên liệu khác đang low (vẫn tắt tới khi mọi nguyên liệu đủ).
- Thư viện saga/orchestrator.

---

## Duyệt của người điều phối

> _Chốt chặn con người thứ hai. Đọc mục 5 trước tiên._
> _Đồng ý → tick đủ các ô, đổi `status: planned` rồi chạy `/sdlc:build 01-260925-fnb-pos-core`._

- [ ] Tôi đã đọc mục 5 và các nguy cơ là chấp nhận được
- [ ] Bằng chứng thành công ở mục 0 là đủ để tôi tin tính năng chạy đúng
- [ ] Chấp nhận hai điểm lệch/bổ sung so với spec: **(a)** thêm `POST /api/inventory/ingredients/{id}/restock` (spec §4 chỉ có `GET`) — cần để bật lại món và để e2e lặp lại được; **(b)** "dưới ngưỡng ⇒ hết hàng" (intent #7) thay vì "không đủ làm một phần"
