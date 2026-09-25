import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useIsOnline } from '@/lib/online'
import { useOrdersHub } from '@/lib/signalr'
import { cn, formatVnd } from '@/lib/utils'
import { useCart } from '@/stores/ui'
import { PaymentForm } from '@/features/payment/PaymentForm'
import { Receipt } from '@/features/payment/Receipt'
import { usePayOrder } from '@/features/payment/usePayOrder'
import { useCurrentShift, useOrderingShift } from '@/features/shift/useShift'
import { DraftOrderSchema, itemStatusLabel } from './orderSchemas'
import { useCancelItem, useCancelOrder, useActiveOrders, useCreateOrder } from './useOrders'

export function OrderPanel() {
  const shift = useCurrentShift()
  const orderingShift = useOrderingShift(shift.data?.id)
  // Chỉ nhận đơn khi ordering đã thấy đúng ca cashier vừa mở — tránh 409 oan (plan §5 #6).
  const shiftId = shift.data && orderingShift.data?.shiftId === shift.data.id ? shift.data.id : undefined
  useOrdersHub(shiftId)

  const online = useIsOnline()
  const lines = useCart((s) => s.lines)
  const add = useCart((s) => s.add)
  const decrement = useCart((s) => s.decrement)
  const clear = useCart((s) => s.clear)
  const create = useCreateOrder()
  // Ở đây chứ không ở thẻ đơn: đơn đã thu có thể rời danh sách, biên nhận vẫn phải còn.
  const pay = usePayOrder()

  const draft = DraftOrderSchema.safeParse({ items: lines.map(({ menuItemId, qty }) => ({ menuItemId, qty })) })
  const draftTotal = lines.reduce((sum, l) => sum + l.price * l.qty, 0)
  const canSend = !!shiftId && online && draft.success && !create.isPending

  return (
    <div className="flex flex-col gap-4 p-4">
      {pay.data && pay.variables && (
        <Receipt order={pay.variables.order} receipt={pay.data} onDone={pay.reset} />
      )}

      <Card aria-label="Đơn nháp">
        <CardHeader>
          <CardTitle>Đơn mới</CardTitle>
          {lines.length > 0 && (
            <Button variant="ghost" size="sm" onClick={clear}>
              Xoá hết
            </Button>
          )}
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {lines.length === 0 && <p className="text-muted-foreground text-sm">Chọn món từ thực đơn.</p>}
          {lines.map((l) => (
            <div key={l.menuItemId} className="flex items-center justify-between gap-2 text-sm">
              <span className="flex-1">{l.name}</span>
              <Button variant="outline" size="sm" aria-label={`Bớt ${l.name}`} onClick={() => decrement(l.menuItemId)}>
                −
              </Button>
              <span className="w-6 text-center tabular-nums">{l.qty}</span>
              <Button
                variant="outline"
                size="sm"
                aria-label={`Thêm ${l.name}`}
                onClick={() => add({ id: l.menuItemId, name: l.name, price: l.price })}
              >
                +
              </Button>
              <span className="w-24 text-right tabular-nums">{formatVnd(l.price * l.qty)}</span>
            </div>
          ))}
          {lines.length > 0 && !draft.success && (
            <p className="text-destructive text-sm">{draft.error.issues[0]?.message}</p>
          )}
          <div className="flex items-center justify-between border-t pt-2 font-medium">
            <span>Tổng</span>
            <span className="tabular-nums">{formatVnd(draftTotal)}</span>
          </div>
          <Button disabled={!canSend} onClick={() => draft.success && create.mutate(draft.data, { onSuccess: clear })}>
            Gửi bếp
          </Button>
        </CardContent>
      </Card>

      <ActiveOrders shiftId={shiftId} online={online} pay={pay} />
    </div>
  )
}

type ActiveOrdersProps = { shiftId: string | undefined; online: boolean; pay: ReturnType<typeof usePayOrder> }

function ActiveOrders({ shiftId, online, pay }: ActiveOrdersProps) {
  const orders = useActiveOrders(shiftId)
  const cancelItem = useCancelItem()
  const cancelOrder = useCancelOrder()
  const [payingId, setPayingId] = useState<string | null>(null)
  const busy = !online || cancelItem.isPending || cancelOrder.isPending || pay.isPending

  if (!shiftId) return null
  if (orders.isPending) return <p className="text-muted-foreground text-sm">Đang tải đơn…</p>
  if (orders.isError) return <p className="text-destructive text-sm">Không tải được danh sách đơn.</p>
  if (orders.data.length === 0) return <p className="text-muted-foreground text-sm">Chưa có đơn nào trong ca.</p>

  return (
    <section aria-label="Đơn trong ca" className="flex flex-col gap-3">
      {orders.data.map((order) => (
        <Card key={order.id} data-testid={`pos-order-${order.code}`}>
          <CardHeader>
            <CardTitle>Đơn #{order.code}</CardTitle>
            <span className="flex items-center gap-2 text-sm font-medium tabular-nums">
              {order.status === 'Paid' && <Badge>Đã thu</Badge>}
              {formatVnd(order.total)}
            </span>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {order.items.map((item) => (
              <div key={item.id} className="flex items-center justify-between gap-2 text-sm">
                <span className={cn('flex-1', item.status === 'Cancelled' && 'text-muted-foreground line-through')}>
                  {item.qty} × {item.name}
                </span>
                <Badge variant={item.status === 'Done' ? 'default' : 'secondary'}>{itemStatusLabel[item.status]}</Badge>
                {order.status === 'Open' && item.status === 'Pending' && (
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`Huỷ món ${item.name}`}
                    disabled={busy}
                    onClick={() => cancelItem.mutate({ order, itemId: item.id })}
                  >
                    Huỷ
                  </Button>
                )}
              </div>
            ))}
            {order.status === 'Open' &&
              (payingId === order.id ? (
                <PaymentForm order={order} pay={pay} online={online} onClose={() => setPayingId(null)} />
              ) : (
                <div className="flex gap-2">
                  <Button className="flex-1" disabled={busy || order.total <= 0} onClick={() => setPayingId(order.id)}>
                    Thu tiền
                  </Button>
                  <Button variant="destructive" size="sm" disabled={busy} onClick={() => cancelOrder.mutate(order)}>
                    Huỷ đơn
                  </Button>
                </div>
              ))}
          </CardContent>
        </Card>
      ))}
    </section>
  )
}
