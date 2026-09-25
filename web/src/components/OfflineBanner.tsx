import { useIsOnline } from '@/lib/online'

/** Banner đỏ khi mất mạng hoặc /healthz lỗi. Có mạng lại: tự ẩn, online.ts tự invalidate query. */
export function OfflineBanner() {
  const online = useIsOnline()
  if (online) return null
  return (
    <div role="alert" className="bg-destructive fixed inset-x-0 top-0 z-50 px-4 py-2 text-center text-sm font-medium text-white">
      Mất kết nối tới hệ thống
    </div>
  )
}
