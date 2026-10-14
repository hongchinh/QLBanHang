import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { NAVIGATE_MESSAGE } from '@/lib/sw-push'

// The service worker asks the open window to show a notification's page (notificationclick). Navigating
// inside the SPA keeps the app state; the worker already reduced the url to a same-origin path.
export function SwNavigateListener() {
  const navigate = useNavigate()

  useEffect(() => {
    if (!('serviceWorker' in navigator)) return
    const container = navigator.serviceWorker
    const onMessage = (event: MessageEvent) => {
      const data = event.data as { type?: unknown; url?: unknown } | null
      if (data?.type !== NAVIGATE_MESSAGE) return
      if (typeof data.url !== 'string' || !data.url.startsWith('/') || data.url.startsWith('//')) return
      navigate(data.url)
    }
    container.addEventListener('message', onMessage)
    return () => container.removeEventListener('message', onMessage)
  }, [navigate])

  return null
}
