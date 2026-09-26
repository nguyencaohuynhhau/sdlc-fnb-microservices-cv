import { Fragment, useState } from 'react'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useActiveOrders } from '@/features/orders/useOrders'
import { useIsOnline } from '@/lib/online'
import { cn, formatVnd } from '@/lib/utils'
import { useCloseShift, type ShiftSummary } from './useShift'

// Khớp validate phía server: 0 … 1.000.000.000.
const CountedCashSchema = z
  .string()
  .trim()
  .min(1, 'Nhập số tiền mặt đếm được.')
  .transform(Number)
  .pipe(
    z
      .number({ message: 'Số tiền không hợp lệ.' })
      .min(0, 'Số tiền không được âm.')
      .max(1_000_000_000, 'Số tiền quá lớn.'),
  )

/** Đếm mù: nhập tiền đếm trước, chỉ sau khi chốt mới thấy số dự kiến và độ lệch. */
export function CloseShiftForm({ shiftId, onDone }: { shiftId: string; onDone: () => void }) {
  const close = useCloseShift()
  const orders = useActiveOrders(shiftId)
  const online = useIsOnline()
  const [counted, setCounted] = useState('')
  const parsed = CountedCashSchema.safeParse(counted)
  const unpaid = orders.data?.filter((o) => o.status === 'Open').length ?? 0

  if (close.data) return <Summary summary={close.data} onDone={onDone} />

  return (
    <form
      aria-label="Đóng ca"
      className="flex flex-col gap-2 border-b px-4 py-3 sm:max-w-sm"
      onSubmit={(e) => {
        e.preventDefault()
        if (parsed.success) close.mutate({ shiftId, countedCash: parsed.data })
      }}
    >
      {unpaid > 0 && (
        <p role="alert" className="text-destructive text-sm font-medium">
          Còn {unpaid} đơn chưa thu tiền. Vẫn có thể đóng ca.
        </p>
      )}
      <Label htmlFor="counted-cash">Tiền mặt đếm được trong két</Label>
      <Input
        id="counted-cash"
        inputMode="numeric"
        autoFocus
        value={counted}
        aria-invalid={counted !== '' && !parsed.success}
        onChange={(e) => setCounted(e.target.value)}
      />
      {counted !== '' && !parsed.success && (
        <p className="text-destructive text-sm">{parsed.error.issues[0]?.message}</p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={!online || !parsed.success || close.isPending}>
          Xác nhận đóng ca
        </Button>
        <Button type="button" variant="ghost" disabled={close.isPending} onClick={onDone}>
          Huỷ
        </Button>
      </div>
    </form>
  )
}

const varianceLabel = (v: number) => (v === 0 ? 'Khớp tiền' : `${v < 0 ? 'Thiếu' : 'Thừa'} ${formatVnd(Math.abs(v))}`)

function Summary({ summary: s, onDone }: { summary: ShiftSummary; onDone: () => void }) {
  const rows: [string, string][] = [
    ['Số đơn đã thu', String(s.orderCount)],
    ['Doanh thu', formatVnd(s.revenue)],
    ['Tiền mặt', formatVnd(s.cashTotal)],
    ['Chuyển khoản', formatVnd(s.transferTotal)],
    ['Quỹ đầu ca', formatVnd(s.openingFloat)],
    ['Tiền mặt dự kiến trong két', formatVnd(s.expectedCash)],
    ['Đếm được', formatVnd(s.countedCash)],
  ]
  return (
    <section aria-label="Tổng kết ca" data-testid="shift-summary" className="flex flex-col gap-2 border-b px-4 py-3 sm:max-w-sm">
      <dl className="grid grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-sm">
        {rows.map(([k, v]) => (
          <Fragment key={k}>
            <dt className="text-muted-foreground">{k}</dt>
            <dd className="text-right tabular-nums">{v}</dd>
          </Fragment>
        ))}
      </dl>
      <p className={cn('font-medium', s.variance !== 0 && 'text-destructive')}>Lệch: {varianceLabel(s.variance)}</p>
      <Button onClick={onDone}>Xong</Button>
    </section>
  )
}
