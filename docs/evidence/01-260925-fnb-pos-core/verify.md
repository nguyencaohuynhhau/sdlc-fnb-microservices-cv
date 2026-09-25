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
- 4 devDependency web ngoài danh sách đã duyệt (`@types/react`, `@types/react-dom`, `@types/node`, `@testing-library/dom`) vẫn chờ người điều phối duyệt.
