import { auth, AuthResponseSchema } from './auth'

export class ApiError extends Error {
  readonly status: number
  readonly detail: string
  constructor(status: number, detail: string) {
    super(detail)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

export type ApiOptions = {
  method?: string
  body?: unknown
  /** Phiên bản đơn (`version`) — gửi thành header `If-Match` cho lệnh ghi lên đơn đã có. */
  ifMatch?: number
  /** false = endpoint public (login, refresh, healthz): không đính Bearer, không refresh khi 401. */
  auth?: boolean
}

// Single-flight: mọi request 401 cùng lúc chờ chung MỘT lần refresh.
let refreshing: Promise<boolean> | null = null

function refreshOnce(): Promise<boolean> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null
  })
  return refreshing
}

async function doRefresh(): Promise<boolean> {
  const refreshToken = auth.getRefreshToken()
  if (!refreshToken) return false
  try {
    const res = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    if (!res.ok) return false
    auth.setSession(AuthResponseSchema.parse(await res.json()))
    return true
  } catch {
    return false
  }
}

/** Access token còn dùng được (tự refresh nếu vừa F5) — cho SignalR `accessTokenFactory`. */
export async function getFreshAccessToken(): Promise<string> {
  if (!auth.getAccessToken()) await refreshOnce()
  return auth.getAccessToken() ?? ''
}

function sessionExpired(): never {
  auth.clear()
  if (typeof window !== 'undefined' && window.location.pathname !== '/login') window.location.assign('/login')
  throw new ApiError(401, 'Phiên đăng nhập đã hết hạn. Đăng nhập lại nhé.')
}

async function send(path: string, opts: ApiOptions, token: string | null): Promise<Response> {
  const headers: Record<string, string> = {}
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`
  if (opts.ifMatch !== undefined) headers['If-Match'] = `"${opts.ifMatch}"`
  try {
    return await fetch(path, {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body === undefined ? undefined : JSON.stringify(opts.body),
    })
  } catch {
    throw new ApiError(0, 'Mất kết nối tới hệ thống')
  }
}

async function toError(res: Response): Promise<ApiError> {
  let detail = `Lỗi ${res.status}`
  try {
    const body: unknown = await res.json()
    if (body && typeof body === 'object') {
      if ('detail' in body && typeof body.detail === 'string') detail = body.detail
      else if ('title' in body && typeof body.title === 'string') detail = body.title
    }
  } catch {
    // Không phải ProblemDetails — giữ thông báo mặc định.
  }
  return new ApiError(res.status, detail)
}

/**
 * Client HTTP duy nhất của web. Trả `unknown` — nơi gọi tự parse bằng Zod.
 * 204 → null. Lỗi → ném `ApiError { status, detail }` (detail tiếng Việt từ ProblemDetails).
 */
export async function api(path: string, opts: ApiOptions = {}): Promise<unknown> {
  const useAuth = opts.auth !== false
  if (useAuth && !auth.getAccessToken() && auth.getRefreshToken()) await refreshOnce()

  const sentToken = useAuth ? auth.getAccessToken() : null
  let res = await send(path, opts, sentToken)

  if (res.status === 401 && useAuth) {
    // Request khác đã refresh xong trong lúc mình chờ → token đã đổi, chỉ cần retry.
    const ok = auth.getAccessToken() !== sentToken || (await refreshOnce())
    if (!ok) sessionExpired()
    res = await send(path, opts, auth.getAccessToken())
    if (res.status === 401) sessionExpired()
  }

  if (!res.ok) throw await toError(res)
  if (res.status === 204) return null
  const type = res.headers.get('content-type') ?? ''
  return type.includes('json') ? res.json() : res.text()
}

export const errorMessage = (e: unknown) => (e instanceof ApiError ? e.detail : 'Có lỗi xảy ra. Thử lại nhé.')
