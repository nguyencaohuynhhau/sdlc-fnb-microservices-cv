import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from '@/lib/apiClient'
import { queryKeys } from '@/lib/queryKeys'
import { OrderSchema, type DraftOrder, type Order } from './orderSchemas'

const parseOrder = async (p: Promise<unknown>) => OrderSchema.parse(await p)

/** Đơn đang mở của ca hiện hành (server lọc theo ca), mới nhất trước. */
export function useOpenOrders(shiftId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.orders.list(shiftId ?? ''),
    queryFn: async () => z.array(OrderSchema).parse(await api('/api/orders?status=Open')),
    enabled: !!shiftId,
  })
}

function useInvalidateOrders() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.kitchen.orders })
  }
}

export function useCreateOrder() {
  const onSuccess = useInvalidateOrders()
  return useMutation({
    mutationFn: (draft: DraftOrder) => parseOrder(api('/api/orders', { method: 'POST', body: draft })),
    onSuccess,
  })
}

export function useCancelItem() {
  const onSuccess = useInvalidateOrders()
  return useMutation({
    mutationFn: ({ order, itemId }: { order: Order; itemId: string }) =>
      parseOrder(api(`/api/orders/${order.id}/items/${itemId}`, { method: 'DELETE', ifMatch: order.version })),
    onSuccess,
  })
}

export function useCancelOrder() {
  const onSuccess = useInvalidateOrders()
  return useMutation({
    mutationFn: (order: Order) =>
      parseOrder(api(`/api/orders/${order.id}/cancel`, { method: 'POST', ifMatch: order.version })),
    onSuccess,
  })
}
