import { createFileRoute, redirect } from '@tanstack/react-router'
import { auth, homeFor } from '@/lib/auth'

export const Route = createFileRoute('/')({
  beforeLoad: () => {
    throw redirect({ to: auth.isLoggedIn() ? homeFor(auth.getRole()) : '/login' })
  },
})
