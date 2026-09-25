import { useForm } from '@tanstack/react-form'
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, errorMessage } from '@/lib/apiClient'
import { auth, AuthResponseSchema, homeFor } from '@/lib/auth'

const LoginSchema = z.object({
  username: z.string().min(3, 'Tên đăng nhập từ 3 đến 50 ký tự.').max(50, 'Tên đăng nhập từ 3 đến 50 ký tự.'),
  password: z.string().min(8, 'Mật khẩu tối thiểu 8 ký tự.').max(100, 'Mật khẩu tối đa 100 ký tự.'),
})

export const Route = createFileRoute('/login')({
  beforeLoad: () => {
    if (auth.isLoggedIn()) throw redirect({ to: homeFor(auth.getRole()) })
  },
  component: LoginPage,
})

function LoginPage() {
  const navigate = useNavigate()
  // Câu lỗi của server (401/423) — UI state của form, không phải dữ liệu cache.
  const [serverError, setServerError] = useState<string | null>(null)

  const form = useForm({
    defaultValues: { username: '', password: '' },
    validators: { onChange: LoginSchema },
    onSubmit: async ({ value }) => {
      setServerError(null)
      try {
        const res = AuthResponseSchema.parse(
          await api('/api/auth/login', { method: 'POST', body: value, auth: false }),
        )
        auth.setSession(res)
        await navigate({ to: homeFor(res.role) })
      } catch (e) {
        setServerError(errorMessage(e))
      }
    },
  })

  return (
    <main className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">Đăng nhập</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            noValidate
            className="flex flex-col gap-4"
            onSubmit={(e) => {
              e.preventDefault()
              void form.handleSubmit()
            }}
          >
            {(['username', 'password'] as const).map((name) => (
              <form.Field key={name} name={name}>
                {(field) => {
                  const error = field.state.meta.isTouched ? field.state.meta.errors[0]?.message : undefined
                  return (
                    <div className="flex flex-col gap-2">
                      <Label htmlFor={name}>{name === 'username' ? 'Tên đăng nhập' : 'Mật khẩu'}</Label>
                      <Input
                        id={name}
                        name={name}
                        type={name === 'password' ? 'password' : 'text'}
                        autoComplete={name === 'password' ? 'current-password' : 'username'}
                        value={field.state.value}
                        onBlur={field.handleBlur}
                        onChange={(e) => field.handleChange(e.target.value)}
                        aria-invalid={!!error}
                        aria-describedby={error ? `${name}-error` : undefined}
                      />
                      {error && (
                        <p id={`${name}-error`} className="text-destructive text-sm">
                          {error}
                        </p>
                      )}
                    </div>
                  )
                }}
              </form.Field>
            ))}
            {serverError && (
              <p role="alert" className="text-destructive text-sm">
                {serverError}
              </p>
            )}
            <form.Subscribe selector={(s) => s.isSubmitting}>
              {(isSubmitting) => (
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
                </Button>
              )}
            </form.Subscribe>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
