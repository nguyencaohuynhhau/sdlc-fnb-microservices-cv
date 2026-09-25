import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/apiClient'
import { queryKeys } from '@/lib/queryKeys'
import type { Order } from '@/features/orders/orderSchemas'
import { PaymentReceiptSchema, type PaymentMethod } from './paymentSchemas'

export type PayOrderInput = { order: Order; method: PaymentMethod; idempotencyKey: string }

/**
 * Thu tiền một đơn. Khoá do form cấp lúc mở: bấm lại sau lỗi mạng/503 gửi đúng khoá cũ,
 * server trả lại kết quả lần trước thay vì thu lần hai. 409 → toast + tải lại (queryClient).
 */
export function usePayOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ order, method, idempotencyKey }: PayOrderInput) =>
      PaymentReceiptSchema.parse(
        await api('/api/payments', {
          method: 'POST',
          body: { orderId: order.id, expectedTotal: order.total, method },
          idempotencyKey,
        }),
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.kitchen.orders })
    },
  })
}
