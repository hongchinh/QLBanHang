import { precacheAndRoute, cleanupOutdatedCaches } from 'workbox-precaching'
import { registerRoute } from 'workbox-routing'
import { NetworkFirst } from 'workbox-strategies'
import { ExpirationPlugin } from 'workbox-expiration'
import { isCacheableApiPath } from './lib/sw-routes'

declare const self: ServiceWorkerGlobalScope

precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

// Branch- or user-scoped API GETs are not routed here, so they go straight to the network.
registerRoute(
  ({ url }) => url.pathname.startsWith('/api/') && isCacheableApiPath(url.pathname),
  new NetworkFirst({
    cacheName: 'api-cache',
    networkTimeoutSeconds: 8,
    plugins: [new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 86400 })],
  }),
  'GET'
)

self.addEventListener('message', (event: any) => {
  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting()
  }
})
