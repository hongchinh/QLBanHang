import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type * as SignalR from '@microsoft/signalr'

// Shared mocks — declared before vi.mock calls (hoisted by Vitest)
const mockOn = vi.fn()
const mockStart = vi.fn().mockResolvedValue(undefined)
const mockStop = vi.fn().mockResolvedValue(undefined)
const mockWithUrl = vi.fn().mockReturnThis()
const mockWithAutomaticReconnect = vi.fn().mockReturnThis()
const mockOnReconnecting = vi.fn()
const mockOnReconnected = vi.fn()
const mockOnClose = vi.fn()
// The connection the hook gets. `state` is what SignalR would report: a test sets it by hand.
const connection = {
  state: 'Disconnected',
  on: mockOn,
  start: mockStart,
  stop: mockStop,
  onreconnecting: mockOnReconnecting,
  onreconnected: mockOnReconnected,
  onclose: mockOnClose,
}
const mockBuild = vi.fn().mockReturnValue(connection)
const invalidateQueries = vi.fn()
// One client for every render, as the real useQueryClient returns: the hook's effect depends on it.
const queryClient = { invalidateQueries }
// The token of the signed-in user: a test changes it and renders again.
const auth = { accessToken: 'test-token' as string | null }

vi.mock('@microsoft/signalr', async (importOriginal) => {
  const actual = await importOriginal<typeof SignalR>()
  return {
    HubConnectionState: actual.HubConnectionState,
    HubConnectionBuilder: vi.fn(function () {
      return {
        withUrl: mockWithUrl,
        withAutomaticReconnect: mockWithAutomaticReconnect,
        build: mockBuild,
      }
    }),
  }
})

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: Object.assign(
    vi.fn((selector: (s: { accessToken: string | null }) => unknown) =>
      selector({ accessToken: auth.accessToken })
    ),
    { getState: () => ({ accessToken: auth.accessToken }) },
  ),
}))

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>()
  return {
    ...actual,
    useQueryClient: vi.fn(() => queryClient),
  }
})

import { HubConnectionState } from '@microsoft/signalr'
import { realtimeBus } from '@/lib/realtime-bus'
import { useNotificationHub } from './useNotificationHub'

describe('useNotificationHub', () => {
  beforeEach(() => vi.clearAllMocks())
  afterEach(() => vi.restoreAllMocks())

  it('connects to /hubs/notifications on mount', () => {
    renderHook(() => useNotificationHub())
    expect(mockWithUrl).toHaveBeenCalledWith(
      '/hubs/notifications',
      expect.objectContaining({ accessTokenFactory: expect.any(Function) })
    )
    expect(mockStart).toHaveBeenCalled()
  })

  it('registers NewNotification handler', () => {
    renderHook(() => useNotificationHub())
    expect(mockOn).toHaveBeenCalledWith('NewNotification', expect.any(Function))
  })

  it('stops connection on unmount', () => {
    const { unmount } = renderHook(() => useNotificationHub())
    unmount()
    expect(mockStop).toHaveBeenCalled()
  })

  it('invalidates unread-count query on NewNotification', () => {
    renderHook(() => useNotificationHub())

    // Get the handler registered with 'NewNotification' and call it
    const handler = mockOn.mock.calls.find(([event]: [string]) => event === 'NewNotification')?.[1] as (() => void) | undefined
    handler?.()

    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: ['notifications', 'unread-count'],
    })
  })
})

