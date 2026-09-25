# Verify — 01-260925-fnb-pos-core (lát B)
Ngày: 2026-09-25 23:48   Commit: f9ed2d4   Nhánh: `feat/01-260925-fnb-pos-core-slice-b`

Phạm vi: **lát B** của spec §11 (tiền: thu tiền idempotent, gRPC `MarkPaid`, đối chiếu két khi đóng ca).
Lát B chịu tiêu chí **4, 5, 8**, và bổ sung phần tiền cho 1, 11, 12. Tiêu chí 6, 7, 10 và phần ca đêm của 9
thuộc lát C/D, **chưa** kiểm ở đây. Kết quả lát A để nguyên bên dưới.

## Cổng tự động

`npm run sdlc:verify -- --all --e2e` trên stack compose dựng lại từ `down -v` — log đầy đủ: `verify-slice-b.log`.

| Cổng | Workspace | Kết quả |
|------|-----------|---------|
| format (`dotnet format --verify-no-changes`) | backend | ✅ |
| build (`-warnaserror`) | backend | ✅ |
| test (unit + gateway) | backend | ✅ 46/46 |
| e2e (Testcontainers Postgres/Kafka/Redis, gRPC qua TestServer) | backend | ✅ 52/52 |
| lint (ratchet) | web | ✅ |
| build (`tsc -b && vite build`) | web | ✅ |
| test (vitest) | web | ✅ 16/16 |
| e2e (Playwright, qua nginx → gateway, `workers: 1`) | web | ✅ 5/5 |
| eval quy trình (`npm run sdlc:evals`) | — | ✅ 9/9 |

Tổng **119/119 test xanh** (lát A 81 + lát B 38).

Lần chạy đầu `web:test` đỏ: cả 6 worker vitest quá hạn 60s lúc khởi động, ngay sau khi Testcontainers
dọn container — **không test nào chạy**. Chạy riêng `npm --prefix web test`: 16/16 xanh; chạy lại toàn bộ: xanh.
Không đổi cấu hình hay test nào để qua cổng.

## Tiêu chí chấp nhận

| # | Tiêu chí | Bằng chứng | ✅/❌ |
|---|----------|------------|-------|
| 4 | Bấm Thanh toán → hiện bill, đơn 20 món ≤ 2s | `web/e2e/order-to-payment.spec.ts` test 1: đơn 20 phần / 10 dòng, đo từ bấm "Xác nhận thu" tới biên nhận, qua gateway. `b-latency.log`: 4 lần đo **201–949ms** (ngân sách 2000ms). `b-payment-form.png`, `b-payment-receipt.png` | ✅ |
| 5 | Gửi lại lệnh thanh toán không tạo bút toán thứ hai | `PaymentIdempotencyTests.Payments_ConcurrentSameKey_CreatesOneLedgerEntry` (5 request song song cùng key → 5 phản hồi giống hệt, **1** dòng `payments`); `Payments_LostReply_RetrySameKeyCompletes`; `Payments_SameKeyDifferentBody_Returns422`; `Payments_TwoKeysSameOrder_OnlyOneCompleted`; `MarkPaidTests.MarkPaid_SamePaymentIdTwice_IsIdempotent`. Phía web: `apiClient_401Retry_KeepsSameIdempotencyKey`, `paymentForm_RetryAfterError_ReusesKey_ReopenMintsNewKey`. Tổng tiền ca không đổi: `CloseShift_ReconcilesCash` cộng từ `payments` | ✅ |
| 8 | Đóng ca: số đơn, doanh thu, tiền dự kiến, tiền đếm, phần lệch | `ShiftTests.CloseShift_ReconcilesCash` (lệch −5.000); `CloseShift_ConcurrentWithPayments_NoPaymentAfterClose` (đóng ca chen vào chờ payment đang chạy); `Close_ComputesExpectedAndVariance` (unit). e2e test 2: luồng đầy đủ tới tổng kết, `variance = countedCash − expectedCash`. `b-close-shift-summary.png` (**Thiếu 2.000 ₫**), `b-close-shift-open-orders-warning.png` | ✅ |
| 1 | Một lệnh dựng, một lệnh seed (dữ liệu mẫu) | Seed có thêm ca hôm qua đã đóng với 6 đơn đã thu + bút toán: `SeederTests.YesterdayShift_ReconciledAgainstItsPayments`, `SeedTwice_SameSummary_NoDuplicates`. Nguyên liệu vẫn chờ lát C | ✅ phần A+B · ⏳ lát C |
| 11 | README có sơ đồ + bảng đối chiếu JD | Sơ đồ có cạnh gRPC; dòng **Idempotency**, **gRPC** đã điền kèm test (`grep -n "Idempotency\|gRPC" README.md`) | ✅ phần A+B · rà tay ở lát D |
| 12 | Bằng chứng cho 4 luồng | Thanh toán + chốt ca: 6 ảnh `b-*.png` + `b-latency.log`. Trừ kho: lát C | ✅ 3/4 luồng · ⏳ lát C |

