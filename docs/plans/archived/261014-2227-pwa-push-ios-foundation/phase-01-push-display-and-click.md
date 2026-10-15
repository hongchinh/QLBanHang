# Phase 01 — Push hiển thị và bấm mở đúng trang

**Status:** [x] complete
**Complexity:** M

## Objective

Service worker hiện mọi push nhận được và, khi bấm, đưa người dùng tới đúng trang: focus cửa sổ app đang mở và điều hướng SPA qua thông điệp `NAVIGATE`, hoặc mở cửa sổ mới nếu không có. Mọi logic nằm trong module thuần `lib/sw-push.ts` có test; `sw.ts` chỉ nối dây.

Hiện trạng: backend gửi payload `{ title, body, url }` ([PushSenderService.cs](../../../backend/src/OrderMgmt.Infrastructure/Notifications/PushSenderService.cs), gọi từ `NotificationService.SendAsync`), nhưng [sw.ts](../../../frontend/src/sw.ts) chỉ có listener `message` (`SKIP_WAITING`).

## Files

- `frontend/src/lib/sw-push.ts` (mới)
- `frontend/src/lib/sw-push.test.ts` (mới)
- `frontend/src/sw.ts`
- `frontend/src/components/sw-navigate-listener.tsx` (mới)
- `frontend/src/components/sw-navigate-listener.test.tsx` (mới)
- `frontend/src/App.tsx`

Mọi lệnh chạy từ `frontend`.

## Tasks

### Task 1 — `parsePushPayload` và `notificationTag`

Hợp đồng:

```ts
export const DEFAULT_PUSH_TITLE = 'QL Đơn Hàng'

export interface PushPayload {
  title: string
  body: string
  url: string
  tag?: string
}

// JSON object → mỗi trường lấy khi là string (title rỗng → mặc định; body thiếu → ''; url thiếu/rỗng → '/';
// tag chỉ có khi là string khác rỗng). Không phải JSON, hoặc JSON không phải object (số, mảng, null)
// → { title: DEFAULT_PUSH_TITLE, body: text ?? '', url: '/' }.
export function parsePushPayload(text: string | null | undefined): PushPayload

// Khóa gộp thông báo: tag khi có, ngược lại url (hai cập nhật của cùng một báo giá thay nhau, không chồng).
export function notificationTag(payload: PushPayload): string
```

1. **Viết test thất bại** — tạo `src/lib/sw-push.test.ts`:

   ```ts
   import { describe, expect, it } from 'vitest'
   import { DEFAULT_PUSH_TITLE, notificationTag, parsePushPayload } from './sw-push'

   describe('parsePushPayload', () => {
     it('reads title, body and url from the backend payload', () => {
       expect(parsePushPayload('{"title":"Báo giá BG-001","body":"Đã xác nhận","url":"/quotations/1"}'))
         .toEqual({ title: 'Báo giá BG-001', body: 'Đã xác nhận', url: '/quotations/1' })
     })

     it('keeps a non-empty tag', () => {
       expect(parsePushPayload('{"title":"t","body":"b","url":"/x","tag":"fund:1"}').tag).toBe('fund:1')
     })

     it.each([
       ['{"title":"t","body":"b","url":"/x","tag":""}'],
       ['{"title":"t","body":"b","url":"/x","tag":5}'],
     ])('drops an empty or non-string tag (%s)', (text) => {
       expect(parsePushPayload(text)).not.toHaveProperty('tag')
     })

     it('fills missing or non-string fields with defaults', () => {
       expect(parsePushPayload('{"body":3}')).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
       expect(parsePushPayload('{"title":"","url":""}')).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
     })

     it.each(['not json', '42', '[1,2]', 'null'])('treats %s as a plain-text body', (text) => {
       expect(parsePushPayload(text)).toEqual({ title: DEFAULT_PUSH_TITLE, body: text, url: '/' })
     })

     it.each([null, undefined, ''])('handles an empty push (%s)', (text) => {
       expect(parsePushPayload(text)).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
     })
   })

   describe('notificationTag', () => {
     it('uses the tag when there is one', () => {
       expect(notificationTag({ title: 't', body: 'b', url: '/x', tag: 'fund:1' })).toBe('fund:1')
     })

     it('falls back to the url', () => {
       expect(notificationTag({ title: 't', body: 'b', url: '/quotations/1' })).toBe('/quotations/1')
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/lib/sw-push.test.ts` / Expected: FAIL, `Failed to resolve import "./sw-push"`.

