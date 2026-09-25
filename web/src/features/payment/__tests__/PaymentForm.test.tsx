import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { auth } from '@/lib/auth'
import type { Order } from '@/features/orders/orderSchemas'
import { PaymentForm } from '../PaymentForm'
import { usePayOrder } from '../usePayOrder'

const order: Order = {
  id: 'o1',
  code: 7,
  shiftId: 's1',
  status: 'Open',
  total: 90000,
  createdAt: '2026-09-25T02:00:00Z',
  paidAt: null,
  version: 3,
  items: [{ id: 'i1', menuItemId: 'm1', name: 'Cà phê sữa đá', unitPrice: 30000, qty: 3, status: 'Done' }],
}

function Harness() {
  const pay = usePayOrder()
  const [open, setOpen] = useState(true)
  return open ? (
    <PaymentForm order={order} pay={pay} online onClose={() => setOpen(false)} />
  ) : (
    <button onClick={() => setOpen(true)}>Mở lại</button>
  )
}

beforeEach(() => {
  auth.setSession({ accessToken: 'a', refreshToken: 'r', role: 'Cashier', expiresIn: 3600 })
})

afterEach(() => {
  vi.unstubAllGlobals()
  auth.clear()
})

test('paymentForm_RetryAfterError_ReusesKey_ReopenMintsNewKey', async () => {
  const keys: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (_url: string, init: RequestInit = {}) => {
      keys.push((init.headers as Record<string, string>)['Idempotency-Key'])
      return new Response(JSON.stringify({ status: 503, detail: 'Hệ thống đơn hàng đang bận' }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
  render(
    <QueryClientProvider client={new QueryClient()}>
      <Harness />
    </QueryClientProvider>,
  )
  const confirm = () => screen.getByRole('button', { name: /Xác nhận thu/ })

  fireEvent.click(confirm())
  await waitFor(() => expect(keys).toHaveLength(1))
  await waitFor(() => expect((confirm() as HTMLButtonElement).disabled).toBe(false))
  fireEvent.click(confirm())
  await waitFor(() => expect(keys).toHaveLength(2))

  fireEvent.click(screen.getByRole('button', { name: 'Đóng' }))
  fireEvent.click(screen.getByRole('button', { name: 'Mở lại' }))
  await waitFor(() => expect((confirm() as HTMLButtonElement).disabled).toBe(false))
  fireEvent.click(confirm())
  await waitFor(() => expect(keys).toHaveLength(3))

  // Bấm lại trong cùng một lần mở form = cùng một lần thu → cùng khoá, server không thu hai lần.
  expect(keys[0]).toMatch(/^[0-9a-f-]{36}$/)
  expect(keys[1]).toBe(keys[0])
  expect(keys[2]).not.toBe(keys[0])
})
