import { createRootRoute, Outlet } from '@tanstack/react-router'
import { OfflineBanner } from '@/components/OfflineBanner'
import { Toaster } from '@/components/ui/sonner'

export const Route = createRootRoute({
  component: () => (
    <>
      <OfflineBanner />
      <Outlet />
      <Toaster />
    </>
  ),
})