3. **Viết code tối thiểu** — tạo `src/lib/sw-push.ts`, mở đầu bằng chú thích như `sw-routes.ts`:

   ```ts
   // Imported by sw.ts; kept free of DOM/service-worker globals so it is testable.

   export const DEFAULT_PUSH_TITLE = 'QL Đơn Hàng'

   export interface PushPayload { title: string; body: string; url: string; tag?: string }

   const str = (value: unknown): string | undefined =>
     typeof value === 'string' && value !== '' ? value : undefined

   export function parsePushPayload(text: string | null | undefined): PushPayload {
     const fallback: PushPayload = { title: DEFAULT_PUSH_TITLE, body: text ?? '', url: '/' }
     if (!text) return fallback
     let data: unknown
     try {
       data = JSON.parse(text)
     } catch {
       return fallback
     }
     if (typeof data !== 'object' || data === null || Array.isArray(data)) return fallback
     const record = data as Record<string, unknown>
     const payload: PushPayload = {
       title: str(record.title) ?? DEFAULT_PUSH_TITLE,
       body: typeof record.body === 'string' ? record.body : '',
       url: str(record.url) ?? '/',
     }
     const tag = str(record.tag)
     if (tag) payload.tag = tag
     return payload
   }

   export function notificationTag(payload: PushPayload): string {
     return payload.tag ?? payload.url
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/lib/sw-push.test.ts` / Expected: PASS.

5. **Commit** — `git add src/lib/sw-push.ts src/lib/sw-push.test.ts && git commit -m "feat(pwa): parse push payloads with an optional tag"`

### Task 2 — `resolveTargetUrl`

Hợp đồng:

```ts
// Đường dẫn cùng origin (pathname + search + hash) mà notificationclick được phép mở.
// Thiếu url, khác origin, protocol không phải http(s) (javascript:, data:…) hoặc URL lỗi → '/'.
export function resolveTargetUrl(url: string | undefined, origin: string): string
```

1. **Viết test thất bại** — thêm vào `src/lib/sw-push.test.ts` (sửa import thành `{ DEFAULT_PUSH_TITLE, notificationTag, parsePushPayload, resolveTargetUrl }`):

   ```ts
   describe('resolveTargetUrl', () => {
     const origin = 'https://app.example.com'

     it.each([
       ['/quotations/1', '/quotations/1'],
       ['/reports/cash-book?fundId=2#top', '/reports/cash-book?fundId=2#top'],
       ['quotations/1', '/quotations/1'],
       ['https://app.example.com/customers?q=a', '/customers?q=a'],
     ])('keeps same-origin %s as %s', (url, expected) => {
       expect(resolveTargetUrl(url, origin)).toBe(expected)
     })

     it.each([
       'https://evil.example.com/phish',
       '//evil.example.com/phish',
       'javascript:alert(1)',
       'data:text/html,hi',
       'http://[bad',
       '',
       undefined,
     ])('falls back to / for %s', (url) => {
       expect(resolveTargetUrl(url, origin)).toBe('/')
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/lib/sw-push.test.ts` / Expected: FAIL, `resolveTargetUrl is not a function` (hoặc lỗi import tương ứng).

3. **Viết code tối thiểu** — thêm vào `src/lib/sw-push.ts`:

   ```ts
   export function resolveTargetUrl(url: string | undefined, origin: string): string {
     if (!url) return '/'
     try {
       const target = new URL(url, origin)
       if (target.origin !== origin) return '/'
       if (target.protocol !== 'https:' && target.protocol !== 'http:') return '/'
       return target.pathname + target.search + target.hash
     } catch {
       return '/'
     }
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/lib/sw-push.test.ts` / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): keep notification targets on the app origin"`

### Task 3 — `focusOrOpenWindow` và `NAVIGATE_MESSAGE`

Hợp đồng (kiểu tối giản để test bằng đối tượng giả, `self.clients` của SW thỏa mãn):

```ts
export const NAVIGATE_MESSAGE = 'NAVIGATE'

export interface NavigateMessage { type: typeof NAVIGATE_MESSAGE; url: string }

export interface WindowClientLike {
  focus(): Promise<unknown>
  postMessage(message: NavigateMessage): void
}

export interface ClientsLike {
  matchAll(options: { type: 'window'; includeUncontrolled: boolean }): Promise<readonly WindowClientLike[]>
  openWindow(url: string): Promise<unknown>
}

// Có cửa sổ app → focus cửa sổ đầu tiên và gửi NAVIGATE (điều hướng SPA, không tải lại).
// Không có → mở cửa sổ mới tại url.
export async function focusOrOpenWindow(clients: ClientsLike, url: string): Promise<void>
```

