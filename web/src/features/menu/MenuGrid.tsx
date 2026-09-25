import { Badge } from '@/components/ui/badge'
import { useCart } from '@/stores/ui'
import { cn, formatVnd } from '@/lib/utils'
import { useMenu } from './useMenu'

export function MenuGrid() {
  const menu = useMenu()
  const add = useCart((s) => s.add)

  if (menu.isPending) return <p className="text-muted-foreground p-4">Đang tải thực đơn…</p>
  if (menu.isError) return <p className="text-destructive p-4">Không tải được thực đơn.</p>
  if (menu.data.length === 0) return <p className="text-muted-foreground p-4">Thực đơn trống — chạy seed</p>

  return (
    <ul className="grid grid-cols-2 gap-3 p-4 md:grid-cols-3 xl:grid-cols-4" aria-label="Thực đơn">
      {menu.data.map((item) => (
        <li key={item.id}>
          <button
            type="button"
            disabled={!item.isAvailable}
            onClick={() => add(item)}
            className={cn(
              'bg-card flex h-full w-full flex-col items-start gap-1 rounded-xl border p-3 text-left shadow-xs',
              'hover:bg-accent focus-visible:ring-ring/50 outline-none focus-visible:ring-[3px]',
              'disabled:cursor-not-allowed disabled:opacity-50',
            )}
          >
            <span className="flex w-full items-start justify-between gap-2 font-medium">
              {item.name}
              {!item.isAvailable && <Badge variant="destructive">Hết</Badge>}
            </span>
            <span className="text-muted-foreground text-sm">{formatVnd(item.price)}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}
