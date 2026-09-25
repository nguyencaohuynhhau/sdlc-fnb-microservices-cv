import { z } from 'zod'

// Hình dạng theo docs/api/endpoints.md (lát A). Parse mọi response để lệch hợp đồng lộ ra ngay.

export const MenuItemSchema = z.object({
  id: z.string(),
  name: z.string(),
  price: z.number(),
  isAvailable: z.boolean(),
})
export type MenuItem = z.infer<typeof MenuItemSchema>

export const OrderItemStatusSchema = z.enum(['Pending', 'Preparing', 'Done', 'Cancelled'])
export type OrderItemStatus = z.infer<typeof OrderItemStatusSchema>

export const OrderItemSchema = z.object({
  id: z.string(),
  menuItemId: z.string(),
  name: z.string(),
  unitPrice: z.number(),
  qty: z.number().int(),
  status: OrderItemStatusSchema,
})
export type OrderItem = z.infer<typeof OrderItemSchema>

export const OrderSchema = z.object({
  id: z.string(),
  code: z.number().int(),
  shiftId: z.string(),
  status: z.enum(['Open', 'Paid', 'Cancelled']),
  total: z.number(),
  createdAt: z.string(),
  paidAt: z.string().nullable(),
  version: z.number().int(),
  items: z.array(OrderItemSchema),
})
export type Order = z.infer<typeof OrderSchema>

/** Đơn nháp gửi bếp: 1–100 dòng, mỗi dòng 1–99 phần (khớp validate phía server). */
export const DraftOrderSchema = z.object({
  items: z
    .array(
      z.object({
        menuItemId: z.string().min(1),
        qty: z.number().int().min(1, 'Số lượng tối thiểu là 1.').max(99, 'Mỗi món tối đa 99 phần.'),
      }),
    )
    .min(1, 'Chọn ít nhất một món.')
    .max(100, 'Một đơn tối đa 100 dòng.'),
})
export type DraftOrder = z.infer<typeof DraftOrderSchema>

export const itemStatusLabel: Record<OrderItemStatus, string> = {
  Pending: 'Chờ',
  Preparing: 'Đang làm',
  Done: 'Xong',
  Cancelled: 'Đã huỷ',
}
