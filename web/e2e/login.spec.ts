import { expect, test } from '@playwright/test'

test('đăng nhập sai → đúng câu thông báo của server', async ({ page }) => {
  await page.goto('/login')
  // Dùng tài khoản không tồn tại: server trả cùng câu với sai mật khẩu, và không làm khoá
  // tài khoản seed (10 lần sai → khoá 15 phút) khi chạy lại nhiều lần.
  await page.getByLabel('Tên đăng nhập').fill('khong-ton-tai')
  await page.getByLabel('Mật khẩu').fill('sai-mat-khau-123')
  await page.getByRole('button', { name: 'Đăng nhập' }).click()

  await expect(page.getByText('Tên đăng nhập hoặc mật khẩu không đúng.')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})
