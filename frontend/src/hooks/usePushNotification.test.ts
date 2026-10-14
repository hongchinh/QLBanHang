import { renderHook, act } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { usePushNotification } from './usePushNotification'

vi.mock('@/features/push/api', () => ({
  pushApi: {
    subscribe: vi.fn().mockResolvedValue(undefined),
    unsubscribe: vi.fn().mockResolvedValue(undefined),
  },
}))

// Stub browser APIs
function stubNotificationPermission(permission: NotificationPermission) {
  Object.defineProperty(Notification, 'permission', { value: permission, configurable: true })
}

function stubPushManager(subscribeResult: PushSubscription | null = null) {
  const mockPushManager = {
    getSubscription: vi.fn().mockResolvedValue(subscribeResult),
    subscribe: vi.fn().mockResolvedValue({
      endpoint: 'https://push.example.com/sub/1',
      toJSON: () => ({
        endpoint: 'https://push.example.com/sub/1',
        keys: { p256dh: 'key123', auth: 'auth123' },
      }),
    } as unknown as PushSubscription),
  }
  Object.defineProperty(navigator, 'serviceWorker', {
    value: {
      ready: Promise.resolve({
        pushManager: mockPushManager,
      }),
    },
    configurable: true,
  })
  return mockPushManager
}

describe('usePushNotification', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'Notification', {
      value: { permission: 'default', requestPermission: vi.fn().mockResolvedValue('granted') },
      configurable: true,
    })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('state là idle khi permission là default', () => {
    stubNotificationPermission('default')
    stubPushManager()
    const { result } = renderHook(() => usePushNotification('/vapid-key'))
    expect(result.current.state).toBe('idle')
  })

  it('state là denied khi Notification.permission là denied', () => {
    stubNotificationPermission('denied')
    const { result } = renderHook(() => usePushNotification('/vapid-key'))
    expect(result.current.state).toBe('denied')
  })

  it('unsupported khi không có Notification API', () => {
    const originalNotification = window.Notification
    // @ts-expect-error intentional
    delete window.Notification
    const { result } = renderHook(() => usePushNotification('/vapid-key'))
    expect(result.current.state).toBe('unsupported')
    window.Notification = originalNotification
  })

  it('subscribe chuyển state sang granted', async () => {
    stubNotificationPermission('default')
    stubPushManager()
    const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))

    await act(async () => {
      await result.current.subscribe()
    })

    expect(result.current.state).toBe('granted')
  })

  it('unsubscribe chuyển state về idle và gọi pushApi.unsubscribe', async () => {
    const pushApiMod = await import('@/features/push/api')
    vi.mocked(pushApiMod.pushApi.unsubscribe).mockResolvedValue(undefined)

    // mockUnsubscribe also removes the sub from the mock manager so the
    // useEffect that re-runs on state==='idle' won't find it and flip back to 'granted'.
    const pushManager = stubPushManager(null)
    const mockUnsubscribeFn = vi.fn().mockImplementation(async () => {
      pushManager.getSubscription.mockResolvedValue(null)
      return true
    })
    const existingSub = {
      endpoint: 'https://push.example.com/sub/existing',
      unsubscribe: mockUnsubscribeFn,
    } as unknown as PushSubscription
    pushManager.getSubscription.mockResolvedValue(existingSub)

    stubNotificationPermission('granted')

    const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))

    // Initial state is 'granted' since Notification.permission === 'granted'
    expect(result.current.state).toBe('granted')

    await act(async () => {
      await result.current.unsubscribe()
    })

    expect(result.current.state).toBe('idle')
    expect(mockUnsubscribeFn).toHaveBeenCalled()
    expect(pushApiMod.pushApi.unsubscribe).toHaveBeenCalledWith('https://push.example.com/sub/existing')
  })
})

describe('usePushNotification on iOS', () => {
  const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1'

  function stubIos(standalone: boolean) {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(IPHONE)
    // jsdom has no maxTouchPoints at all, so spyOn would throw: define it.
    Object.defineProperty(navigator, 'maxTouchPoints', { value: 5, configurable: true })
    Object.defineProperty(navigator, 'standalone', { value: standalone, configurable: true })
    vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }))
  }

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    delete (navigator as { standalone?: boolean }).standalone
    delete (navigator as { maxTouchPoints?: number }).maxTouchPoints
  })

  it('asks Safari users to install the app first, even without the Notification API', () => {
    stubIos(false)
    const originalNotification = window.Notification
    // @ts-expect-error Safari on iOS has no Notification API until the app is installed
    delete window.Notification
    try {
      const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
      expect(result.current.state).toBe('ios-needs-install')
    } finally {
      window.Notification = originalNotification
    }
  })

  it('uses the normal flow once installed', () => {
    stubIos(true)
    Object.defineProperty(window, 'Notification', {
      value: { permission: 'default', requestPermission: vi.fn() },
      configurable: true,
    })
    stubPushManager()
    const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
    expect(result.current.state).toBe('idle')
  })

  it('does not subscribe while the app is not installed', async () => {
    stubIos(false)
    const requestPermission = vi.fn()
    Object.defineProperty(window, 'Notification', {
      value: { permission: 'default', requestPermission },
      configurable: true,
    })
    const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
    await act(async () => { await result.current.subscribe() })
    expect(requestPermission).not.toHaveBeenCalled()
    expect(result.current.state).toBe('ios-needs-install')
  })
})
