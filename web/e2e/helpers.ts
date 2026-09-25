import { expect, type Page } from '@playwright/test'

export async function loginAs(page: Page, username: 'cashier' | 'kitchen' | 'owner') {
  const password = process.env.SEED_PASSWORD
  if (!password) throw new Error('Cần biến môi trường SEED_PASSWORD (mật khẩu seeder).')
  await page.goto('/login')
  await page.getByLabel('Tên đăng nhập').fill(username)
  await page.getByLabel('Mật khẩu').fill(password)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await expect(page).toHaveURL(username === 'kitchen' ? /\/kitchen$/ : /\/pos$/)
}

/** Mở ca nếu chưa có, rồi chờ ordering đồng bộ ca (hết "Đang đồng bộ ca…"). */
export async function ensureShiftOpen(page: Page) {
  const bar = page.getByTestId('shift-bar')
  await expect(bar).not.toContainText('Đang tải ca…')
  const open = page.getByRole('button', { name: 'Mở ca' })
  if (await open.isVisible()) await open.click()
  await expect(page.getByRole('button', { name: 'Đóng ca' })).toBeVisible()
  await expect(bar).not.toContainText('Đang đồng bộ ca…', { timeout: 10_000 })
}

export const availableMenuItems = (page: Page) =>
  page.getByRole('list', { name: 'Thực đơn' }).getByRole('button', { disabled: false })
