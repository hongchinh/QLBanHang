# Section 01 — Push handler trong service worker

## Hiện trạng

- Backend: `NotificationService.SendAsync` (`backend/src/OrderMgmt.Application/Notifications/Services/NotificationService.cs:37`) lưu notification, bắn SignalR, rồi gọi `IPushSender.SendAsync(userId, title, body, link ?? "/")`.
- `PushSenderService` serialize payload `{ title, body, url }` và gửi qua WebPush (VAPID); HTTP 410 → xóa subscription.
- Frontend: `usePushNotification` subscribe với `userVisibleOnly: true`.
- **Thiếu:** `frontend/src/sw.ts` không có listener `push` / `notificationclick` → push tới nhưng không hiện gì (Chrome có thể hiện thông báo chung "site đã cập nhật trong nền"; iOS sẽ thu hồi subscription nếu push không hiển thị).

## Thiết kế

### `frontend/src/lib/sw-push.ts` (mới, thuần — không DOM/SW API, testable như `sw-routes.ts`)

```ts
export interface PushPayload { title: string; body: string; url: string }

export function parsePushPayload(text: string | null | undefined): PushPayload
// JSON hợp lệ → lấy title/body/url (thiếu trường → mặc định)
// JSON lỗi → { title: 'QL Đơn Hàng', body: text ?? '', url: '/' }

export function resolveTargetUrl(url: string | undefined, origin: string): string
// new URL(url, origin); khác origin, protocol không phải http(s), hoặc lỗi → '/'
// trả về pathname + search + hash (đường dẫn tương đối cùng origin)
```

### `sw.ts`

```ts
self.addEventListener('push', (event) => {
  const data = parsePushPayload(event.data?.text())
  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: '/icons/icon-192.png',
      badge: '/icons/icon-192.png',
      tag: data.url,          // gộp nhiều cập nhật cùng 1 báo giá
      data: { url: data.url },
    })
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = resolveTargetUrl(event.notification.data?.url, self.location.origin)
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    const client = windows[0]
    if (client) {
      await client.focus()
      client.postMessage({ type: 'NAVIGATE', url })
      return
    }
    await self.clients.openWindow(url)
  })())
})
```

Quyết định:
- **Luôn** `showNotification`, kể cả khi app đang focus — bắt buộc với `userVisibleOnly` (Chrome) và iOS. Trùng với toast SignalR là chấp nhận được.
- Icon dùng file tĩnh `/icons/icon-192.png` (cùng origin với SW) thay vì endpoint branding (khác origin trên prod).
- Click → `postMessage` thay vì `client.navigate()` để điều hướng SPA, không reload, không mất form đang nhập.

### `App.tsx`

Thêm effect lắng nghe `navigator.serviceWorker` `'message'`: nếu `data.type === 'NAVIGATE'` → `navigate(data.url)` (react-router). Gỡ listener khi unmount.

## Edge cases

- Payload rỗng/không phải JSON → thông báo mặc định, url `/`.
- `url` trỏ ra ngoài origin hoặc `javascript:` → `/`.
- Người dùng chưa đăng nhập khi bấm thông báo → route guard hiện tại đưa về login (không xử lý thêm).
- Nhiều tab mở → focus tab đầu tiên `matchAll` trả về.

## Kiểm thử

- `lib/sw-push.test.ts`: JSON hợp lệ / lỗi / rỗng / thiếu trường; URL tương đối, cùng origin, khác origin, `javascript:`, rỗng.
- Thủ công: Chrome Android/desktop — push khi app mở, khi app đóng; click điều hướng đúng trang.
