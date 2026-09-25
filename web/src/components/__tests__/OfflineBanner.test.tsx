import { onlineManager } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import { afterEach, expect, test } from 'vitest'
import { OfflineBanner } from '../OfflineBanner'

afterEach(() => act(() => onlineManager.setOnline(true)))

test('offlineBanner_ShowsWhenOffline_HidesWhenBack', () => {
  render(<OfflineBanner />)
  expect(screen.queryByText('Mất kết nối tới hệ thống')).toBeNull()

  act(() => onlineManager.setOnline(false))
  expect(screen.getByRole('alert').textContent).toBe('Mất kết nối tới hệ thống')

  act(() => onlineManager.setOnline(true))
  expect(screen.queryByRole('alert')).toBeNull()
})
