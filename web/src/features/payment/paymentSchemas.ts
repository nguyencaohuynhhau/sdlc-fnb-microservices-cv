import { z } from 'zod'

export const PaymentMethodSchema = z.enum(['Cash', 'Transfer'])
export type PaymentMethod = z.infer<typeof PaymentMethodSchema>

/** Phản hồi `POST /api/payments`. Gửi lại cùng `Idempotency-Key` nhận lại đúng body này. */
export const PaymentReceiptSchema = z.object({
  paymentId: z.string(),
  orderId: z.string(),
  shiftId: z.string(),
  amount: z.number(),
  method: PaymentMethodSchema,
  paidAt: z.string(),
})
export type PaymentReceipt = z.infer<typeof PaymentReceiptSchema>

export const methodLabel: Record<PaymentMethod, string> = {
  Cash: 'Tiền mặt',
  Transfer: 'Chuyển khoản',
}
