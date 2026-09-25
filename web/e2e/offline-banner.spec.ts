import { expect, test } from '@playwright/test'
import { availableMenuItems, ensureShiftOpen, loginAs } from './helpers'

test('mất mạng → banner đỏ + khoá "Gửi bếp"; có mạng lại → tự hồi phục, không reload', async ({ page, context }) => {
  await loginAs(page, 'cashier')
  await ensureShiftOpen(page)
  await availableMenuItems(page).first().click()
  const send = page.getByRole('button', { name: 'Gửi bếp' })
  await expect(send).toBeEnabled()

  // Reload tạo document mới → performance.timeOrigin đổi.
  const timeOrigin = await page.evaluate(() => performance.timeOrigin)

  await context.route('**/healthz', (route) => route.abort())
  await context.setOffline(true)
  const banner = page.getByText('Mất kết nối tới hệ thống')
  await expect(banner).toBeVisible({ timeout: 15_000 })
  await expect(send).toBeDisabled()

  await context.unroute('**/healthz')
  await context.setOffline(false)
  await expect(banner).toBeHidden({ timeout: 15_000 })
  await expect(send).toBeEnabled()

  // Cùng một document (timeOrigin không đổi) = không reload.
  expect(await page.evaluate(() => performance.timeOrigin)).toBe(timeOrigin)
})
