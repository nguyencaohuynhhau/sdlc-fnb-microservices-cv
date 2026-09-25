import { appendFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import { availableMenuItems, ensureShiftOpen, loginAs } from './helpers'

const LATENCY_LOG = fileURLToPath(new URL('../../docs/evidence/01-260925-fnb-pos-core/a-latency.log', import.meta.url))
const BUDGET_MS = 2000

/** Chờ tới khi trang đã vào group SignalR của ca (JoinShift trả kết quả qua WebSocket). */
function hubJoined(page: Page) {
  return page
    .waitForEvent('websocket', (ws) => ws.url().includes('/hubs/orders'))
    .then((ws) => ws.waitForEvent('framereceived', (f) => String(f.payload).includes('"type":3')))
}

test('POS "Gửi bếp" → bếp thấy đơn < 2s; bếp "Xong" → POS thấy < 2s', async ({ browser }) => {
  const pos = await (await browser.newContext()).newPage()
  const kitchen = await (await browser.newContext()).newPage()

  await loginAs(pos, 'cashier')
  await ensureShiftOpen(pos)

  const kitchenJoined = hubJoined(kitchen)
  await loginAs(kitchen, 'kitchen')
  await kitchenJoined

  // Ba món khác nhau → ba dòng trong đơn.
  const menu = availableMenuItems(pos)
  for (let i = 0; i < 3; i++) await menu.nth(i).click()
  const send = pos.getByRole('button', { name: 'Gửi bếp' })
  await expect(send).toBeEnabled()

  const created = pos.waitForResponse((r) => r.url().endsWith('/api/orders') && r.request().method() === 'POST')
  const t0 = Date.now()
  await send.click()
  const response = await created
  expect(response.status()).toBe(201)
  const { code } = (await response.json()) as { code: number }

  const pendingCard = kitchen.getByTestId('kitchen-col-Pending').getByTestId(`kitchen-order-${code}`)
  await expect(pendingCard).toBeVisible({ timeout: Math.max(1, BUDGET_MS - (Date.now() - t0)) })
  const toKitchenMs = Date.now() - t0

  // Bếp: Chờ → Đang làm cho cả ba món.
  const start = pendingCard.getByRole('button', { name: 'Bắt đầu làm' })
  for (let left = 3; left > 0; left--) {
    await expect(start).toHaveCount(left)
    await start.first().click()
  }
  await expect(pendingCard).toBeHidden()

  // Đang làm → Xong; món cuối cùng là mốc đo độ trễ về POS.
  const done = kitchen.getByTestId('kitchen-col-Preparing').getByTestId(`kitchen-order-${code}`).getByRole('button', { name: 'Xong' })
  for (let left = 3; left > 1; left--) {
    await expect(done).toHaveCount(left)
    await done.first().click()
  }
  await expect(done).toHaveCount(1)
  const t1 = Date.now()
  await done.click()

  const posDoneBadges = pos.getByTestId(`pos-order-${code}`).getByText('Xong', { exact: true })
  await expect(posDoneBadges).toHaveCount(3, { timeout: BUDGET_MS })
  const toPosMs = Date.now() - t1

  mkdirSync(dirname(LATENCY_LOG), { recursive: true })
  appendFileSync(
    LATENCY_LOG,
    `${new Date().toISOString()} order #${code}: POS→kitchen ${toKitchenMs}ms, kitchen Done→POS ${toPosMs}ms (budget ${BUDGET_MS}ms)\n`,
  )
  expect(toKitchenMs).toBeLessThan(BUDGET_MS)
  expect(toPosMs).toBeLessThan(BUDGET_MS)
})
