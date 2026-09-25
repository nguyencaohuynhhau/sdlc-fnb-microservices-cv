import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useIsOnline } from '@/lib/online'
import { CloseShiftForm } from './CloseShiftForm'
import { useCurrentShift, useOpenShift, useOrderingShift } from './useShift'

const timeOf = (iso: string) => new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })

export function ShiftBar() {
  const shift = useCurrentShift()
  const orderingShift = useOrderingShift(shift.data?.id)
  const open = useOpenShift()
  // Giữ ở thanh ca: đóng xong ca biến mất nhưng bảng tổng kết vẫn phải còn tới khi bấm "Xong".
  const [closingId, setClosingId] = useState<string | null>(null)
  const online = useIsOnline()
  const current = shift.data
  const syncing = !!current && orderingShift.data?.shiftId !== current.id

  return (
    <>
    <div className="bg-muted/50 flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2" data-testid="shift-bar">
      {shift.isPending ? (
        <span className="text-muted-foreground">Đang tải ca…</span>
      ) : current ? (
        <span className="flex items-center gap-2">
          Ca mở lúc {timeOf(current.openedAt)}
          {syncing && <Badge variant="secondary">Đang đồng bộ ca…</Badge>}
        </span>
      ) : (
        <span className="text-destructive font-medium">Chưa mở ca làm việc. Mở ca trước khi nhận đơn.</span>
      )}
      {current ? (
        <Button variant="outline" size="sm" disabled={!online || !!closingId} onClick={() => setClosingId(current.id)}>
          Đóng ca
        </Button>
      ) : (
        <Button size="sm" disabled={!online || open.isPending || shift.isPending} onClick={() => open.mutate()}>
          Mở ca
        </Button>
      )}
    </div>
    {closingId && <CloseShiftForm shiftId={closingId} onDone={() => setClosingId(null)} />}
    </>
  )
}
