import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatVnd } from '@/lib/utils'
import type { Order } from '@/features/orders/orderSchemas'
import { methodLabel, type PaymentReceipt } from './paymentSchemas'

/** Biên nhận giữ trên màn hình tới khi bấm "Đơn mới", kể cả khi đơn đã rời danh sách. */
export function Receipt({ order, receipt, onDone }: { order: Order; receipt: PaymentReceipt; onDone: () => void }) {
  return (
    <Card aria-label="Biên nhận" data-testid="receipt">
      <CardHeader>
        <CardTitle>Đã thu đơn #{order.code}</CardTitle>
        <Badge>{methodLabel[receipt.method]}</Badge>
      </CardHeader>
      <CardContent className="flex flex-col gap-1 text-sm">
        {order.items
          .filter((i) => i.status !== 'Cancelled')
          .map((i) => (
            <div key={i.id} className="flex justify-between gap-2">
              <span>
                {i.qty} × {i.name}
              </span>
              <span className="tabular-nums">{formatVnd(i.unitPrice * i.qty)}</span>
            </div>
          ))}
        <div className="flex justify-between border-t pt-2 font-medium">
          <span>Tổng</span>
          <span className="tabular-nums">{formatVnd(receipt.amount)}</span>
        </div>
        <p className="text-muted-foreground">{new Date(receipt.paidAt).toLocaleString('vi-VN')}</p>
        <Button className="mt-2" onClick={onDone}>
          Đơn mới
        </Button>
      </CardContent>
    </Card>
  )
}
