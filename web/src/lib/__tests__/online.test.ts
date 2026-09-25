import { onlineManager, QueryClient } from '@tanstack/react-query'
import { afterEach, expect, test, vi } from 'vitest'
import { pingHealthz, startOnlineWatch } from '../online'

afterEach(() => {
  vi.unstubAllGlobals()
  onlineManager.setOnline(true)
})

test('online_HealthzFails_MarksOffline', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('down', { status: 503 })))
  await pingHealthz()
  expect(onlineManager.isOnline()).toBe(false)

  vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('Failed to fetch'))))
  onlineManager.setOnline(true)
  await pingHealthz()
  expect(onlineManager.isOnline()).toBe(false)
})

test('online_BackOnline_InvalidatesQueries', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('ok', { status: 200 })))
  const queryClient = new QueryClient()
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const stop = startOnlineWatch(queryClient, 60_000)

  onlineManager.setOnline(false)
  expect(invalidate).not.toHaveBeenCalled()
  onlineManager.setOnline(true)
  expect(invalidate).toHaveBeenCalledTimes(1)
  stop()
})
