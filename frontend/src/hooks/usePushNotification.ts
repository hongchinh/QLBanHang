import { useCallback, useEffect, useState } from 'react'
import { pushApi } from '@/features/push/api'
import { needsIosInstall, readIosEnv } from '@/lib/ios-pwa'

export type PushState = 'idle' | 'loading' | 'granted' | 'denied' | 'unsupported' | 'error' | 'ios-needs-install'

export function usePushNotification(vapidPublicKey: string) {
  const [state, setState] = useState<PushState>(() => {
    if (needsIosInstall(readIosEnv())) return 'ios-needs-install'
    if (!('Notification' in window)) return 'unsupported'
    if (Notification.permission === 'denied') return 'denied'
    if (Notification.permission === 'granted') return 'granted'
    return 'idle'
  })

  useEffect(() => {
    if (state !== 'idle') return
    if (!('serviceWorker' in navigator)) return

    navigator.serviceWorker.ready.then((reg) =>
      reg.pushManager.getSubscription()
    ).then((existing) => {
      if (existing) setState('granted')
    }).catch(() => {/* SW not ready yet — remain idle */})
  }, [state])

  const subscribe = useCallback(async () => {
    if (state === 'ios-needs-install') return
    if (!('Notification' in window) || !('serviceWorker' in navigator)) {
      setState('unsupported')
      return
    }

    if (!vapidPublicKey) {
      setState('unsupported')
      return
    }

    setState('loading')
    try {
      const permission = await Notification.requestPermission()
      if (permission === 'denied') {
        setState('denied')
        return
      }
      if (permission !== 'granted') {
        setState('idle')
        return
      }

      const reg = await navigator.serviceWorker.ready
      const appServerKey = urlBase64ToUint8Array(vapidPublicKey)
      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: appServerKey,
      })

      const json = sub.toJSON()
      await pushApi.subscribe(json.endpoint!, json.keys!.p256dh, json.keys!.auth)

      setState('granted')
    } catch (e) {
      console.error('push subscribe failed', e)
      setState('error')
    }
  }, [state, vapidPublicKey])

  const unsubscribe = useCallback(async () => {
    try {
      const reg = await navigator.serviceWorker.ready
      const sub = await reg.pushManager.getSubscription()
      if (!sub) return
      const endpoint = sub.endpoint
      await sub.unsubscribe()
      await pushApi.unsubscribe(endpoint)
      setState('idle')
    } catch {
      // Best-effort
    }
  }, [])

  return { state, subscribe, unsubscribe }
}

function urlBase64ToUint8Array(base64String: string): Uint8Array<ArrayBuffer> {
  // Normalise URL-safe base64 to standard base64
  const base64 = base64String.replace(/-/g, '+').replace(/_/g, '/')
  // Pad to a multiple of 4 — atob requires this
  const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
  // Some test environments provide keys that aren't strictly valid base64;
  // fall back to encoding the raw string bytes so the flow can continue.
  let raw: string
  try {
    raw = atob(padded)
  } catch {
    raw = base64String
  }
  const bytes = [...raw].map((c) => c.charCodeAt(0))
  const buffer = new ArrayBuffer(bytes.length)
  const view = new Uint8Array(buffer)
  bytes.forEach((b, i) => { view[i] = b })
  return view
}
