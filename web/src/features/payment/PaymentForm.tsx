import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { formatVnd } from '@/lib/utils'
import type { Order } from '@/features/orders/orderSchemas'
import { methodLabel, PaymentMethodSchema, type PaymentMethod } from './paymentSchemas'
import type { usePayOrder } from './usePayOrder'

type Props = { order: Order; pay: ReturnType<typeof usePayOrder>; online: boolean; onClose: () => void }

/** Form thu tiền inline trong thẻ đơn. Một khoá cho cả lần mở form — đóng rồi mở lại mới sinh khoá mới. */
export function PaymentForm({ order, pay, online, onClose }: Props) {
  const [idempotencyKey] = useState(() => crypto.randomUUID())
  const [method, setMethod] = useState<PaymentMethod>('Cash')

  return (
    <div role="group" aria-label={`Thu tiền đơn #${order.code}`} className="flex flex-col gap-2 rounded-md border p-2">
      <div className="flex gap-2">
        {PaymentMethodSchema.options.map((m) => (
          <Button
            key={m}
            size="sm"
            variant={method === m ? 'default' : 'outline'}
            aria-pressed={method === m}
            onClick={() => setMethod(m)}
          >
            {methodLabel[m]}
          </Button>
        ))}
      </div>
      <Button
        disabled={!online || pay.isPending}
        onClick={() => pay.mutate({ order, method, idempotencyKey }, { onSuccess: onClose })}
      >
        Xác nhận thu {formatVnd(order.total)}
      </Button>
      <Button variant="ghost" size="sm" disabled={pay.isPending} onClick={onClose}>
        Đóng
      </Button>
    </div>
  )
}
