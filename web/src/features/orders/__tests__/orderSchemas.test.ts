import { expect, test } from 'vitest'
import { DraftOrderSchema, MenuItemSchema, OrderSchema } from '../orderSchemas'

const order = {
  id: 'o1',
  code: 12,
  shiftId: 's1',
  status: 'Open',
  total: 90000,
  createdAt: '2026-09-25T02:00:00Z',
  paidAt: null,
  version: 812,
  items: [
    { id: 'i1', menuItemId: 'm1', name: 'Cà phê sữa đá', unitPrice: 30000, qty: 3, status: 'Pending' },
    { id: 'i2', menuItemId: 'm2', name: 'Trà đào', unitPrice: 35000, qty: 1, status: 'Cancelled' },
  ],
}

test('orderSchemas_ValidOrder_Parses', () => {
  expect(OrderSchema.parse(order)).toEqual(order)
  expect(MenuItemSchema.parse({ id: 'm1', name: 'Bạc xỉu', price: 32000, isAvailable: false }).isAvailable).toBe(false)
})

test('orderSchemas_UnknownItemStatus_Rejected', () => {
  const bad = { ...order, items: [{ ...order.items[0], status: 'Served' }] }
  expect(OrderSchema.safeParse(bad).success).toBe(false)
})

test('orderSchemas_MissingVersion_Rejected', () => {
  expect(OrderSchema.safeParse({ ...order, version: undefined }).success).toBe(false)
})

test('draftOrder_Validation', () => {
  expect(DraftOrderSchema.safeParse({ items: [{ menuItemId: 'm1', qty: 2 }] }).success).toBe(true)
  expect(DraftOrderSchema.safeParse({ items: [] }).success).toBe(false)
  expect(DraftOrderSchema.safeParse({ items: [{ menuItemId: 'm1', qty: 100 }] }).success).toBe(false)
  expect(DraftOrderSchema.safeParse({ items: [{ menuItemId: 'm1', qty: 0 }] }).success).toBe(false)
  const tooMany = Array.from({ length: 101 }, (_, i) => ({ menuItemId: `m${i}`, qty: 1 }))
  expect(DraftOrderSchema.safeParse({ items: tooMany }).success).toBe(false)
})