describe('useNotificationHub: dashboard signal and connection status', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // clearAllMocks keeps "once" implementations a test did not use up.
    mockStart.mockReset().mockResolvedValue(undefined)
    realtimeBus.setStatus('connecting')
    connection.state = HubConnectionState.Disconnected
    auth.accessToken = 'test-token'
  })
  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  const handlerOf = (event: string) =>
    mockOn.mock.calls.find((call) => call[0] === event)?.[1] as ((payload?: unknown) => void) | undefined
  const connected = () => waitFor(() => expect(realtimeBus.getStatus()).toBe('connected'))
  // A start() the test settles itself.
  function pendingStart() {
    let resolve!: () => void
    let reject!: (error: Error) => void
    mockStart.mockImplementationOnce(
      () =>
        new Promise<void>((res, rej) => {
          resolve = res
          reject = rej
        }),
    )
    return { resolve: () => resolve(), reject: (error: Error) => reject(error) }
  }

  it('forwards DashboardChanged to the bus and marks the executive queries stale without refetching them', () => {
    const listener = vi.fn()
    const off = realtimeBus.onDashboardChanged(listener)
    renderHook(() => useNotificationHub())

    const signal = { branchId: 'b1', domains: ['cash', 'debt'] }
    const handler = handlerOf('DashboardChanged')
    expect(handler).toEqual(expect.any(Function))
    handler?.(signal)
    off()

    expect(listener).toHaveBeenCalledTimes(1)
    expect(listener).toHaveBeenCalledWith(signal)
    expect(invalidateQueries).toHaveBeenCalledTimes(1)
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['executive'], refetchType: 'none' })
  })

  it('NewNotification still touches the notification queries only', () => {
    const listener = vi.fn()
    const off = realtimeBus.onDashboardChanged(listener)
    renderHook(() => useNotificationHub())

    handlerOf('NewNotification')?.()
    off()

    expect(listener).not.toHaveBeenCalled()
    expect(invalidateQueries).toHaveBeenCalledTimes(1)
    const [filters] = invalidateQueries.mock.calls[0] as [{ queryKey: string[]; refetchType?: string }]
    expect(filters.queryKey[0]).toBe('notifications')
    expect(filters.refetchType).toBeUndefined()
  })

  it('is connecting until start() resolves, then connected', async () => {
    const start = pendingStart()
    // What a previous connection left behind.
    realtimeBus.setStatus('disconnected')

    renderHook(() => useNotificationHub())
    expect(realtimeBus.getStatus()).toBe('connecting')

    start.resolve()
    await connected()
  })

  it('follows reconnecting, reconnected and close', async () => {
    renderHook(() => useNotificationHub())
    await connected()

    mockOnReconnecting.mock.calls[0][0]()
    expect(realtimeBus.getStatus()).toBe('disconnected')

    mockOnReconnected.mock.calls[0][0]()
    expect(realtimeBus.getStatus()).toBe('connected')

    mockOnClose.mock.calls[0][0]()
    expect(realtimeBus.getStatus()).toBe('disconnected')
  })

  it('unmount disconnects, and a late callback of that connection changes nothing', async () => {
    const { unmount } = renderHook(() => useNotificationHub())
    await connected()
    const onReconnecting = mockOnReconnecting.mock.calls[0][0]
    const onReconnected = mockOnReconnected.mock.calls[0][0]
    const onClose = mockOnClose.mock.calls[0][0]

    unmount()
    expect(realtimeBus.getStatus()).toBe('disconnected')

    onReconnected()
    expect(realtimeBus.getStatus()).toBe('disconnected')

    // The connection that replaced it is up: the old one must not report it down.
    realtimeBus.setStatus('connected')
    onReconnecting()
    onClose()
    expect(realtimeBus.getStatus()).toBe('connected')
  })

  it('a start() that resolves after unmount does not report connected', async () => {
    const start = pendingStart()
    const { unmount } = renderHook(() => useNotificationHub())

    unmount()
    start.resolve()
    await Promise.resolve()
    await Promise.resolve()

    expect(realtimeBus.getStatus()).toBe('disconnected')
  })

  it('reconnects without an end: the retry delay is always a number, at most 30 seconds', () => {
    renderHook(() => useNotificationHub())

    expect(mockWithAutomaticReconnect).toHaveBeenCalledTimes(1)
    const policy = mockWithAutomaticReconnect.mock.calls[0][0] as {
      nextRetryDelayInMilliseconds: (context: { previousRetryCount: number }) => number | null
    }
    const delay = (previousRetryCount: number) => policy.nextRetryDelayInMilliseconds({ previousRetryCount })

    expect(delay(0)).toBe(2_000)
    expect(delay(1)).toBe(4_000)
    expect(delay(14)).toBe(30_000)
    expect(delay(100)).toBe(30_000)
    for (const count of [0, 1, 13, 14, 15, 100, 1_000_000]) {
      expect(delay(count)).toEqual(expect.any(Number))
      expect(delay(count)).toBeGreaterThan(0)
      expect(delay(count)).toBeLessThanOrEqual(30_000)
    }
  })

  it('retries a failed start() every 5 seconds until it connects', async () => {
    vi.useFakeTimers()
    vi.spyOn(console, 'error').mockImplementation(() => {})
    mockStart.mockRejectedValueOnce(new Error('offline')).mockRejectedValueOnce(new Error('offline'))

    renderHook(() => useNotificationHub())
    await vi.advanceTimersByTimeAsync(0)
    expect(realtimeBus.getStatus()).toBe('disconnected')
    expect(mockStart).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(4_999)
    expect(mockStart).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(mockStart).toHaveBeenCalledTimes(2)
    expect(realtimeBus.getStatus()).toBe('disconnected')

    await vi.advanceTimersByTimeAsync(5_000)
    expect(mockStart).toHaveBeenCalledTimes(3)
    await vi.advanceTimersByTimeAsync(0)
    expect(realtimeBus.getStatus()).toBe('connected')

    // Connected: nothing keeps retrying.
    await vi.advanceTimersByTimeAsync(60_000)
    expect(mockStart).toHaveBeenCalledTimes(3)
  })

  it('stops retrying after unmount', async () => {
    vi.useFakeTimers()
    vi.spyOn(console, 'error').mockImplementation(() => {})
    mockStart.mockRejectedValueOnce(new Error('offline'))
    const { unmount } = renderHook(() => useNotificationHub())
    await vi.advanceTimersByTimeAsync(0)
    expect(mockStart).toHaveBeenCalledTimes(1)

    unmount()
    await vi.advanceTimersByTimeAsync(60_000)

    expect(mockStart).toHaveBeenCalledTimes(1)
  })

  it('a start() that rejects after unmount neither retries nor changes the status', async () => {
    vi.useFakeTimers()
    vi.spyOn(console, 'error').mockImplementation(() => {})
    const start = pendingStart()
    const { unmount } = renderHook(() => useNotificationHub())

    unmount()
    realtimeBus.setStatus('connected')
    start.reject(new Error('stopped'))
    await vi.advanceTimersByTimeAsync(60_000)

    expect(mockStart).toHaveBeenCalledTimes(1)
    expect(realtimeBus.getStatus()).toBe('connected')
    // A start stopped by the unmount (StrictMode, leaving the page) is not reported as a failure.
    expect(console.error).not.toHaveBeenCalled()
  })

  describe('a connection that closes while the hook is mounted', () => {
    // SignalR reports Disconnected before it calls the close callbacks.
    const close = () => {
      connection.state = HubConnectionState.Disconnected
      mockOnClose.mock.calls[0][0]()
    }
    async function mountConnected() {
      vi.useFakeTimers()
      const view = renderHook(() => useNotificationHub())
      await vi.advanceTimersByTimeAsync(0)
      expect(realtimeBus.getStatus()).toBe('connected')
      expect(mockStart).toHaveBeenCalledTimes(1)
      return view
    }

    it('is started again 5 seconds later', async () => {
      await mountConnected()

      close()
      expect(realtimeBus.getStatus()).toBe('disconnected')

      await vi.advanceTimersByTimeAsync(4_999)
      expect(mockStart).toHaveBeenCalledTimes(1)
      await vi.advanceTimersByTimeAsync(1)
      expect(mockStart).toHaveBeenCalledTimes(2)
      await vi.advanceTimersByTimeAsync(0)
      expect(realtimeBus.getStatus()).toBe('connected')
      expect(mockBuild).toHaveBeenCalledTimes(1)
    })

    it('keeps trying every 5 seconds when the restart fails', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      await mountConnected()
      mockStart.mockRejectedValueOnce(new Error('offline'))

      close()
      await vi.advanceTimersByTimeAsync(5_000)
      expect(mockStart).toHaveBeenCalledTimes(2)
      expect(realtimeBus.getStatus()).toBe('disconnected')

      await vi.advanceTimersByTimeAsync(5_000)
      expect(mockStart).toHaveBeenCalledTimes(3)
      await vi.advanceTimersByTimeAsync(0)
      expect(realtimeBus.getStatus()).toBe('connected')
    })

    it.each([
      HubConnectionState.Connecting,
      HubConnectionState.Connected,
      HubConnectionState.Reconnecting,
      HubConnectionState.Disconnecting,
    ])('is left alone by the retry when its state is %s', async (state) => {
      await mountConnected()
      close()

      // Something else brought it back before the retry ran.
      connection.state = state
      await vi.advanceTimersByTimeAsync(60_000)

      expect(mockStart).toHaveBeenCalledTimes(1)
    })

    it('schedules one restart for two close events', async () => {
      await mountConnected()

      close()
      await vi.advanceTimersByTimeAsync(2_000)
      close()

      // The second event starts the 5 seconds again.
      await vi.advanceTimersByTimeAsync(4_999)
      expect(mockStart).toHaveBeenCalledTimes(1)
      await vi.advanceTimersByTimeAsync(60_000)
      expect(mockStart).toHaveBeenCalledTimes(2)
    })

    it('is not started again when it closes after unmount', async () => {
      const { unmount } = await mountConnected()

      unmount()
      close()
      await vi.advanceTimersByTimeAsync(60_000)

      expect(mockStart).toHaveBeenCalledTimes(1)
    })

    it('unmount cancels the restart a close scheduled', async () => {
      const { unmount } = await mountConnected()

      close()
      await vi.advanceTimersByTimeAsync(1_000)
      unmount()
      await vi.advanceTimersByTimeAsync(60_000)

      expect(mockStart).toHaveBeenCalledTimes(1)
    })
  })

  describe('access token', () => {
    it('a refreshed token neither stops nor rebuilds the connection', async () => {
      const { rerender } = renderHook(() => useNotificationHub())
      await connected()
      const statusChanged = vi.fn()
      const off = realtimeBus.onStatus(statusChanged)

      auth.accessToken = 'refreshed-token'
      rerender()
      off()

      expect(mockBuild).toHaveBeenCalledTimes(1)
      expect(mockStart).toHaveBeenCalledTimes(1)
      expect(mockStop).not.toHaveBeenCalled()
      expect(statusChanged).not.toHaveBeenCalled()
      expect(realtimeBus.getStatus()).toBe('connected')
    })

    it('the connection reads the token of the moment it asks for it', async () => {
      const { rerender } = renderHook(() => useNotificationHub())
      await connected()
      const { accessTokenFactory } = mockWithUrl.mock.calls[0][1] as { accessTokenFactory: () => string }
      expect(accessTokenFactory()).toBe('test-token')

      auth.accessToken = 'refreshed-token'
      rerender()

      expect(accessTokenFactory()).toBe('refreshed-token')
    })

    it('losing the token stops the connection, and a new token builds a new one', async () => {
      const { rerender } = renderHook(() => useNotificationHub())
      await connected()

      auth.accessToken = null
      rerender()

      expect(mockStop).toHaveBeenCalledTimes(1)
      expect(realtimeBus.getStatus()).toBe('disconnected')
      expect(mockBuild).toHaveBeenCalledTimes(1)

      auth.accessToken = 'next-token'
      rerender()

      expect(mockBuild).toHaveBeenCalledTimes(2)
      expect(mockStart).toHaveBeenCalledTimes(2)
      await connected()
    })

    it('does not connect without a token', () => {
      auth.accessToken = null

      renderHook(() => useNotificationHub())

      expect(mockBuild).not.toHaveBeenCalled()
      expect(mockStart).not.toHaveBeenCalled()
    })
  })
})
