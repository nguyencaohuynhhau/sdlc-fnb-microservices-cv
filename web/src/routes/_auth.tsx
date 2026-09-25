import { createFileRoute, Outlet, redirect, useNavigate } from '@tanstack/react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { auth } from '@/lib/auth'

// Layout cho mọi màn cần đăng nhập. Chưa có phiên → về /login.
export const Route = createFileRoute('/_auth')({
  beforeLoad: () => {
    if (!auth.isLoggedIn()) throw redirect({ to: '/login' })
  },
  component: AuthLayout,
})

const roleLabel = { Owner: 'Chủ quán', Cashier: 'Thu ngân', Kitchen: 'Bếp' } as const

function AuthLayout() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const role = auth.getRole()

  const logout = () => {
    auth.clear()
    queryClient.clear()
    void navigate({ to: '/login' })
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center justify-between border-b px-4 py-2">
        <span className="font-semibold">FnB POS</span>
        <span className="flex items-center gap-3 text-sm">
          {role && <span className="text-muted-foreground">{roleLabel[role]}</span>}
          <Button variant="ghost" size="sm" onClick={logout}>
            Đăng xuất
          </Button>
        </span>
      </header>
      <Outlet />
    </div>
  )
}
