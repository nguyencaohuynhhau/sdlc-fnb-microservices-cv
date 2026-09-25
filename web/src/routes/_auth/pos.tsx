import { createFileRoute, redirect } from '@tanstack/react-router'
import { MenuGrid } from '@/features/menu/MenuGrid'
import { OrderPanel } from '@/features/orders/OrderPanel'
import { ShiftBar } from '@/features/shift/ShiftBar'
import { auth, homeFor } from '@/lib/auth'

export const Route = createFileRoute('/_auth/pos')({
  beforeLoad: () => {
    const role = auth.getRole()
    if (role !== 'Cashier' && role !== 'Owner') throw redirect({ to: homeFor(role) })
  },
  component: PosPage,
})

function PosPage() {
  return (
    <>
      <ShiftBar />
      <main className="grid flex-1 lg:grid-cols-[1fr_24rem]">
        <MenuGrid />
        <aside className="border-l">
          <OrderPanel />
        </aside>
      </main>
    </>
  )
}
