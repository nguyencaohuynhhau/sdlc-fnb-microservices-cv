import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from '@/lib/apiClient'
import { queryKeys } from '@/lib/queryKeys'

const ShiftSchema = z.object({
  id: z.string(),
  openedBy: z.string(),
  openedAt: z.string(),
  closedAt: z.string().nullable(),
  openingFloat: z.number(),
})
export type Shift = z.infer<typeof ShiftSchema>

/** Phản hồi đóng ca: đối chiếu tiền trong két. `variance` = đếm được − dự kiến (âm = thiếu). */
export const ShiftSummarySchema = ShiftSchema.extend({
  closedAt: z.string(),
  countedCash: z.number(),
  expectedCash: z.number(),
  variance: z.number(),
  orderCount: z.number().int(),
  revenue: z.number(),
  cashTotal: z.number(),
  transferTotal: z.number(),
})
export type ShiftSummary = z.infer<typeof ShiftSummarySchema>

const CurrentShiftSchema = z.object({ shiftId: z.string(), openedAt: z.string() })

/** Ca đang mở theo cashier (204 → null). */
export function useCurrentShift() {
  return useQuery({
    queryKey: queryKeys.shift.current,
    queryFn: async () => ShiftSchema.nullable().parse(await api('/api/shifts/current')),
  })
}

/**
 * Ca mà ordering đã biết (projection qua Kafka, nhất quán cuối). Khi cashier đã có ca mà
 * ordering chưa thấy → poll dày để bật "Gửi bếp" ngay khi đồng bộ xong (plan §5 nguy cơ #6).
 */
export function useOrderingShift(expectedShiftId?: string) {
  return useQuery({
    queryKey: queryKeys.orders.currentShift,
    queryFn: async () => CurrentShiftSchema.nullable().parse(await api('/api/orders/current-shift')),
    refetchInterval: (q) => (expectedShiftId && q.state.data?.shiftId !== expectedShiftId ? 500 : 10_000),
  })
}

export function useOpenShift() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () =>
      ShiftSchema.parse(await api('/api/shifts/open', { method: 'POST', body: { openingFloat: 0 } })),
    onSuccess: (shift) => {
      queryClient.setQueryData(queryKeys.shift.current, shift)
      void queryClient.invalidateQueries({ queryKey: queryKeys.orders.currentShift })
    },
  })
}

export function useCloseShift() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ shiftId, countedCash }: { shiftId: string; countedCash: number }) =>
      ShiftSummarySchema.parse(await api(`/api/shifts/${shiftId}/close`, { method: 'POST', body: { countedCash } })),
    onSuccess: () => {
      queryClient.setQueryData(queryKeys.shift.current, null)
      void queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })
    },
  })
}