### Trạng thái đã chụp (lát B)

| Trạng thái | Ảnh |
|------------|-----|
| Form thu: chọn phương thức, "Xác nhận thu {tổng}" | `b-payment-form.png` |
| Thành công: biên nhận, đơn gắn "Đã thu" | `b-payment-receipt.png` |
| Lỗi: 409 tổng tiền đổi giữa chừng | `b-total-mismatch-409.png` (409 **thật**: tab hai huỷ một món; tab một bị chặn SignalR nên còn giữ tổng cũ) |
| Lỗi: mất kết nối, nút xác nhận bị khoá | `b-payment-offline.png` |
| Cảnh báo: đóng ca khi còn đơn chưa thu | `b-close-shift-open-orders-warning.png` |
| Thành công: tổng kết ca, lệch "Thiếu" | `b-close-shift-summary.png` |

## Test hồi quy (`requiredTests`)

- `Cashier.IntegrationTests/PaymentIdempotencyTests.cs` (mới): pass. Đột biến khi build: bỏ `ON CONFLICT DO NOTHING` / bỏ bước đọc lại key → đỏ.
- `Ordering.IntegrationTests/MarkPaidTests.cs` (mới): pass. Đột biến: hạ số lần thử lại xuống 1 → `MarkPaid_WhileKitchenUpdates_RetriesAndSucceeds` đỏ.
- `Cashier.IntegrationTests/ShiftTests.cs`: pass. Đột biến: bỏ `FOR SHARE`/`FOR UPDATE` → `CloseShift_ConcurrentWithPayments_NoPaymentAfterClose` đỏ. Ca cũ chỉ đổi phần gửi body `countedCash`.
- `ConcurrencyTests.cs`, `OutboxTests.cs`, `AuthTests.cs`: pass, **không đổi** so với `main` (`git diff main --stat` rỗng).

## Kiểm tra thêm của lát B

- `verify-slice-b.log`, `b-latency.log`: không chứa mật khẩu Postgres, mật khẩu seed, khoá JWT hay chuỗi JWT (so với giá trị trong `.env`, không in ra).
- Log container `cashier`, `gateway`, `ordering` sau toàn bộ e2e: không có giá trị `Idempotency-Key` hay `Bearer ey…`.
- Cổng gRPC 8092 không publish ra host; Playwright chỉ đi `http://localhost:5173`.

## Điều chưa kiểm được

