import { createFileRoute, redirect } from '@tanstack/react-router'
import { KitchenBoard } from '@/features/kitchen/KitchenBoard'
import { auth, homeFor } from '@/lib/auth'

export const Route = createFileRoute('/_auth/kitchen')({
  beforeLoad: () => {
    const role = auth.getRole()
    if (role !== 'Kitchen' && role !== 'Owner') throw redirect({ to: homeFor(role) })
  },
  component: KitchenBoard,
})
