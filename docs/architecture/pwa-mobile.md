# PWA And Mobile

How the installable web app behaves on each platform, how a push notification travels from the backend to a screen, and what the hosting must provide. Written for round 1 of the manager mobile hub ([brainstorm](../brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md), [plan](../plans/archived/261014-2227-pwa-push-ios-foundation/SUMMARY.md)).

## Platform Matrix

| | Chrome Android | Chrome / Edge desktop | iOS Safari, not installed | iOS, installed (16.4+) |
|---|---|---|---|---|
| Install | `beforeinstallprompt` → `InstallPrompt` banner | `beforeinstallprompt` → `InstallPrompt` banner | Manual: Share → Add to Home Screen; the push banner explains it (`ios-needs-install`) | Already installed |
| Web push | Yes | Yes | No (no `Notification` API) | Yes, permission asked from the "Bật thông báo" button |
| Service worker update banner | Yes (`registerType: 'prompt'`, "Có phiên bản mới") | Yes | Yes | Yes |
| App shell cache (precache) | Yes | Yes | Yes | Yes |
| API cache | Allow-list only | Allow-list only | Allow-list only | Allow-list only |

## iOS Limits

- Web push needs iOS 16.4 or later **and** the app running standalone (opened from the home screen). The permission must be requested from a user gesture: the "Bật thông báo" button does that.
- iOS may clear storage and caches of an app not opened for about 7 days. There is no Background Sync (a non-goal).
- A push that does not show a notification can make iOS revoke the subscription, so the service worker always shows one.
- `index.html` sets `viewport-fit=cover` and `apple-mobile-web-app-status-bar-style=default`: content never sits under the status bar, but the notch (landscape) and the home indicator still overlap the page. Safe-area padding:
  - Utilities `pt-safe` / `pb-safe` / `pl-safe` / `pr-safe` in `src/index.css` (`env(safe-area-inset-*)`, 0 on desktop and Android).
  - `AppShell` pads left, right and bottom, which covers the header, the push banner, the sticky form bars and every page.
  - The mobile drawer (`fixed`, outside the shell) uses `pt-safe pb-safe pl-safe`.
  - Elements with their own spacing keep it as a minimum with `max(<spacing>, env(safe-area-inset-*))`: the mobile search sheet, the "Có phiên bản mới" popup and the phone toast viewport.
- The home screen icon is `apple-touch-icon` 180×180 from `GET /api/settings/branding/icon/180`. The renderer paints a solid background (white behind a logo, blue for the placeholder), so iOS never fills transparent pixels with black.

## Push Flow

1. A service calls `NotificationService.SendAsync` (today only `QuotationService`, on confirm, accountant confirm and cancel). It stores the notification and sends it over SignalR.
2. `PushSenderService` sends Web Push (VAPID) to every subscription of the user with the JSON payload `{ title, body, url }`. HTTP 410 deletes the subscription.
3. The service worker `push` listener (`src/sw.ts`) parses the payload with `parsePushPayload` (`src/lib/sw-push.ts`; bad or empty payloads fall back to the default title and `/`) and calls `showNotification` with the static same-origin icon `/icons/icon-192.png`, `tag = payload.tag ?? payload.url` and `renotify: true`.
   - `tag` is optional and unused by the backend so far; round 3 sends it. Notifications with the same tag replace each other and still sound or vibrate thanks to `renotify` (Safari ignores the flag).
   - The notification shows even while the app is focused, so it can duplicate the SignalR toast. That is accepted.
4. `notificationclick` closes the notification, reduces its url to a same-origin path with `resolveTargetUrl` (another origin, a non-http(s) scheme or a bad URL becomes `/`) and calls `focusOrOpenWindow`: it focuses the first open window and posts `{ type: 'NAVIGATE', url }`, or opens a new window when none is open.
5. `SwNavigateListener` (mounted inside `BrowserRouter` in `App.tsx`) receives `NAVIGATE` and calls `navigate(url)`: SPA navigation, no reload, an open form keeps its input. It ignores any other message and any url that is not a `/` path.

All service worker logic lives in pure modules (`sw-routes.ts`, `sw-push.ts`) with unit tests. `sw.ts` itself is not type-checked (`tsconfig.app.json` excludes it) and only wires listeners.

## Prompts

`usePushNotification` reports one state; `isPushPromptVisible` decides whether `App.tsx` gives the shell a banner.

| State | Meaning | Banner |
|---|---|---|
| `idle` | Can subscribe | "Bật thông báo" + "Để sau" |
| `loading` | Asking permission / subscribing | Disabled button |
| `error` | Subscribe failed | "Thử lại" + "Để sau" |
| `ios-needs-install` | iOS (iPhone, iPad, iPadOS with a desktop UA) not running standalone | Install instructions + "Để sau"; `subscribe` does nothing |
| `granted` | Subscribed | None |
| `denied` | Permission denied | None |
| `unsupported` | No Notification / service worker, or no VAPID key | None |

"Để sau" hides the banner for 30 days (`localStorage` key `push_prompt_dismissed_until`).

## API Cache Allow-List

The service worker's `NetworkFirst` cache keys on the URL only; `X-Branch-Id` and `Authorization` are not part of the key. A cached branch-, user- or permission-scoped response would be served to another branch or user on a slow network. So only reference data that is the same for every signed-in user is cached, through the allow-list `CACHEABLE_API_PREFIXES` in `src/lib/sw-routes.ts`. Never add a branch- or user-scoped path there.

## Hosting

- Frontend: Caddy (`frontend/Caddyfile`). `/index.html`, `/sw.js` and `/manifest.webmanifest` are `no-cache`, so an installed app always picks up a new worker (and its push handler); hashed assets under `/assets/` are cached for a year.
- Production serves the frontend and the API from different origins, and Caddy does not proxy `/api`. The manifest icons, the favicon and the `apple-touch-icon` therefore use the absolute API base `VITE_API_BASE_URL` through `brandingIconUrl` / `iconLinkTags` (`src/lib/pwa-icons.ts`, injected by `vite.config.ts`). Never put a relative `/api/...` URL in `index.html`.
- VAPID: the backend reads `Vapid:PublicKey`, `Vapid:PrivateKey` and `Vapid:Subject` (`VapidOptions`). The frontend reads `VITE_VAPID_PUBLIC_KEY` at build time; `frontend/Dockerfile` declares it as a build `ARG`, and the Railway frontend service must set it to the public key of the backend's pair. Without it the hook reports `unsupported` and no banner shows.

## Manual Test Checklist

1. Chrome desktop (`npm run dev` + backend): enable notifications; change a quotation's status with another account. The notification shows with the app open and with the tab closed; clicking it opens the quotation, without a reload when the app is open (a form being edited keeps its input).
2. Chrome Android (deployed build or an HTTPS tunnel), app installed: as in 1.
3. iPhone iOS 16.4+, Safari, not installed: the install instructions banner shows; "Để sau" hides it for 30 days.
4. iPhone, installed: the home screen icon is the branding logo without a black background; the header, drawer, search sheet, update popup and toasts are not covered by the notch or the home indicator (also in landscape); enable notifications; a push arrives; clicking it opens the right page.
5. Production, after setting `VITE_VAPID_PUBLIC_KEY` on Railway: DevTools → Network shows `Cache-Control: no-cache` on `/sw.js` and `/manifest.webmanifest`; the favicon shows in the browser tab; the "Bật thông báo" banner shows (not `unsupported`).
