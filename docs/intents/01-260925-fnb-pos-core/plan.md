---
id: 01-260925-fnb-pos-core
intent: ./intent.md
spec: ./spec.md
status: planned
branch: feat/01-260925-fnb-pos-core-slice-b
generated_by: /sdlc:plan
created: 2026-09-25
slice: B — Tiền (spec §11). Lát A đã ship (PR #1, plan lưu ở plan-slice-a.md). Lát C/D có plan riêng sau khi B ship.
---

# Plan: Lát B — Thu tiền, idempotency, gRPC MarkPaid, đối chiếu tiền cuối ca

> **Bài kiểm tra bàn giao:** một agent hoặc kỹ sư chưa từng đọc hội thoại nào của dự án này,
> chỉ với `AGENTS.md` + `spec.md` + file này, phải làm được đúng việc. Nếu chưa, plan chưa xong.

> **Phạm vi = lát B trong spec §11**: `POST /api/payments` (bắt buộc `Idempotency-Key`), gRPC
> `OrderPayments.MarkPaid` cashier → ordering, sự kiện `OrderPaid`, đóng ca có đối chiếu tiền, POS
> thu tiền + form đóng ca. Đạt tiêu chí chấp nhận **#4, #5, #8**. Tồn kho (C) và báo cáo (D)
> **không** nằm ở đây.

> **Nền đã có từ lát A** (đọc code trước khi sửa): `Shift` aggregate + `ShiftsController`,
> `Order` aggregate + `OrderService.MutateAsync` (If-Match ↔ xmin), outbox qua `Entity.Raise()` +
> `OutboxInterceptor`, `DomainExceptionHandler` (ProblemDetails tiếng Việt), `FallbackPolicy`
> bắt đăng nhập, gateway khai báo route bằng code ở `Gateway/Program.cs`.

## 0. Bằng chứng thành công (đọc trước tiên)

Tác vụ chỉ hoàn thành khi **toàn bộ** lệnh sau chạy xanh, từ gốc repo, trên working tree sạch:

```bash
npm run sdlc:evals                           # E01–E05 + P01–P04 xanh (P04: 4 gói gRPC có trong bảng §4)
npm run sdlc:verify -- --all                 # backend: format+build+unit · web: lint+build+vitest
docker compose down -v && docker compose up -d --build   # 8 container healthy
npm --prefix backend run seed                # "Seeded: 3 users, 20 menu items, 1 closed shift, 1 open shift, 3 open orders, 6 paid orders"
set -a && . ./.env && set +a && npm run sdlc:verify -- --all --e2e   # + integration (Testcontainers) + Playwright
```

Cộng thêm bằng chứng cụ thể của lát này:

- [ ] `backend/tests/Cashier.IntegrationTests/PaymentIdempotencyTests.cs` — `Payments_ConcurrentSameKey_CreatesOneLedgerEntry`: 5 request song song cùng key → cả 5 nhận 200 với **body giống hệt**, bảng `payments` đúng **1** dòng. Đỏ khi bỏ `ON CONFLICT DO NOTHING` / bỏ bước đọc lại key (tiêu chí **#5**)
- [ ] Cùng file: `Payments_SameKeyDifferentBody_Returns422`, `Payments_MissingKey_Returns400`, `Payments_TotalMismatch_Returns409` (detail có tổng thật, không có dòng `payments`), `Payments_NoOpenShift_Returns409`, `Payments_LostReply_RetrySameKeyCompletes`, `Payments_OrderingUnavailable_Returns503_NoLedgerEntry`, `Payments_TwoKeysSameOrder_OnlyOneCompleted`
- [ ] `backend/tests/Cashier.IntegrationTests/ShiftTests.cs` — `CloseShift_ReconcilesCash` (quỹ đầu 0 + 2 tiền mặt + 1 chuyển khoản, đếm lệch 5.000 → `expectedCash`, `variance = -5000`, `orderCount`, `revenue`, `cashTotal`, `transferTotal` đúng) và `CloseShift_ConcurrentWithPayments_NoPaymentAfterClose` (payment đang giữ ca + đóng ca chen vào → đóng ca **chờ** và tính cả payment đó). Đỏ khi bỏ `FOR SHARE`/`FOR UPDATE` (tiêu chí **#8**)
- [ ] `backend/tests/Ordering.IntegrationTests/MarkPaidTests.cs` — gọi gRPC thật qua TestServer: `MarkPaid_TotalMismatch_ReturnsActualTotal`, `MarkPaid_SamePaymentIdTwice_IsIdempotent`, `MarkPaid_OtherPaymentId_AlreadyPaid`, `MarkPaid_WritesOrderPaidToOutbox`, `MarkPaid_WhileKitchenUpdates_RetriesAndSucceeds`, `MarkPaid_ConcurrentDifferentPayments_OnlyOneWins`, `MarkPaid_WithoutToken_Unauthenticated`, `MarkPaid_TotalWithDifferentScale_Matches`, `Kitchen_PaidOrderWithPendingItems_StillVisible`
- [ ] `web/e2e/order-to-payment.spec.ts` — (a) luồng đầy đủ spec §7: đăng nhập → mở ca → 3 món → bếp thấy ≤ 2s → "Xong" → thu tiền mặt → đóng ca, nhập tiền đếm, thấy tổng kết; (b) biến thể **20 phần / ≥ 10 dòng**: từ bấm "Xác nhận thu" tới biên nhận hiện **< 2000ms**, ghi vào `docs/evidence/01-260925-fnb-pos-core/b-latency.log` (tiêu chí **#4**). Chạy **qua gateway 8080** (nginx 5173 → gateway)
- [ ] Ảnh trong `docs/evidence/01-260925-fnb-pos-core/`: `b-payment-form.png`, `b-payment-receipt.png`, `b-total-mismatch-409.png` (409 **thật**: tab thứ hai thêm món trong lúc tab một đang mở form thu), `b-close-shift-summary.png`, `b-close-shift-open-orders-warning.png`, `b-payment-offline.png`
- [ ] Các test trong `requiredTests` (4 cũ + 2 mới) tồn tại và xanh; `ConcurrencyTests`, `OutboxTests`, `AuthTests` của lát A **không** đổi kỳ vọng

## 1. Các file sẽ chạm

### 1a. Config & tài liệu

| File | Hành động | Mục đích |
|------|-----------|----------|
| `sdlc.config.json` | sửa | `requiredTests` += `PaymentIdempotencyTests.cs`, `MarkPaidTests.cs`; `e2eTriggers`: thay `backend/src/Cashier/Cashier.Domain` bằng `backend/src/Cashier`, thêm `backend/src/Shared/Protos` |
| `docker-compose.yml` | sửa | `ordering`: `Kestrel__Endpoints__Http__Url=http://+:8082`, `Kestrel__Endpoints__Grpc__Url=http://+:8092`, `Kestrel__Endpoints__Grpc__Protocols=Http2` (giữ `ASPNETCORE_HTTP_PORTS=8082` cho healthcheck; **không** publish 8092). `cashier`: `Services__OrderingGrpc=http://ordering:8092`, `depends_on: ordering: service_healthy` |
| `docs/api/endpoints.md` | sửa | `POST /api/payments`, body mới của `close`, `GET /api/orders?active=true`, bảng lỗi 422/503, mục gRPC nội bộ |
| `docs/database/schema.md` | sửa | `payments`, `idempotency_keys`, `orders.paid_at`/`paid_payment_id`; ghi lệch spec (xem B2) |
| `docs/architecture/STRUCTURE.md` | sửa | thêm `backend/src/Shared/Protos/` |
| `README.md` | sửa | bảng đối chiếu JD: điền dòng **Idempotency**, **gRPC**; dòng output seed mới |
| `CHANGELOG.md` | sửa | mục mới (bước 5 của `/sdlc:build`) |
| `docs/intents/INDEX.md` | sửa | trạng thái lát B |

### 1b. `backend/`

| File | Hành động | Mục đích |
|------|-----------|----------|
| `Directory.Packages.props` | sửa | 4 gói gRPC ở §4, version stable mới nhất lúc build, ghim cứng |
| `src/Shared/Protos/order_payments.proto` | tạo | `package fnb.ordering.v1; service OrderPayments { rpc MarkPaid }` — tiền là **string** invariant, không `double` |
| `src/Shared/Shared.Kernel/DomainException.cs` | sửa | thêm `UnprocessableRequestException` (422), `DependencyUnavailableException` (503) |
| `src/Shared/Shared.Kernel/Topics.cs` | sửa | `OrderPaid = "fnb.ordering.order-paid.v1"` |
| `src/Shared/Shared.Web/WebExtensions.cs` | sửa | `DomainExceptionHandler` map 422, 503 |
| `src/Ordering/Ordering.Domain/Order.cs` | sửa | `PaidAt`, `PaidPaymentId`, `MarkPaid(paymentId, expectedTotal, now)` → `MarkPaidOutcome`; `SetItemStatus` cho phép khi `Paid`; `IsActive`; record `OrderPaid` |
| `src/Ordering/Ordering.Application/OrderService.cs` | sửa | `MarkPaidAsync` (re-read + retry ≤ 3 lần khi xmin xung đột, notify sau commit) |
| `src/Ordering/Ordering.Application/Abstractions.cs` | sửa | `ListByShiftAsync` nhận scope `Active` |
| `src/Ordering/Ordering.Application/Views.cs` | sửa | `OrderView` thêm `PaidAt` |
| `src/Ordering/Ordering.Infrastructure/OrderRepository.cs` | sửa | lọc Active (Open, hoặc Paid còn món Pending/Preparing) |
| `src/Ordering/Ordering.Infrastructure/OrderingDbContext.cs` | sửa | map `paid_at`, `paid_payment_id` unique-nullable |
| `src/Ordering/Ordering.Infrastructure/Migrations/<ts>_OrderPaid.cs` (+ Designer, snapshot) | tạo | migration **chỉ thêm cột** |
| `src/Ordering/Ordering.Api/Ordering.Api.csproj` | sửa | `Grpc.AspNetCore`; `<Protobuf Include="..\..\Shared\Protos\order_payments.proto" GrpcServices="Server" />` |
| `src/Ordering/Ordering.Api/Grpc/OrderPaymentsService.cs` | tạo | `[Authorize(Roles = "Cashier,Owner")]`, parse tiền invariant, `NotFound` → `RpcException(StatusCode.NotFound)` |
| `src/Ordering/Ordering.Api/Program.cs` | sửa | `AddGrpc()`, `MapGrpcService<OrderPaymentsService>()` |
| `src/Ordering/Ordering.Api/Controllers/OrdersController.cs` | sửa | `GET /api/orders?active=true`; `/api/kitchen/orders` dùng Active |
| `src/Cashier/Cashier.Domain/Shift.cs` | sửa | `Close(countedCash, cashTotal, now)` tính `ExpectedCash`, `Variance`; `ShiftClosed` thêm `CountedCash, ExpectedCash, Variance` (thêm trường, không đổi topic) |
| `src/Cashier/Cashier.Domain/Payment.cs` | tạo | `Payment` (id = Idempotency-Key), `PaymentMethod { Cash, Transfer }`, `PaymentStatus` |
| `src/Cashier/Cashier.Application/IOrderPayments.cs` | tạo | cổng sang ordering + `MarkPaidResult` (Ok / TotalMismatch(actual) / AlreadyPaid / OrderCancelled / NotFound) |
| `src/Cashier/Cashier.Application/IPaymentLedger.cs` | tạo | transaction: giữ key, khoá ca, ghi payment, lưu phản hồi; tổng tiền theo ca |
| `src/Cashier/Cashier.Application/PayOrderHandler.cs` | tạo | luồng thu tiền (B5) |
| `src/Cashier/Cashier.Application/CloseShiftHandler.cs` | sửa | khoá ca `FOR UPDATE` → cộng payments → `Close` → trả tổng kết |
| `src/Cashier/Cashier.Application/IShiftRepository.cs` | sửa | nếu cần thêm `LockForCloseAsync` |
| `src/Cashier/Cashier.Infrastructure/Cashier.Infrastructure.csproj` | sửa | `Grpc.Net.ClientFactory`, `Google.Protobuf`, `Grpc.Tools` (PrivateAssets); `<Protobuf ... GrpcServices="Client" />` |
| `src/Cashier/Cashier.Infrastructure/CashierDbContext.cs` | sửa | `payments`, `idempotency_keys` |
| `src/Cashier/Cashier.Infrastructure/Migrations/<ts>_Payments.cs` (+ Designer, snapshot) | tạo | hai bảng mới, partial unique `(order_id) WHERE status = 'Completed'` |
| `src/Cashier/Cashier.Infrastructure/PaymentLedger.cs` | tạo | SQL `INSERT … ON CONFLICT DO NOTHING`, `SELECT … FOR SHARE` |
| `src/Cashier/Cashier.Infrastructure/ShiftRepository.cs` | sửa | `SELECT … FOR UPDATE` khi đóng ca |
| `src/Cashier/Cashier.Infrastructure/OrderPaymentsClient.cs` | tạo | gRPC client, deadline 3s, `Unavailable`/`DeadlineExceeded` → `DependencyUnavailableException` |
| `src/Cashier/Cashier.Infrastructure/ForwardAuthHandler.cs` | tạo | `DelegatingHandler` chép `Authorization` của request hiện tại sang lời gọi gRPC |
| `src/Cashier/Cashier.Infrastructure/DependencyInjection.cs` | sửa | `AddHttpContextAccessor`, `AddGrpcClient<…>().AddHttpMessageHandler<ForwardAuthHandler>()`, đăng ký handler/ledger |
| `src/Cashier/Cashier.Api/Controllers/PaymentsController.cs` | tạo | `POST /api/payments`, `[Authorize(Roles = "Cashier,Owner")]`, đọc header `Idempotency-Key` |
| `src/Cashier/Cashier.Api/Contracts/PaymentContracts.cs` | tạo | `CreatePaymentRequest` (đúng spec §4), `PaymentResponse` |
| `src/Cashier/Cashier.Api/Contracts/ShiftContracts.cs` | sửa | `CloseShiftRequest(decimal CountedCash)`, `ShiftSummaryResponse` |
| `src/Cashier/Cashier.Api/Controllers/ShiftsController.cs` | sửa | `close` nhận body |
| `src/Gateway/Program.cs` | sửa | `Route("/api/payments", "cashier")` |
| `tools/Seeder/DemoData.cs` | sửa | ca hôm qua: 6 đơn Paid + payments (tiền mặt & chuyển khoản) + đóng ca có đối chiếu; chuỗi tổng kết mới |
| `tests/Ordering.UnitTests/OrderTests.cs` | sửa | luật `MarkPaid`; `SetItemStatus` khi Paid được, `AddItem`/`CancelItem` khi Paid bị chặn |
| `tests/Cashier.UnitTests/ShiftTests.cs` | sửa | `Close_ComputesExpectedAndVariance`; cập nhật lời gọi `Close` cũ |
| `tests/Ordering.IntegrationTests/MarkPaidTests.cs` | tạo | xem mục 0 |
| `tests/Ordering.IntegrationTests/Ordering.IntegrationTests.csproj` | sửa | `Grpc.Tools` + `<Protobuf ... GrpcServices="Client" />` (runtime gRPC đến qua `Ordering.Api`) |
| `tests/Cashier.IntegrationTests/PaymentIdempotencyTests.cs` | tạo | xem mục 0 |
| `tests/Cashier.IntegrationTests/FakeOrderPayments.cs` | tạo | giả ordering: nhớ `orderId → paymentId` (cùng luật idempotent), trễ cấu hình được, chế độ "làm xong rồi mất phản hồi", "Unavailable" |
| `tests/Cashier.IntegrationTests/CashierFixture.cs` | sửa | `ConfigureTestServices` thay `IOrderPayments` bằng fake |
| `tests/Cashier.IntegrationTests/ShiftTests.cs` | sửa | lời gọi `close` gửi body `{ countedCash }`; 2 ca mới |
| `tests/Gateway.Tests/GatewayTests.cs` | sửa | `InlineData("/api/payments", "cashier")` |
| `tests/Seeder.IntegrationTests/SeederTests.cs` | sửa | chuỗi tổng kết mới; ca hôm qua có `variance` |

### 1c. `web/`

| File | Hành động | Mục đích |
|------|-----------|----------|
| `src/lib/apiClient.ts` | sửa | tuỳ chọn `idempotencyKey` → header `Idempotency-Key`; giữ nguyên khi retry sau refresh 401 |
| `src/lib/__tests__/apiClient.test.ts` | sửa | gửi header; retry sau 401 **cùng** key |
| `src/lib/queryKeys.ts` | sửa | `orders.list` theo scope active (nếu cần) |
| `src/features/orders/useOrders.ts` | sửa | `/api/orders?active=true` |
| `src/features/orders/orderSchemas.ts` | sửa | `paidAt` nullable |
| `src/features/orders/OrderPanel.tsx` | sửa | nút "Thu tiền" (đơn Open, tổng > 0), `PaymentForm` inline, biên nhận; đơn Paid chỉ hiện trạng thái bếp |
| `src/features/payment/usePayOrder.ts` | tạo | mutation, key sinh bằng `crypto.randomUUID()` lúc **mở** form, dùng lại khi bấm lại |
| `src/features/payment/paymentSchemas.ts` | tạo | Zod cho `PaymentResponse` |
| `src/features/payment/PaymentForm.tsx` | tạo | chọn "Tiền mặt"/"Chuyển khoản", "Xác nhận thu {tổng}", disabled khi offline |
| `src/features/payment/Receipt.tsx` | tạo | biên nhận: mã đơn, món, tổng, phương thức, giờ |
| `src/features/payment/__tests__/paymentSchemas.test.ts` | tạo | parse phản hồi thật mẫu, từ chối số tiền là chuỗi |
| `src/features/shift/useShift.ts` | sửa | `useCloseShift` gửi `{ countedCash }`, parse `ShiftSummary` |
| `src/features/shift/CloseShiftForm.tsx` | tạo | đếm mù: nhập tiền đếm → xác nhận → hiện số đơn, doanh thu, dự kiến, đếm được, lệch; cảnh báo (không chặn) khi còn đơn Open |
| `src/features/shift/ShiftBar.tsx` | sửa | "Đóng ca" mở `CloseShiftForm` thay vì đóng ngay |
| `e2e/order-to-payment.spec.ts` | tạo | xem mục 0 |
| `e2e/helpers.ts` | sửa | `ensureShiftOpen` đi qua form đóng/mở ca mới nếu cần |

**Không** chạm tới file nào ngoài danh sách này mà không cập nhật plan trước.

## 2. Các bước thực thi

Đánh dấu `[x]` ngay khi xong từng bước, trước khi sang bước sau.

### Nhóm B0 — Khung

- [ ] **B0.1** `sdlc.config.json` theo bảng 1a. → kiểm chứng: `npm run sdlc:evals` xanh; sửa một dòng trong `backend/src/Cashier/Cashier.Api` rồi `npm run sdlc:verify` in e2e được bật.
- [ ] **B0.2** Thêm 4 gói gRPC vào `Directory.Packages.props` (tra version stable mới nhất bằng `dotnet package search <tên> --exact-match`, ghim cứng). → kiểm chứng: `npm run sdlc:evals` — P04 xanh.

### Nhóm B — Backend

- [ ] **B1** Ordering domain: `Order.MarkPaid(paymentId, expectedTotal, now)`:
  đã Paid **cùng** `paymentId` → `Ok` không làm gì; Paid khác `paymentId` → `AlreadyPaid`;
  `Cancelled` → `OrderCancelled`; `Total != expectedTotal` (so `decimal`) → `TotalMismatch(Total)`;
  còn lại → `Status = Paid`, `PaidAt`, `PaidPaymentId`, `Touch`, `Raise(new OrderPaid(OrderId, ShiftId, PaymentId, Total, PaidAt, Lines[(MenuItemId, Qty)] của món chưa huỷ))`.
  `SetItemStatus` cho phép khi `Open` **hoặc** `Paid`; `AddItem`/`CancelItem`/`Cancel` vẫn chỉ khi `Open`.
  `Topics.OrderPaid`. → kiểm chứng: `npm --prefix backend test` — ca mới trong `OrderTests` xanh, đỏ khi bỏ nhánh "cùng paymentId".
- [ ] **B2** Migration ordering (`paid_at timestamptz null`, `paid_payment_id uuid null` unique) và cashier (`payments`, `idempotency_keys(key, endpoint) pk`, `request_hash`, `response_status`, `response_body jsonb`, `created_at`). Ghi lệch spec vào `schema.md`: **chỉ dòng `Completed` được lưu** — `Pending` chỉ tồn tại trong transaction chưa commit nên không bao giờ nhìn thấy được; cột `status` và index partial giữ nguyên như spec làm chốt chặn thứ hai. → kiểm chứng: `dotnet ef migrations script` của mỗi dịch vụ chỉ có `ADD COLUMN`/`CREATE TABLE`/`CREATE INDEX`, không `DROP`/`ALTER … TYPE`.
- [ ] **B3** Proto + server: `order_payments.proto` (`MarkPaidRequest { string order_id; string expected_total; string payment_id; }`, `MarkPaidReply { oneof result { Ok ok; TotalMismatch total_mismatch; AlreadyPaid already_paid; OrderCancelled order_cancelled; } }`, `TotalMismatch { string actual_total; }`). `OrderPaymentsService` parse `decimal.Parse(s, CultureInfo.InvariantCulture)`, trả `actual_total` bằng `ToString("0.00", InvariantCulture)`. `OrderService.MarkPaidAsync`: vòng tối đa 3 lần {đọc đơn → `MarkPaid` → `TrySaveAsync`}; xung đột xmin → đọc lại, kiểm lại; hết lượt → `RpcException(Aborted)`. Notify `orderUpdated` **sau** commit. Kestrel hai endpoint qua env compose (1a). → kiểm chứng: `MarkPaidTests` xanh; `MarkPaid_WhileKitchenUpdates_RetriesAndSucceeds` dùng một `SaveChangesInterceptor` của test chèn `UPDATE orders … ` từ connection khác trước lần lưu đầu → buộc xung đột thật; đỏ khi hạ retry xuống 1.
- [ ] **B4** Scope "Active": `GET /api/orders?active=true` (giữ `?status=` cho tương thích) và `/api/kitchen/orders` = Open **hoặc** (Paid **và** còn món `Pending`/`Preparing`). → kiểm chứng: `Kitchen_PaidOrderWithPendingItems_StillVisible` xanh; bếp PATCH món của đơn Paid → 200.
- [ ] **B5** Cashier thu tiền — `PayOrderHandler`, **một** transaction Postgres:
  1. `Idempotency-Key` thiếu / không phải UUID → `InvalidRequestException` 400 "Yêu cầu không hợp lệ." (log lý do phía server **không** kèm giá trị key).
  2. `request_hash = SHA-256("{OrderId}|{ExpectedTotal.ToString("0.00", Invariant)}|{Method}")` — chuẩn hoá scale để `45000` và `45000.00` không bị coi là khác.
  3. `INSERT INTO idempotency_keys (key, endpoint='POST /api/payments', request_hash) … ON CONFLICT DO NOTHING`. Request trùng key đang chạy song song sẽ **chờ** ở đây tới khi request đầu commit/rollback.
  4. 0 dòng → `SELECT` dòng đó: hash khác → 422 "Yêu cầu không khớp với lần gửi trước."; hash giống → trả nguyên `response_status` + `response_body` đã lưu.
  5. `SELECT … FROM shifts WHERE closed_at IS NULL FOR SHARE` — không có → 409 "Chưa mở ca làm việc."
  6. gRPC `MarkPaid(orderId, expectedTotal, paymentId = Idempotency-Key)`, deadline 3s, JWT của người gọi đi kèm. `TotalMismatch` → 409 "Đơn vừa thay đổi, tổng tiền hiện tại là {x}. Kiểm tra lại rồi thu tiền." (x định dạng `N0` vi-VN); `AlreadyPaid` → 409 "Đơn này đã được thanh toán."; `OrderCancelled` → 409 "Đơn đã bị huỷ."; `NotFound` → 404 "Không tìm thấy đơn."; không kết nối/hết giờ → 503 "Không kết nối được dịch vụ đơn hàng. Thử lại sau giây lát."
  7. `Ok` → insert `payments` (`id = key`, `status = Completed`), lưu phản hồi 200 vào `idempotency_keys`, COMMIT.
  Mọi lỗi ở bước 5–6 **rollback** cả dòng key → bấm lại cùng key vẫn chạy lại được.
  → kiểm chứng: `PaymentIdempotencyTests` xanh; đột biến bỏ `ON CONFLICT` → `ConcurrentSameKey` đỏ (500 / 2 dòng); đột biến lưu cả phản hồi lỗi → `LostReply` đỏ.
- [ ] **B6** Đóng ca có đối chiếu: `POST /api/shifts/{id}/close` body `CloseShiftRequest([Range(typeof(decimal), "0", "1000000000")] decimal CountedCash)`. Handler: `SELECT … FOR UPDATE` ca → cộng payments của ca (`cashTotal`, `transferTotal`, `orderCount`) → `Shift.Close(countedCash, cashTotal, now)` (`ExpectedCash = OpeningFloat + cashTotal`, `Variance = CountedCash − ExpectedCash`) → trả `ShiftSummaryResponse` (trường của `Shift` + `OrderCount`, `Revenue`, `CashTotal`, `TransferTotal`). `ShiftClosed` mang thêm `CountedCash, ExpectedCash, Variance` cho lát D. → kiểm chứng: `CloseShift_ReconcilesCash`, `CloseShift_ConcurrentWithPayments_NoPaymentAfterClose` (fake MarkPaid trễ 500ms; đóng ca gửi sau 100ms phải chờ và `cashTotal` gồm payment đó; đỏ khi bỏ khoá), `Close_ComputesExpectedAndVariance` xanh; các ca `ShiftTests` cũ chỉ đổi phần gửi body.
- [ ] **B7** Nối dây: gRPC client + `ForwardAuthHandler`, route gateway `/api/payments`, compose (1a). → kiểm chứng: `GatewayTests` xanh; `docker compose up -d --build` → 8 container healthy; `curl` qua 8080 với token cashier: `POST /api/payments` thiếu key → 400.
- [ ] **B8** Seeder: ca hôm qua có 6 đơn Paid (4 tiền mặt, 2 chuyển khoản) + payments tương ứng + đóng ca đếm lệch nhỏ; chạy 2 lần không nhân đôi; chuỗi "…, 3 open orders, 6 paid orders". → kiểm chứng: `SeederTests` xanh; `npm --prefix backend run seed` hai lần cùng output.

### Nhóm W — Web (worktree riêng, xem mục 3)

- [ ] **W1** `apiClient`: `idempotencyKey?: string` → header `Idempotency-Key`; retry sau refresh 401 dùng **cùng** key. → kiểm chứng: `apiClient.test.ts` ca mới xanh, đỏ khi retry không kèm header.
- [ ] **W2** Thu tiền: `usePayOrder` (key tạo lúc mở form, giữ tới khi thành công hoặc đóng form; `onSuccess` invalidate `orders`), `PaymentForm` inline trong thẻ đơn, `Receipt` giữ ở `OrderPanel` từ `pay.data` (đơn biến khỏi danh sách vẫn thấy biên nhận, tới khi bấm "Đơn mới"), 409 → toast đúng `detail` + refetch đơn, offline → nút disabled. POS lấy `?active=true`. → kiểm chứng: `npm --prefix web run build` + vitest xanh; `paymentSchemas.test.ts`.
- [ ] **W3** `CloseShiftForm` + `useCloseShift` gửi body, parse Zod; cảnh báo "Còn {n} đơn chưa thu tiền" khi có đơn Open (vẫn cho đóng). → kiểm chứng: vitest; xem tay trên dev server.

### Nhóm I — Tích hợp (sau khi gộp W)

- [ ] **I1** Gộp nhánh web; `docker compose down -v && docker compose up -d --build && npm --prefix backend run seed`; viết `order-to-payment.spec.ts` (mục 0), sửa `helpers.ts`. Biến thể 20 phần: bấm vòng tròn các món còn hàng tới 20 phần, ≥ 10 dòng; "Gửi bếp" → "Thu tiền" → "Tiền mặt" → đo `performance.now()` từ bấm "Xác nhận thu" tới `Receipt` hiện, **< 2000ms**, ghi `b-latency.log`. → kiểm chứng: `npx playwright test` xanh 3 lần liên tiếp, log có 3 mốc.
- [ ] **I2** Chụp 6 ảnh ở mục 0 bằng Playwright. → kiểm chứng: 6 file tồn tại, `b-total-mismatch-409.png` có câu "Đơn vừa thay đổi…".
- [ ] **I3** Tài liệu 1a + `CHANGELOG.md` + README (JD: Idempotency, gRPC). → kiểm chứng: `grep -n "Idempotency\|gRPC" README.md` có dòng ở bảng JD.
- [ ] **I4** `npm run sdlc:verify -- --all --e2e` → `docs/evidence/01-260925-fnb-pos-core/verify-slice-b.log`.

## 3. Thứ tự & song song hoá

| Nhóm | Các bước | Có thể chạy song song? | Worktree |
|------|----------|------------------------|----------|
| B0 | B0.1–B0.2 | không — làm trước | chính (`feat/01-260925-fnb-pos-core-slice-b`) |
| B | B1→B2→B3→B4 (ordering) rồi B5→B6→B7→B8 (cashier cần proto ở B3) | **có**, song song với W | chính |
| W | W1→W2→W3 | **có** — chỉ chạm `web/`, xây theo hợp đồng spec §4 + mục 2 plan này, test với fetch giả | `../sdlc-fnb-microservices-cv-web`, nhánh `feat/01-260925-fnb-pos-core-slice-b-web` |
| I | I1→I4 | không | chính, sau khi merge W |

```bash
# sau B0.2, từ gốc repo:
git worktree add ../sdlc-fnb-microservices-cv-web -b feat/01-260925-fnb-pos-core-slice-b-web
# khi W xong, từ worktree chính:
git merge --no-ff feat/01-260925-fnb-pos-core-slice-b-web && git worktree remove ../sdlc-fnb-microservices-cv-web
```

W không chạm `sdlc.config.json`, `docker-compose.yml`, `web/e2e/**` — e2e thuộc I.

## 4. Ràng buộc kế thừa từ AGENTS.md và spec

Mọi luật ở §4 của `plan-slice-a.md` vẫn áp dụng (FallbackPolicy, `record` + DataAnnotations,
ProblemDetails tiếng Việt, `decimal(18,2)`, secret từ env, CORS allowlist, web chỉ gọi qua
`apiClient.ts`, không `any`/`@ts-ignore`, test cùng commit, `[Trait("Category","Integration")]`).
Riêng lát B:

- **Không log `Idempotency-Key`** (spec §8) — kể cả trong message lỗi 400. Log request của cashier
  giữ ở mức hiện tại (không log header).
- Endpoint gRPC có `[Authorize(Roles = "Cashier,Owner")]`; **không** thêm `[AllowAnonymous]`
  (eval P02). Cổng 8092 không publish ra host, gateway không có route tới nó.
- Tiền qua gRPC là `string` invariant-culture; **cấm** `double`/`float` trong `.proto`.
- Migration **chỉ thêm** (cột nullable, bảng mới) — deploy giữa ca không làm hỏng đơn đang mở.
- Thông báo lỗi: đúng câu chữ spec §4; câu mới (đơn đã huỷ, 503) ghi vào `endpoints.md`.
- Seeder và auto-migrate vẫn chỉ khi `ASPNETCORE_ENVIRONMENT=Development`.
- **Không tự ý thêm dependency** ngoài bảng dưới + các bảng ở `plan-slice-a.md`. Cần thêm → dừng, hỏi, thêm dòng bảng rồi mới cài (ca C03, eval P04).

### Dependency xin duyệt cho lát B

| Nơi | Gói | Lý do |
|-----|-----|-------|
| backend (`Ordering.Api`) | `Grpc.AspNetCore` | server gRPC `MarkPaid` — có trong spec §8 |
| backend (`Cashier.Infrastructure`) | `Grpc.Net.ClientFactory` | client gRPC qua `IHttpClientFactory`, gắn `DelegatingHandler` chuyển JWT — có trong spec §8 |
| backend (`Cashier.Infrastructure`, test ordering) | `Grpc.Tools` | sinh code C# từ `.proto` lúc build (`PrivateAssets=all`, không vào runtime). **Không có trong spec §8** — `Grpc.AspNetCore` kéo nó theo cho server, nhưng phía client phải tham chiếu trực tiếp |
| backend (`Cashier.Infrastructure`) | `Google.Protobuf` | runtime của message sinh ra phía client. **Không có trong spec §8** — cùng lý do trên |

Không cài `Grpc.AspNetCore.Web`, `Grpc.HealthCheck`, `Polly`/`Microsoft.Extensions.Http.Resilience`
(retry nằm ở người dùng bấm lại cùng key, không retry ngầm), `MediatR`. Web **không** thêm gói
nào (`crypto.randomUUID()` có sẵn; form thu tiền inline, không cần dialog của radix).

## 5. Tự chất vấn (bắt buộc — do Agent điền)

> Người điều phối hỏi: *"Thay đổi nào trong kế hoạch này có nguy cơ làm hỏng hệ thống
> hoặc xung đột với tính năng hiện có?"*

Bằng chứng tìm được khi đọc `Order.cs`, `OrderService.cs`, `OrdersController.cs`, `Shift.cs`,
`CloseShiftHandler.cs`, `ShiftRepository.cs`, `ShiftEventsConsumer.cs`, `DemoData.cs`,
`useShift.ts`, `ShiftBar.tsx`, `apiClient.ts`, `docker-compose.yml`:

| # | Nguy cơ | Vì sao có thể xảy ra | Cách phòng | Test nào bắt được |
|---|---------|----------------------|------------|-------------------|
| 1 | **Mất phản hồi gRPC**: ordering đã `Paid` nhưng cashier timeout → rollback, không có bút toán | Mạng / deadline 3s | `paymentId = Idempotency-Key`; POS bấm lại **cùng** key → `MarkPaid` thấy cùng `paymentId` → `Ok` → ghi bút toán. **Trần:** người dùng bỏ ngang không bấm lại → đơn Paid bên ordering, không có dòng `payments`. Lát D đối chiếu `OrderPaid` với payments để phát hiện | `Payments_LostReply_RetrySameKeyCompletes`, `MarkPaid_SamePaymentIdTwice_IsIdempotent` |
| 2 | **Transaction cashier mở trong lúc gọi gRPC** giữ khoá key + `FOR SHARE` ca | Thiết kế một transaction | Deadline 3s cứng; lỗi → rollback, không lưu phản hồi lỗi để bấm lại được | `Payments_OrderingUnavailable_Returns503_NoLedgerEntry` |
| 3 | **Bếp và thu tiền ghi cùng dòng `orders`** → xmin đổi → `MarkPaid` thất bại oan dù tổng tiền không đổi | `SetItemStatus` và `MarkPaid` cùng lưu `orders` (spec §4 nói rõ) | Retry ≤ 3 lần, **đọc lại và kiểm lại tổng** mỗi lần | `MarkPaid_WhileKitchenUpdates_RetriesAndSucceeds` (xung đột ép bằng interceptor) |
| 4 | **Đơn Paid biến mất khỏi bếp**: `/api/kitchen/orders` lọc `Status == Open`, `OpenItem()` ném lỗi khi đơn không Open → thu tiền trước khi pha xong thì bếp không thấy và không bấm được | Code lát A giả định chỉ Open mới cần bếp | Scope Active; `SetItemStatus` cho phép khi Paid | `Kitchen_PaidOrderWithPendingItems_StillVisible`, `OrderTests` |
| 5 | **Thu tiền chen giữa lúc đóng ca** → bút toán rơi vào ca đã chốt, lệch tiền | Hai request khác endpoint | Payment `FOR SHARE` ca mở; đóng ca `FOR UPDATE` → tuần tự hoá | `CloseShift_ConcurrentWithPayments_NoPaymentAfterClose` |
| 6 | **Đổi hợp đồng `close`** (thêm body bắt buộc) làm vỡ `ShiftBar`, `ShiftTests` (gửi `null`), seeder (`yesterday.Close(now)`), unit test | Lời gọi tìm bằng grep: `ShiftTests.cs:78,79,91`, `DemoData.cs:106`, `Cashier.UnitTests/ShiftTests.cs:36,47,58,60`, `useShift.ts:52` | Cập nhật cả 4 nơi trong cùng bước (B6, B8, W3); kỳ vọng cũ (409 khi đóng hai lần, 404) **giữ nguyên** — đây là đổi hợp đồng theo spec §4, không phải nới test | `ShiftTests` (cũ + mới), `SeederTests` |
| 7 | **h2c và HTTP/1.1 chung Kestrel**: gRPC cần HTTP/2 không TLS; cấu hình `Kestrel__Endpoints` **ghi đè** `ASPNETCORE_HTTP_PORTS` → healthcheck có thể hỏng | Kestrel ưu tiên endpoints tường minh | Endpoint `Http` giữ đúng 8082 (khớp biến healthcheck); gRPC riêng 8092 `Http2` | Kiểm chứng B7 (8 container healthy) + e2e thu tiền qua compose |
| 8 | **gRPC bị `FallbackPolicy` chặn** (401) vì cashier gọi không kèm token | Lát A bắt đăng nhập mặc định | `ForwardAuthHandler` chuyển JWT người gọi; không nới policy | `MarkPaid_WithoutToken_Unauthenticated` + e2e |
| 9 | **Số thập phân qua protobuf / so hash**: `double` mất chính xác; `45000` vs `45000.00` ra hash khác → 422 oan | Protobuf không có decimal | Tiền là string invariant; hash dùng `"0.00"` | `MarkPaid_TotalWithDifferentScale_Matches`, `Payments_SameKeyDifferentBody_Returns422` |
| 10 | **Deploy giữa ca**: migration đổi schema dưới chân đơn đang mở; `ShiftClosed` thêm trường làm vỡ consumer ordering | Ordering có bản sao record `ShiftClosed` riêng (`ShiftEventsConsumer.cs:17`) | Migration chỉ thêm cột nullable; System.Text.Json bỏ qua trường lạ → consumer cũ đọc được | Kiểm chứng B2 (script không có DROP) + `ShiftProjectionTests` |
| 11 | **Lộ `Idempotency-Key` trong log** | Log lỗi 400 dễ in giá trị header | Log chỉ ghi lý do ("thiếu"/"không phải UUID") | Rà ở `/sdlc:ship` + grep `docker compose logs cashier` sau e2e không có key |
| 12 | **Hai key khác nhau cho cùng đơn** (hai máy POS) → thu hai lần | Idempotency theo key, không theo đơn | Ordering chỉ nhận một `paymentId` (AlreadyPaid); index partial `(order_id) WHERE Completed` chặn lần hai ở DB | `Payments_TwoKeysSameOrder_OnlyOneCompleted`, `MarkPaid_ConcurrentDifferentPayments_OnlyOneWins` |

**Tính năng hiện có có thể bị ảnh hưởng:** bảng bếp (lọc Active), danh sách đơn POS (đổi query
sang `?active=true`), đóng ca (hợp đồng mới), seeder (chuỗi tổng kết, `SeederTests`, README),
projection ca ở ordering (sự kiện `ShiftClosed` thêm trường), `ConcurrencyTests` (xmin — không
đổi kỳ vọng), gateway (route mới).

**Nếu phải quay đầu:** `git revert` dải commit của lát. Migration chỉ thêm → DB dev có thể giữ
nguyên hoặc `docker compose down -v`. Không có dữ liệu thật (Seeder/auto-migrate chặn ngoài
Development).

## 6. Điều KHÔNG làm trong lần này

- Tồn kho, trừ kho theo `OrderPaid`, cờ hết hàng do sự kiện — **lát C** (lát B chỉ **phát** `OrderPaid`).
- `reporting`, màn báo cáo, URL state, phát hiện đơn Paid không có bút toán (nguy cơ #1) — **lát D**.
- Sự kiện Kafka riêng cho payment (spec §5 ghi "ghi outbox" ở cashier nhưng §4 không có topic
  nào của payment; lát D lấy tổng tiền theo phương thức từ `ShiftClosed` đã thêm trường).
- Hoàn tiền, huỷ thanh toán, thanh toán một phần / tách bill, cổng thanh toán thật, in hoá đơn.
- Chặn đóng ca phía server khi còn đơn Open (chỉ cảnh báo ở UI).
- POS xem đơn của ca khác; nhập quỹ đầu ca trên UI (vẫn `0`).
- Retry ngầm phía server khi gọi gRPC (retry là người dùng bấm lại cùng key).

---

## Duyệt của người điều phối

> _Chốt chặn con người thứ hai. Đọc mục 5 trước tiên._
> _Đồng ý → tick đủ các ô và chạy `/sdlc:build 01-260925-fnb-pos-core`._

- [ ] Tôi đã đọc mục 5 và các nguy cơ là chấp nhận được
- [ ] Bằng chứng thành công ở mục 0 là đủ để tôi tin tính năng chạy đúng
- [ ] Danh sách dependency ở mục 4 được duyệt (đặc biệt `Grpc.Tools`, `Google.Protobuf` — không có trong spec §8)