- Tiêu chí 6, 7, 10 và phần ca đêm của 9 thuộc lát C/D.
- Đơn `Paid` không có bút toán khi mất phản hồi gRPC **và** thu ngân đóng form thay vì bấm lại (plan §5 nguy cơ #1) — chưa có cơ chế phát hiện; để lát D đối chiếu `OrderPaid` với `payments`.
- Độ trễ đo trên một máy dev (Docker Desktop, Windows), một người dùng mỗi vai trò; chưa đo khi tải cao.
- vitest trên máy này khởi động chậm (44–56s môi trường, sát ngưỡng 60s của worker) — có thể lại đỏ giả khi máy bận. Chưa đổi cấu hình vì ngoài phạm vi plan.
- Chưa chạy trên CI của nhánh này (cần push — chờ người điều phối).

---

# Verify — 01-260925-fnb-pos-core (lát A)
Ngày: 2026-09-25 17:24   Commit: cc7c886   Nhánh: `feat/01-260925-fnb-pos-core-slice-a`

Phạm vi: **lát A** của spec §11 (nền + đặt món & bếp). Intent có 12 tiêu chí; lát A chịu tiêu chí
1, 2, 3 và phần gán ca của 9. Các tiêu chí còn lại thuộc lát B/C/D và **chưa** được kiểm ở đây.

## Cổng tự động

`npm run sdlc:verify -- --all --e2e` trên stack compose đang chạy — log đầy đủ: `verify-slice-a.log`.

| Cổng | Workspace | Kết quả |
|------|-----------|---------|
| format (`dotnet format --verify-no-changes`) | backend | ✅ |
| build (`-warnaserror`) | backend | ✅ |
| test (unit + gateway) | backend | ✅ 38/38 |
| e2e (Testcontainers Postgres/Kafka/Redis) | backend | ✅ 29/29 |
| lint (ratchet) | web | ✅ |
| build (`tsc -b && vite build`) | web | ✅ |
| test (vitest) | web | ✅ 11/11 |
| e2e (Playwright, qua nginx → gateway) | web | ✅ 3/3 |
| eval quy trình (`node docs/evals/run.mjs`) | — | ✅ 8/8 |

Tổng **81/81 test xanh**.

## Tiêu chí chấp nhận

| # | Tiêu chí | Bằng chứng | ✅/❌ |
|---|----------|------------|-------|
| 1 | Một lệnh dựng hệ thống, một lệnh nạp dữ liệu mẫu | `a-seed.log`: `down -v` → `up --wait` 8 container healthy sau 38s; seed 2 lần cùng kết quả. `a-compose-ps.png`. `SeederTests.SeedTwice_SameSummary_NoDuplicates` | ✅ phần lát A — nguyên liệu (lát C) và đơn lịch sử đã thanh toán (lát B) chưa có trong seed |
| 2 | Đơn 3 món → bếp ≤ 2s, không tải lại | `web/e2e/order-to-kitchen.spec.ts`; `a-latency.log`: 5 lần đo, 298–1253ms. `a-kitchen-board.png` | ✅ |
| 3 | Bếp "Xong" → POS đổi trạng thái ≤ 2s | cùng spec; `a-latency.log`: 132–267ms. `a-pos-order.png` | ✅ |
| 4 | Bill đơn 20 món ≤ 2s | — | ⏳ lát B |
| 5 | Gửi lại lệnh thanh toán không tạo bút toán thứ hai | — | ⏳ lát B |
| 6 | Bán cà phê sữa → trừ đúng định lượng | — | ⏳ lát C |
| 7 | Nguyên liệu dưới ngưỡng → cảnh báo hết hàng trên POS | Chỉ phần hiển thị: badge "Hết" + nút khoá khi `isAvailable=false` (`a-pos-order.png`, món Bánh flan). Nguồn sự kiện từ inventory chưa có | ⏳ lát C |
| 8 | Đóng ca: số đơn, doanh thu, tiền dự kiến, tiền đếm, phần lệch | Chỉ mở/đóng ca (`ShiftTests.cs`, `a-no-open-shift.png`); đối chiếu tiền chưa có | ⏳ lát B |
| 9 | Đơn 01:30 thuộc ca mở 18:00 | Phần gán ca: đơn lấy `shift_id` từ hình chiếu ca qua Kafka, không từ ngày — `ShiftProjectionTests.OrderCreation_AfterShiftOpenedEvent_Succeeds`, `ShiftClosedArrivingBeforeOpened_ShiftStaysClosed`. Ca vắt qua nửa đêm trong báo cáo chưa kiểm | ✅ phần lát A · ⏳ lát D |
| 10 | Filter/sort/paging nằm trong URL | — | ⏳ lát D |
| 11 | README có sơ đồ + bảng đối chiếu JD | `README.md` mục "Kiến trúc", "Đối chiếu JD" (dòng lát A đã điền, B/C/D đánh dấu) | ✅ phần lát A · rà tay ở lát D |
| 12 | Bằng chứng cho 4 luồng: đặt món, thanh toán, trừ kho, chốt ca | Đặt món: 8 ảnh `a-*.png` + `a-latency.log`. Thanh toán, trừ kho, chốt ca: chưa có | ⏳ lát B/C/D |

### Trạng thái đã chụp (lát A)

| Trạng thái | Ảnh |
|------------|-----|
| Lỗi đăng nhập (câu của server) | `a-login-error.png` |
| Rỗng: thực đơn trống | `a-pos-empty-menu.png` (chặn `/api/menu` trả `[]` vì DB demo đã seed) |
| Rỗng: chưa mở ca | `a-no-open-shift.png` (đóng ca thật) |
| Thành công: POS có đơn nháp + đơn đang mở | `a-pos-order.png` |
| Thành công: bảng bếp 3 cột | `a-kitchen-board.png` |
| Lỗi: mất kết nối, nút ghi bị khoá | `a-offline-banner.png` |
| Lỗi: 409 hai người sửa cùng đơn | `a-conflict-409.png` (409 thật: một tab bị chặn SignalR nên giữ phiên bản cũ) |
| Hạ tầng | `a-compose-ps.png` |

## Test hồi quy (`requiredTests`)

- `Ordering.IntegrationTests/ConcurrencyTests.cs`: pass (5/5). Đột biến khi build: bỏ `IsRowVersion` → 3 test đỏ.
- `Ordering.IntegrationTests/OutboxTests.cs`: pass. Đột biến: publisher bỏ dòng cũ → đỏ.
- `Cashier.IntegrationTests/ShiftTests.cs`: pass. Đột biến: bỏ index hoặc bỏ `NULLS NOT DISTINCT` → 10/10 request mở ca đều 201, đỏ.
- `Identity.IntegrationTests/AuthTests.cs`: pass (`Login_TenFailures_Returns423`, `Refresh_ReusedToken_RevokesFamily`).

## Kiểm tra thêm của lát A (plan §0)

- Sau `docker compose up`, `git status --porcelain -uall | grep -E '(^|/)(bin|obj|dist|node_modules)/'` rỗng.
- Log bằng chứng đã soát: không chứa mật khẩu Postgres, mật khẩu seed, khoá JWT hay chuỗi JWT.
- Không gọi thẳng dịch vụ: Playwright đi `http://localhost:5173` → nginx → gateway 8080; cổng 8081–8083 không publish ra host.

## Điều chưa kiểm được

- Tiêu chí 4–8, 10, 12 và phần ca đêm của 9 thuộc lát B/C/D — chưa có mã, chưa kiểm.
- Độ trễ đo trên một máy dev (Docker Desktop, Windows), một người dùng mỗi vai trò; chưa đo khi tải cao.
- Chưa chạy trên CI (`.github/workflows/ci.yml` chưa được kích hoạt bằng push — cần người điều phối cho phép push).
- 4 devDependency web ngoài danh sách đã duyệt (`@types/react`, `@types/react-dom`, `@types/node`, `@testing-library/dom`) đã được người điều phối duyệt lúc ship (2026-09-25) — xem plan §4.
