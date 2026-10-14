import { precacheAndRoute, cleanupOutdatedCaches } from 'workbox-precaching'
import { registerRoute } from 'workbox-routing'
import { NetworkFirst } from 'workbox-strategies'
import { ExpirationPlugin } from 'workbox-expiration'
import { isCacheableApiPath } from './lib/sw-routes'
import { focusOrOpenWindow, notificationTag, parsePushPayload, resolveTargetUrl } from './lib/sw-push'

declare const self: ServiceWorkerGlobalScope

precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

// Only allow-listed reference data is cached; every other API GET goes straight to the network.
registerRoute(
  ({ url }) => isCacheableApiPath(url.pathname),
  new NetworkFirst({
    cacheName: 'api-cache',
    networkTimeoutSeconds: 8,
    plugins: [new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 86400 })],
  }),
  'GET'
)

self.addEventListener('message', (event: ExtendableMessageEvent) => {
  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting()
  }
})

// Every push must show a notification, even while the app is focused: Chrome requires it for
// userVisibleOnly subscriptions and iOS revokes the subscription of a service worker that does not.
// The icon is the static file on this origin; the branding endpoint lives on the API origin in production.
self.addEventListener('push', (event: PushEvent) => {
  const payload = parsePushPayload(event.data?.text())
  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      icon: '/icons/icon-192.png',
      badge: '/icons/icon-192.png',
      tag: notificationTag(payload),
      // A replacement with the same tag is silent in Chrome without renotify; Safari ignores it.
      renotify: true,
      data: { url: payload.url },
    })
  )
})

// Posts NAVIGATE to an open window (SPA navigation, no reload, an open form keeps its input) instead of
// client.navigate(); SwNavigateListener in App.tsx handles it.
self.addEventListener('notificationclick', (event: NotificationEvent) => {
  event.notification.close()
  const url = resolveTargetUrl(event.notification.data?.url, self.location.origin)
  event.waitUntil(focusOrOpenWindow(self.clients, url))
})
