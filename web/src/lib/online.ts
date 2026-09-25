import { onlineManager, type QueryClient } from '@tanstack/react-query'
import { useSyncExternalStore } from 'react'
import { api } from './apiClient'

/** Ping `/healthz`: 200 → online; lỗi mạng / 503 → offline (banner đỏ, khoá nút ghi). */
export async function pingHealthz(): Promise<void> {
  try {
    await api('/healthz', { auth: false })
    onlineManager.setOnline(true)
  } catch {
    onlineManager.setOnline(false)
  }
}

/** Gọi một lần lúc khởi động app. Trả hàm dừng. */
export function startOnlineWatch(queryClient: QueryClient, intervalMs = 10_000): () => void {
  void pingHealthz()
  const timer = setInterval(() => void pingHealthz(), intervalMs)
  // Có mạng lại → tải lại mọi dữ liệu, không cần F5.
  const unsubscribe = onlineManager.subscribe((online) => {
    if (online) void queryClient.invalidateQueries()
  })
  return () => {
    clearInterval(timer)
    unsubscribe()
  }
}

export function useIsOnline(): boolean {
  return useSyncExternalStore(
    (onChange) => onlineManager.subscribe(onChange),
    () => onlineManager.isOnline(),
  )
}
