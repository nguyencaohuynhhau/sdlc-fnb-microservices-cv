import { z } from 'zod'

export const RoleSchema = z.enum(['Owner', 'Cashier', 'Kitchen'])
export type Role = z.infer<typeof RoleSchema>

export const AuthResponseSchema = z.object({
  accessToken: z.string(),
  refreshToken: z.string(),
  role: RoleSchema,
  expiresIn: z.number(),
})
export type AuthResponse = z.infer<typeof AuthResponseSchema>

const REFRESH_KEY = 'fnb.refreshToken'
const ROLE_KEY = 'fnb.role'

// Access token chỉ nằm trong bộ nhớ (không để XSS đọc từ storage); refresh token + role
// nằm trong localStorage để F5 vẫn giữ phiên — apiClient tự đổi lấy access token mới.
let accessToken: string | null = null

export const auth = {
  getAccessToken: () => accessToken,
  getRefreshToken: () => localStorage.getItem(REFRESH_KEY),
  getRole: (): Role | null => {
    const r = RoleSchema.safeParse(localStorage.getItem(ROLE_KEY))
    return r.success ? r.data : null
  },
  isLoggedIn: () => localStorage.getItem(REFRESH_KEY) !== null,
  setSession(res: AuthResponse) {
    accessToken = res.accessToken
    localStorage.setItem(REFRESH_KEY, res.refreshToken)
    localStorage.setItem(ROLE_KEY, res.role)
  },
  clear() {
    accessToken = null
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(ROLE_KEY)
  },
}

/** Màn hình mặc định theo vai trò: bếp vào /kitchen, thu ngân và chủ quán vào /pos. */
export const homeFor = (role: Role | null) => (role === 'Kitchen' ? '/kitchen' : '/pos')
