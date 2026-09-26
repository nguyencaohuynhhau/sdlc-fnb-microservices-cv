import { appendFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import { availableMenuItems, ensureShiftOpen, hubJoined, loginAs } from './helpers'

const LATENCY_LOG = fileURLToPath(new URL('../../docs/evidence/01-260925-fnb-pos-core/b-latency.log', import.meta.url))
const BUDGET_MS = 2000

/** Bấm vòng tròn `lines` món đầu còn hàng tới đủ `portions` phần, "Gửi bếp", trả mã đơn. */
async function sendOrder(pos: Page, lines: number, portions: number) {
  const menu = availableMenuItems(pos)
  const n = Math.min(lines, await menu.count())
  for (let i = 0; i < portions; i++) await menu.nth(i % n).click()
  const created = pos.waitForResponse((r) => r.url().endsWith('/api/orders') && r.request().method() === 'POST')
  await pos.getByRole('button', { name: 'Gửi bếp' }).click()
  const response = await created
  expect(response.status()).toBe(201)
  return ((await response.json()) as { code: number }).code
}

/** "Thu tiền" trên thẻ đơn → chọn phương thức → "Xác nhận thu"; trả số ms tới khi biên nhận hiện. */
async function pay(pos: Page, code: number, method: 'Tiền mặt' | 'Chuyển khoản') {
  const card = pos.getByTestId(`pos-order-${code}`)
  await card.getByRole('button', { name: 'Thu tiền' }).click()
  await card.getByRole('button', { name: method }).click()
  const t0 = Date.now()
  await card.getByRole('button', { name: /^Xác nhận thu/ }).click()
  await expect(pos.getByTestId('receipt')).toContainText(`Đã thu đơn #${code}`, { timeout: 10_000 })
  return Date.now() - t0
}

test('đơn 20 phần / 10 dòng: "Xác nhận thu" → biên nhận < 2s; thu trước khi bếp xong thì đơn vẫn ở lại', async ({ page }) => {
  await loginAs(page, 'cashier')
  await ensureShiftOpen(page)

  const code = await sendOrder(page, 10, 20)
  const card = page.getByTestId(`pos-order-${code}`)
  await expect(card.getByText(/^2 × /)).toHaveCount(10)

  const ms = await pay(page, code, 'Tiền mặt')
  mkdirSync(dirname(LATENCY_LOG), { recursive: true })
  appendFileSync(
    LATENCY_LOG,
    `${new Date().toISOString()} order #${code}: 20 portions / 10 lines, "Xác nhận thu"→receipt ${ms}ms (budget ${BUDGET_MS}ms)\n`,
  )
  expect(ms).toBeLessThan(BUDGET_MS)

  // Bếp chưa làm xong → đơn vẫn trong danh sách, gắn "Đã thu", hết nút thu/huỷ.
  await expect(card.getByText('Đã thu', { exact: true })).toBeVisible()
  await expect(card.getByRole('button', { name: 'Thu tiền' })).toHaveCount(0)
  await expect(card.getByRole('button', { name: 'Huỷ đơn' })).toHaveCount(0)
})

test('luồng đầy đủ: 3 món → bếp "Xong" → thu tiền mặt → đóng ca, nhập tiền đếm, thấy tổng kết', async ({ browser }) => {
  const pos = await (await browser.newContext()).newPage()
  const kitchen = await (await browser.newContext()).newPage()
  await loginAs(pos, 'cashier')
  await ensureShiftOpen(pos)
  const kitchenJoined = hubJoined(kitchen)
  await loginAs(kitchen, 'kitchen')
  await kitchenJoined

  const code = await sendOrder(pos, 3, 3)
  const pending = kitchen.getByTestId('kitchen-col-Pending').getByTestId(`kitchen-order-${code}`)
  await expect(pending).toBeVisible({ timeout: BUDGET_MS })
  const start = pending.getByRole('button', { name: 'Bắt đầu làm' })
  for (let left = 3; left > 0; left--) {
    await expect(start).toHaveCount(left)
    await start.first().click()
  }
  const done = kitchen.getByTestId('kitchen-col-Preparing').getByTestId(`kitchen-order-${code}`).getByRole('button', { name: 'Xong' })
  for (let left = 3; left > 0; left--) {
    await expect(done).toHaveCount(left)
    await done.first().click()
  }
  await expect(pos.getByTestId(`pos-order-${code}`).getByText('Xong', { exact: true })).toHaveCount(3)

  await pay(pos, code, 'Tiền mặt')
  // Đã thu + bếp xong → rời cả POS lẫn bảng bếp; biên nhận vẫn còn tới khi bấm "Đơn mới".
  await expect(pos.getByTestId(`pos-order-${code}`)).toBeHidden()
  await expect(kitchen.getByTestId(`kitchen-order-${code}`)).toHaveCount(0)
  await pos.getByRole('button', { name: 'Đơn mới' }).click()
  await expect(pos.getByTestId('receipt')).toBeHidden()

  // Đóng ca: đếm mù → server trả dự kiến + lệch → hiện tổng kết.
  await pos.getByRole('button', { name: 'Đóng ca' }).click()
  await pos.getByLabel('Tiền mặt đếm được trong két').fill('1000000')
  const closed = pos.waitForResponse((r) => /\/api\/shifts\/[^/]+\/close$/.test(r.url()))
  await pos.getByRole('button', { name: 'Xác nhận đóng ca' }).click()
  const response = await closed
  expect(response.status()).toBe(200)
  const summary = (await response.json()) as { countedCash: number; expectedCash: number; variance: number; orderCount: number }
  expect(summary.countedCash).toBe(1_000_000)
  expect(summary.variance).toBe(summary.countedCash - summary.expectedCash)
  expect(summary.orderCount).toBeGreaterThanOrEqual(1)

  const panel = pos.getByTestId('shift-summary')
  const expectedText = await pos.evaluate(
    (n) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n),
    summary.expectedCash,
  )
  await expect(panel).toContainText(expectedText)
  await expect(panel).toContainText('Lệch:')
  await expect(pos.getByRole('button', { name: 'Mở ca' })).toBeVisible()
  await panel.getByRole('button', { name: 'Xong' }).click()
  await expect(panel).toBeHidden()
})