1. **Viết test thất bại** — thêm vào `src/lib/sw-push.test.ts` (thêm `vi` vào import từ `vitest`, thêm `focusOrOpenWindow, NAVIGATE_MESSAGE` vào import từ `./sw-push`):

   ```ts
   describe('focusOrOpenWindow', () => {
     function fakeClients(windowCount: number) {
       const windows = Array.from({ length: windowCount }, () => ({
         focus: vi.fn().mockResolvedValue(undefined),
         postMessage: vi.fn(),
       }))
       return {
         windows,
         clients: {
           matchAll: vi.fn().mockResolvedValue(windows),
           openWindow: vi.fn().mockResolvedValue(null),
         },
       }
     }

     it('focuses the first open window and asks it to navigate', async () => {
       const { windows, clients } = fakeClients(2)
       await focusOrOpenWindow(clients, '/quotations/1')
       expect(clients.matchAll).toHaveBeenCalledWith({ type: 'window', includeUncontrolled: true })
       expect(windows[0].focus).toHaveBeenCalledOnce()
       expect(windows[0].postMessage).toHaveBeenCalledWith({ type: NAVIGATE_MESSAGE, url: '/quotations/1' })
       expect(windows[1].focus).not.toHaveBeenCalled()
       expect(clients.openWindow).not.toHaveBeenCalled()
     })

     it('opens a new window when the app is not open', async () => {
       const { clients } = fakeClients(0)
       await focusOrOpenWindow(clients, '/quotations/1')
       expect(clients.openWindow).toHaveBeenCalledWith('/quotations/1')
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/lib/sw-push.test.ts` / Expected: FAIL, `focusOrOpenWindow is not a function`.

3. **Viết code tối thiểu** — thêm các khai báo trong hợp đồng ở trên vào `src/lib/sw-push.ts`, với thân hàm:

   ```ts
   export async function focusOrOpenWindow(clients: ClientsLike, url: string): Promise<void> {
     const windows = await clients.matchAll({ type: 'window', includeUncontrolled: true })
     const client = windows[0]
     if (client) {
       await client.focus()
       client.postMessage({ type: NAVIGATE_MESSAGE, url })
       return
     }
     await clients.openWindow(url)
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/lib/sw-push.test.ts` / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): focus the open app window or open one on notification click"`

### Task 4 — Nối listener `push` và `notificationclick` trong `sw.ts`

`sw.ts` cần `ServiceWorkerGlobalScope`, không chạy được trong Vitest; logic đã được test ở Task 1–3. Task này chỉ nối dây, kiểm bằng typecheck và build (ngoại lệ TDD có lý do: không có runner cho SW global scope).

1. **Kiểm trạng thái trước** — `npm run build && grep -c "notificationclick" dist/sw.js` / Expected: `0`.

2. **Viết code** — trong `src/sw.ts`, thêm import và hai listener sau listener `message` hiện có:

   ```ts
   import { focusOrOpenWindow, notificationTag, parsePushPayload, resolveTargetUrl } from './lib/sw-push'

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
   ```

   `sw.ts` không được kiểm kiểu: `tsconfig.app.json` loại trừ nó, không tsconfig nào có lib WebWorker, và Vite biên dịch worker mà không kiểm kiểu. Kiểm tra của task này là test `sw-push` (Task 1–3), build + grep và thử tay. (`Clients`/`WindowClient` thỏa mãn `ClientsLike` về cấu trúc.)

3. **Typecheck phần còn lại** — `npm run typecheck` / Expected: không lỗi (đảm bảo `lib/sw-push.ts` vẫn đúng kiểu).

4. **Kiểm build** — `npm run build && grep -c "notificationclick" dist/sw.js` / Expected: `1` trở lên.

5. **Commit** — `git commit -am "feat(pwa): show push notifications and open their page on click"`

### Task 5 — `SwNavigateListener` trong router

Component render `null`, đặt bên trong `BrowserRouter` (vì `App` đứng ngoài router nên không dùng `useNavigate` trực tiếp được). Nó nghe `navigator.serviceWorker` sự kiện `message`; khi `data.type === NAVIGATE_MESSAGE` và `data.url` là string bắt đầu bằng `/` thì gọi `navigate(data.url)`. Không có `navigator.serviceWorker` thì không làm gì. Gỡ listener khi unmount.

