import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useIsOnline } from '@/lib/online'
import type { OrderItemStatus } from '@/features/orders/orderSchemas'
import { useKitchenFeed, useSetItemStatus } from './useKitchenFeed'

// Món đi lần lượt Chờ → Đang làm → Xong; mỗi cột chỉ có một nút chuyển sang cột kế.
const COLUMNS: { status: OrderItemStatus; title: string; next?: { status: 'Preparing' | 'Done'; label: string } }[] = [
  { status: 'Pending', title: 'Chờ', next: { status: 'Preparing', label: 'Bắt đầu làm' } },
  { status: 'Preparing', title: 'Đang làm', next: { status: 'Done', label: 'Xong' } },
  { status: 'Done', title: 'Xong' },
]

export function KitchenBoard() {
  const feed = useKitchenFeed()
  const setStatus = useSetItemStatus()
  const online = useIsOnline()

  if (feed.isPending) return <p className="text-muted-foreground p-4">Đang tải đơn…</p>
  if (feed.isError) return <p className="text-destructive p-4">Không tải được đơn cho bếp.</p>
  if (feed.data.every((o) => o.items.every((i) => i.status === 'Cancelled')))
    return <p className="text-muted-foreground p-4">Chưa có đơn nào</p>

  return (
    <div className="grid gap-4 p-4 md:grid-cols-3">
      {COLUMNS.map(({ status, title, next }) => (
        <section key={status} aria-label={title} data-testid={`kitchen-col-${status}`} className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">{title}</h2>
          {feed.data.map((order) => {
            const items = order.items.filter((i) => i.status === status)
            if (items.length === 0) return null
            return (
              <Card key={order.id} data-testid={`kitchen-order-${order.code}`}>
                <CardHeader>
                  <CardTitle>Đơn #{order.code}</CardTitle>
                </CardHeader>
                <CardContent className="flex flex-col gap-2">
                  {items.map((item) => (
                    <div key={item.id} className="flex items-center justify-between gap-2 text-sm">
                      <span>
                        {item.qty} × {item.name}
                      </span>
                      {next && (
                        <Button
                          size="sm"
                          disabled={!online || setStatus.isPending}
                          onClick={() => setStatus.mutate({ order, itemId: item.id, status: next.status })}
                        >
                          {next.label}
                        </Button>
                      )}
                    </div>
                  ))}
                </CardContent>
              </Card>
            )
          })}
        </section>
      ))}
    </div>
  )
}
