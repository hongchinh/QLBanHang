import { useEffect } from 'react'
import { HubConnectionBuilder } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { executiveKeys } from '@/features/executive-dashboard/keys'
import { realtimeBus } from '@/lib/realtime-bus'
import type { DashboardChangedSignal, RealtimeStatus } from '@/lib/realtime-bus'
import { useAuthStore } from '@/stores/auth-store'

const START_RETRY_MS = 5_000

export function useNotificationHub() {
  const queryClient = useQueryClient()
  const accessToken = useAuthStore((s) => s.accessToken)

  useEffect(() => {
    if (!accessToken) return

    const apiBase = import.meta.env.VITE_API_BASE_URL ?? import.meta.env.VITE_API_BASE ?? ''
    const hubUrl = apiBase
      ? apiBase.replace(/\/api$/, '') + '/hubs/notifications'
      : '/hubs/notifications'

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
      })
      // Never gives up (the default policy stops after four attempts): 2, 4, 6... seconds, then every 30.
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => Math.min(30_000, 2_000 * (ctx.previousRetryCount + 1)),
      })
      .build()

    // Cleared in the cleanup: a late callback of this connection must not change the status
    // that the connection replacing it reports, nor start it again.
    let active = true
    let retryTimer: ReturnType<typeof setTimeout> | undefined
    const setStatus = (status: RealtimeStatus) => {
      if (active) realtimeBus.setStatus(status)
    }

    connection.on('NewNotification', () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
    })

    connection.on('DashboardChanged', (signal: DashboardChangedSignal) => {
      realtimeBus.emitDashboardChanged(signal)
      // An open dashboard refetches through the bus. One that is not open only gets its queries
      // marked stale, so it refetches when it is opened again.
      queryClient.invalidateQueries({ queryKey: executiveKeys.all, refetchType: 'none' })
    })

    connection.onreconnecting(() => setStatus('disconnected'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    // Automatic reconnect does not cover a start() that fails: try again every 5 seconds.
    const start = () => {
      connection.start().then(
        () => setStatus('connected'),
        (err) => {
          console.error('SignalR connection failed:', err)
          if (!active) return
          realtimeBus.setStatus('disconnected')
          retryTimer = setTimeout(start, START_RETRY_MS)
        },
      )
    }

    realtimeBus.setStatus('connecting')
    start()

    return () => {
      active = false
      clearTimeout(retryTimer)
      realtimeBus.setStatus('disconnected')
      connection.stop()
    }
  }, [accessToken, queryClient])
}
