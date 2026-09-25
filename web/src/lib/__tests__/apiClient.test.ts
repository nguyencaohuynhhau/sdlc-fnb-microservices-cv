import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { api, ApiError } from '../apiClient'
import { auth } from '../auth'

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const session = (n: number) => ({ accessToken: `access-${n}`, refreshToken: `refresh-${n}`, role: 'Cashier' as const, expiresIn: 3600 })

// Server giả: access-1 đã hết hạn (401), refresh đổi sang access-2 sau một nhịp trễ.
function fakeServer() {
  const calls: { url: string; auth: string | undefined; key: string | undefined; body: unknown }[] = []
  const fetchMock = vi.fn(async (url: string, init: RequestInit = {}) => {
    const headers = (init.headers ?? {}) as Record<string, string>
    calls.push({
      url,
      auth: headers.Authorization,
      key: headers['Idempotency-Key'],
      body: init.body ? JSON.parse(String(init.body)) : undefined,
    })
    if (url === '/api/auth/refresh') {
      await new Promise((r) => setTimeout(r, 20))
      return json(200, session(2))
    }
    if (headers.Authorization !== 'Bearer access-2') return json(401, { status: 401, detail: 'hết hạn' })
    return json(200, [{ url }])
  })
  vi.stubGlobal('fetch', fetchMock)
  return calls
}

beforeEach(() => {
  auth.setSession(session(1))
})

afterEach(() => {
  vi.unstubAllGlobals()
  auth.clear()
})

test('apiClient_401_refreshesOnceThenRetries', async () => {
  const calls = fakeServer()

  const result = await api('/api/menu')

  expect(result).toEqual([{ url: '/api/menu' }])
  expect(calls.map((c) => [c.url, c.auth])).toEqual([
    ['/api/menu', 'Bearer access-1'],
    ['/api/auth/refresh', undefined],
    ['/api/menu', 'Bearer access-2'],
  ])
  expect(calls[1].body).toEqual({ refreshToken: 'refresh-1' })
  expect(auth.getRefreshToken()).toBe('refresh-2')
})

test('apiClient_ParallelRefresh_SingleFlight', async () => {
  const calls = fakeServer()

  const results = await Promise.all([api('/api/menu'), api('/api/orders?status=Open'), api('/api/shifts/current')])

  expect(results).toHaveLength(3)
  expect(calls.filter((c) => c.url === '/api/auth/refresh')).toHaveLength(1)
  expect(calls.filter((c) => c.auth === 'Bearer access-2')).toHaveLength(3)
})

test('apiClient_401Retry_KeepsSameIdempotencyKey', async () => {
  const calls = fakeServer()

  await api('/api/payments', { method: 'POST', body: { orderId: 'o-1' }, idempotencyKey: 'key-1' })

  // Gửi lại mà mất/đổi khoá thì server coi là lần thu mới → nguy cơ thu hai lần.
  expect(calls.filter((c) => c.url === '/api/payments').map((c) => [c.auth, c.key])).toEqual([
    ['Bearer access-1', 'key-1'],
    ['Bearer access-2', 'key-1'],
  ])
})

test('apiClient_ProblemDetails_ThrowsApiErrorWithDetail', async () => {
  const detail = 'Đơn vừa được người khác cập nhật. Tải lại rồi thử lại nhé.'
  const fetchMock = vi.fn(async () => json(409, { status: 409, title: 'Conflict', detail }))
  vi.stubGlobal('fetch', fetchMock)

  const error = await api('/api/orders/1/cancel', { method: 'POST', ifMatch: 7 }).catch((e: unknown) => e)

  expect(error).toBeInstanceOf(ApiError)
  expect(error).toMatchObject({ status: 409, detail })
  expect(fetchMock.mock.calls[0]).toEqual([
    '/api/orders/1/cancel',
    expect.objectContaining({ headers: expect.objectContaining({ 'If-Match': '"7"' }) }),
  ])
})

test('apiClient_204_ReturnsNull', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })))
  expect(await api('/api/shifts/current')).toBeNull()
})