1. **Viết test thất bại** — tạo `src/components/sw-navigate-listener.test.tsx`:

   ```tsx
   import { act, render, screen } from '@testing-library/react'
   import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
   import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
   import { NAVIGATE_MESSAGE } from '@/lib/sw-push'
   import { SwNavigateListener } from './sw-navigate-listener'

   function Where() {
     const { pathname, search } = useLocation()
     return <div data-testid="where">{pathname + search}</div>
   }

   function renderAt(path: string) {
     return render(
       <MemoryRouter initialEntries={[path]}>
         <SwNavigateListener />
         <Routes>
           <Route path="*" element={<Where />} />
         </Routes>
       </MemoryRouter>
     )
   }

   describe('SwNavigateListener', () => {
     let container: EventTarget
     const original = Object.getOwnPropertyDescriptor(navigator, 'serviceWorker')

     beforeEach(() => {
       container = new EventTarget()
       Object.defineProperty(navigator, 'serviceWorker', { value: container, configurable: true })
     })
     afterEach(() => {
       if (original) Object.defineProperty(navigator, 'serviceWorker', original)
       else delete (navigator as { serviceWorker?: unknown }).serviceWorker
     })

     const post = (data: unknown) =>
       act(() => { container.dispatchEvent(new MessageEvent('message', { data })) })

     it('navigates on a NAVIGATE message from the service worker', () => {
       renderAt('/')
       post({ type: NAVIGATE_MESSAGE, url: '/quotations/7?tab=history' })
       expect(screen.getByTestId('where')).toHaveTextContent('/quotations/7?tab=history')
     })

     it.each([
       { type: 'SKIP_WAITING' },
       { type: NAVIGATE_MESSAGE, url: 'https://evil.example.com' },
       { type: NAVIGATE_MESSAGE, url: 42 },
       null,
     ])('ignores %o', (data) => {
       renderAt('/customers')
       post(data)
       expect(screen.getByTestId('where')).toHaveTextContent('/customers')
     })

     it('stops listening when unmounted', () => {
       const remove = vi.spyOn(container, 'removeEventListener')
       const { unmount } = renderAt('/')
       unmount()
       expect(remove).toHaveBeenCalledWith('message', expect.any(Function))
     })

     it('renders nothing and does not throw without service worker support', () => {
       delete (navigator as { serviceWorker?: unknown }).serviceWorker
       const { container: root } = render(<MemoryRouter><SwNavigateListener /></MemoryRouter>)
       expect(root).toBeEmptyDOMElement()
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/components/sw-navigate-listener.test.tsx` / Expected: FAIL, `Failed to resolve import "./sw-navigate-listener"`.

3. **Viết code tối thiểu** — tạo `src/components/sw-navigate-listener.tsx`:

   ```tsx
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
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/components/sw-navigate-listener.test.tsx` / Expected: PASS.

5. **Commit** — `git add src/components/sw-navigate-listener.tsx src/components/sw-navigate-listener.test.tsx && git commit -m "feat(pwa): navigate the SPA when the service worker asks"`

### Task 6 — Gắn `SwNavigateListener` vào `App.tsx`

1. **Viết test thất bại** — trong `src/App.shell.test.tsx` (file đã mock `AuthInit`, `ProtectedRoute`, `NotFoundPage` và đưa URL về `/` trong `afterEach`), thêm `act` vào import từ `@testing-library/react` và thêm test:

   ```tsx
   // notificationclick posts NAVIGATE to the open window; the app moves inside the SPA, no reload.
   it('follows a NAVIGATE message from the service worker', () => {
     const original = Object.getOwnPropertyDescriptor(navigator, 'serviceWorker');
     const container = new EventTarget();
     Object.defineProperty(navigator, 'serviceWorker', { value: container, configurable: true });
     try {
       window.history.pushState({}, '', '/404');
       render(<App />);
       act(() => {
         container.dispatchEvent(new MessageEvent('message', { data: { type: 'NAVIGATE', url: '/403' } }));
       });
       expect(window.location.pathname).toBe('/403');
       expect(screen.getByText('forbidden')).toBeInTheDocument();
     } finally {
       if (original) Object.defineProperty(navigator, 'serviceWorker', original);
       else delete (navigator as { serviceWorker?: unknown }).serviceWorker;
     }
   });
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/App.shell.test.tsx` / Expected: FAIL, pathname vẫn là đường dẫn ban đầu.

3. **Viết code tối thiểu** — trong `src/App.tsx`: `import { SwNavigateListener } from '@/components/sw-navigate-listener';` và đặt `<SwNavigateListener />` ngay sau `<BrowserRouter>`, trước `<AuthInit>`.

4. **Chạy test, xác nhận đạt** — `npx vitest run src/App.shell.test.tsx src/App.routes.test.tsx` / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): listen for service worker navigation in the app router"`

## Verification

Từ `frontend`:

- `npx vitest run src/lib/sw-push.test.ts src/components/sw-navigate-listener.test.tsx src/App.shell.test.tsx src/App.routes.test.tsx`
- `npm run typecheck` (không phủ `sw.ts`, xem Task 4)
- `npm run lint`
- `npm run test`
- `npm run build && grep -c "notificationclick" dist/sw.js` (≥ 1)
- Thử tay mục 1 của Final Verification trong [SUMMARY.md](SUMMARY.md) (Chrome desktop).

## Exit Criteria

- `sw.ts` có listener `push` và `notificationclick`; mọi push hiện thông báo với `tag` = `payload.tag ?? payload.url`.
- Bấm thông báo: có cửa sổ app → focus + `NAVIGATE`, app điều hướng không tải lại; không có → mở cửa sổ mới; URL khác origin hoặc không phải http(s) → `/`.
- Toàn bộ test frontend, typecheck, lint, build đạt.
