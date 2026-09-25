import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { getFreshAccessToken } from './apiClient'
import { queryKeys } from './queryKeys'

const ORDER_EVENTS = ['orderCreated', 'orderUpdated', 'orderItemStatusChanged', 'orderCancelled'] as const

/**
 * Kết nối hub `/hubs/orders`, vào group của ca; mọi sự kiện đơn → invalidate danh sách đơn
 * (POS) và bảng bếp. Token đi qua query `access_token` (SignalR tự gắn từ accessTokenFactory).
 */
export function connectOrdersHub(shiftId: string, queryClient: QueryClient): () => void {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/orders', { accessTokenFactory: getFreshAccessToken })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })
    void queryClient.invalidateQueries({ queryKey: queryKeys.kitchen.orders })
  }
  for (const event of ORDER_EVENTS) connection.on(event, refresh)
  // Kết nối lại = vào lại group (server quên group của connection cũ) + bù sự kiện đã lỡ.
  connection.onreconnected(() => {
    void connection.invoke('JoinShift', shiftId)
    refresh()
  })

  const started = connection
    .start()
    .then(() => connection.invoke('JoinShift', shiftId))
    .catch(() => {
      // Hub chưa sẵn sàng — dữ liệu vẫn tải qua HTTP; mount lại sẽ thử lại.
    })

  return () => {
    void started.finally(() => connection.stop())
  }
}

export function useOrdersHub(shiftId: string | undefined) {
  const queryClient = useQueryClient()
  useEffect(() => (shiftId ? connectOrdersHub(shiftId, queryClient) : undefined), [shiftId, queryClient])
}
