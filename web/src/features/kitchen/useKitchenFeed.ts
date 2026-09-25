import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from '@/lib/apiClient'
import { queryKeys } from '@/lib/queryKeys'
import { useOrdersHub } from '@/lib/signalr'
import { OrderSchema, type Order } from '@/features/orders/orderSchemas'
import { useOrderingShift } from '@/features/shift/useShift'

/** Đơn `Open` của ca hiện hành (cũ nhất trước), cập nhật trực tiếp qua SignalR. */
export function useKitchenFeed() {
  const shift = useOrderingShift()
  useOrdersHub(shift.data?.shiftId)
  return useQuery({
    queryKey: queryKeys.kitchen.orders,
    queryFn: async () => z.array(OrderSchema).parse(await api('/api/kitchen/orders')),
  })
}

export function useSetItemStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ order, itemId, status }: { order: Order; itemId: string; status: 'Preparing' | 'Done' }) =>
      OrderSchema.parse(
        await api(`/api/orders/${order.id}/items/${itemId}/status`, {
          method: 'PATCH',
          body: { status },
          ifMatch: order.version,
        }),
      ),
    onSuccess: (updated) => {
      // Ghi ngay phiên bản mới vào cache để lần bấm kế tiếp gửi đúng If-Match, không chờ refetch.
      queryClient.setQueryData<Order[]>(queryKeys.kitchen.orders, (list) =>
        list?.map((o) => (o.id === updated.id ? updated : o)),
      )
      void queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })
    },
  })
}
