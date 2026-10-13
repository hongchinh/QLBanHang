# PWA — Progressive Web App cho QLBanHang

**Date:** 2026-05-23  
**Status:** Design approved, pending implementation plan

---

## Problem Framing

Project QLBanHang (Vite + React + ASP.NET Core) cần hỗ trợ cài đặt dưới dạng native app trên desktop và mobile, đồng thời hoạt động tốt khi mạng kém hoặc mất mạng, và có khả năng gửi push notifications.

---

## Goals & Non-Goals

**Goals:**
- Installable: hiện nút "Install app" trên Chrome/Edge/Android/iOS
- Static cache: shell HTML/JS/CSS được precache, load nhanh khi reload
- API cache: GET `/api/**` cached với NetworkFirst — hiển thị dữ liệu cũ khi offline
- Push notifications: backend gửi thông báo tới browser kể cả khi app không mở

**Non-Goals:**
- Offline mutations (Background Sync) — user không thể tạo/sửa báo giá khi offline
- iOS Safari push notifications (giới hạn platform, để phase sau)
- Native mobile app (React Native / Capacitor)

---

## Constraints & Assumptions

- Deploy trên Railway/Vercel/Cloud — có HTTPS, PWA hoạt động đầy đủ
- Service Worker chỉ chạy trên HTTPS (hoặc localhost trong dev)
- Icons PNG cần được tạo: 192×192 và 512×512
- Push notifications cần user đồng ý cấp quyền
- Backend cần VAPID keys (generate một lần, lưu vào **environment variable / user-secrets** — không commit vào appsettings.json)

---

## Approaches Considered

| Approach | Tính năng | Độ phức tạp |
|----------|-----------|-------------|
| 1 — Minimal | Installable + static cache | Thấp |
| 2 — Standard | + API runtime caching | Trung bình |
| **3 — Full** | **+ Push notifications** | **Cao** |

**Chọn Approach 3** vì đáp ứng đầy đủ yêu cầu; phân phase để deploy an toàn.

---

## Architecture

Push notification là **delivery channel** của hệ thống Notification hiện có, không phải một system riêng biệt. Pipeline:

```
QuotationService (status change)
    │
    ▼
INotificationService.SendAsync(userId, type, title, body, link)
    ├── persist Notification → DB  (in-app bell icon)
    └── IPushSender.SendAsync(userId, payload)
            │  look up PushSubscription by userId
            │  WebPush NuGet → Push Service (Google/Mozilla)
            │  on HTTP 410 → delete stale subscription from DB
            ▼
       Browser Service Worker
           push event → showNotification()
```

```
┌─────────────────────────────────────────────────────┐
│  Browser                                            │
│  ┌──────────┐   fetch /api/*   ┌─────────────────┐  │
│  │ React    │ ←──────────────→ │ Service Worker  │  │
│  │ App      │                  │ sw.ts           │  │
│  └──────────┘   push event     │  - precache     │  │
│       ↑ ────────────────────── │  - api cache    │  │
│                                │  - push handler │  │
│                                └────────┬────────┘  │
└─────────────────────────────────────────│───────────┘
                                          │ NetworkFirst
                             ┌────────────▼────────────┐
                             │  ASP.NET Core WebApi    │
                             │  INotificationService   │
                             │    → IPushSender        │
                             │  POST /api/push/sub     │
                             │  DELETE /api/push/sub   │
                             └─────────────────────────┘
```

---

## Design Details

### Service Worker (`src/sw.ts`)

Mode: **InjectManifest** — viết SW custom, vite-plugin-pwa inject danh sách precache lúc build.

```typescript
import { precacheAndRoute, cleanupOutdatedCaches } from 'workbox-precaching'
import { registerRoute }                            from 'workbox-routing'
import { NetworkFirst }                             from 'workbox-strategies'
import { ExpirationPlugin }                         from 'workbox-expiration'

precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

registerRoute(
  ({ url }) => url.pathname.startsWith('/api/'),
  new NetworkFirst({
    cacheName: 'api-cache',
    networkTimeoutSeconds: 8,        // 8s cho 3G yếu
    plugins: [new ExpirationPlugin({ maxAgeSeconds: 86400 })]
  }),
  'GET'
)

self.addEventListener('push', (event) => {
  const data = event.data?.json() ?? {}
  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: '/icons/icon-192.png',
      data: { url: data.url }
    })
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  event.waitUntil(clients.openWindow(event.notification.data.url ?? '/'))
})
```

### Vite Config

`vite-plugin-pwa` là build tool — đặt trong `devDependencies`.

> **Lưu ý:** `vite.config.ts` hiện import từ `vitest/config`. Cần tách thành `vitest.config.ts` riêng và đổi `vite.config.ts` về `import { defineConfig } from 'vite'` để `VitePWA` plugin hoạt động đúng.

```typescript
// vite.config.ts
VitePWA({
  registerType: 'prompt',           // KHÔNG dùng autoUpdate — tránh reload giữa chừng khi user đang nhập form
  strategies: 'injectManifest',
  injectManifest: { swSrc: 'src/sw.ts' },
  manifest: {
    name: 'QL Đơn Hàng',
    short_name: 'QLĐơnHàng',
    display: 'standalone',
    theme_color: '#1e40af',         // cần verify với design system thực tế
    background_color: '#ffffff',
    start_url: '/',
    icons: [
      { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
      { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
    ]
  }
})
```

### Push Notifications — Frontend

#### Hook `usePushNotification`

States cần handle:

| State | Mô tả |
|---|---|
| `'idle'` | Chưa kiểm tra permission |
| `'granted'` | Đã subscribe thành công |
| `'denied'` | User từ chối — ẩn nút, không hỏi lại |
| `'unsupported'` | Browser không hỗ trợ |
| `'loading'` | Đang xử lý subscribe |
| `'error'` | Subscribe thất bại (network, server) |

Flow:
1. Mount: kiểm tra `Notification.permission` — set state từ browser permission hiện tại
2. User click "Bật thông báo" → chỉ khi state là `'idle'`
3. Gọi `Notification.requestPermission()` — nếu denied, set `'denied'` và lưu vào localStorage để không hỏi lại
4. Subscribe `pushManager` với VAPID public key
5. `POST /api/push/subscribe` — nếu lỗi network, hiện error toast, giữ state là `'error'` (cho phép retry)

#### Component `InstallPrompt.tsx`

- Vị trí: banner cố định phía trên cùng, dismissable
- Trigger: listen `beforeinstallprompt` event, chỉ hiện sau khi user đã visit 2+ lần (dùng localStorage counter)
- Sau khi dismiss: ẩn 30 ngày (lưu timestamp vào localStorage)
- Buttons: **[Cài đặt]** (gọi `prompt()`) và **[Không, cảm ơn]** (dismiss)

#### Notification Permission UX Flow

Browser chỉ cho phép request permission một lần. Flow phải dùng **soft prompt trước**:

```
[App banner] "Bật thông báo để nhận cập nhật báo giá?"
    [Bật thông báo]  →  gọi Notification.requestPermission()
    [Để sau]         →  ẩn banner, không gọi requestPermission()
```

Không gọi `requestPermission()` trực tiếp khi load page.

### Push Notifications — Backend

#### VAPID Keys

```
# Dev (user-secrets)
dotnet user-secrets set "Vapid:PublicKey" "..."
dotnet user-secrets set "Vapid:PrivateKey" "..."
dotnet user-secrets set "Vapid:Subject" "mailto:admin@example.com"

# Production (Railway / env vars)
VAPID__PUBLICKEY=...
VAPID__PRIVATEKEY=...
VAPID__SUBJECT=mailto:...
```

`appsettings.json` chỉ chứa placeholder rỗng, KHÔNG commit keys thực.

#### Notification Integration Point

Gửi push notification khi `QuotationStatus` thay đổi sang:

| Status | Title | Body |
|---|---|---|
| `Confirmed` | "Báo giá đã xác nhận" | "Báo giá {code} đã được xác nhận." |
| `AccountingConfirmed` | "Kế toán đã duyệt" | "Báo giá {code} đã được kế toán xác nhận." |
| `Cancelled` | "Báo giá bị hủy" | "Báo giá {code} đã bị hủy." |

**Recipients:** Chủ báo giá (`OwnerUserId`) + tất cả user có role Admin.

Payload: `{ "title": "...", "body": "...", "url": "/quotations/{id}" }`

#### `IPushSender` interface (Application layer)

```csharp
// Application/Notifications/Interfaces/IPushSender.cs
public interface IPushSender
{
    Task SendAsync(Guid userId, string title, string body, string url, CancellationToken ct = default);
}
```

#### Mở rộng `INotificationService`

Thêm method vào interface và implementation:

```csharp
Task SendAsync(Guid userId, string type, string title, string? body, string? link, CancellationToken ct = default);
```

Implementation trong `NotificationService.SendAsync`:
1. Persist `Notification` entity vào DB
2. Gọi `IPushSender.SendAsync(userId, ...)` nếu user có subscription

#### `PushSenderService` (Infrastructure layer)

```csharp
// Infrastructure/Notifications/PushSenderService.cs
// - Inject IOptions<VapidOptions> và IAppDbContext
// - Lookup PushSubscription[] by userId
// - Call WebPush.SendNotificationAsync for each subscription
// - On WebPushException with StatusCode 410 → delete subscription from DB
```

#### `PushSubscription` entity (Domain layer)

```csharp
// Domain/Notifications/PushSubscription.cs
public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = default!;   // unique key per device
    public string P256DH { get; set; } = default!;
    public string Auth { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
```

#### `POST /api/push/subscribe`

Upsert: nếu `Endpoint` đã tồn tại thì update keys, không tạo duplicate.

#### `DELETE /api/push/subscribe`

Nhận body `{ "endpoint": "<url>" }` — xóa đúng subscription của thiết bị cụ thể, không xóa tất cả thiết bị của user.

---

## Files to Create / Modify

### Frontend

| File | Hành động |
|------|-----------|
| `frontend/package.json` | devDep: `vite-plugin-pwa`; dep: `workbox-core`, `workbox-precaching`, `workbox-routing`, `workbox-strategies`, `workbox-expiration` |
| `frontend/vite.config.ts` | Đổi import về `'vite'`, thêm `VitePWA` plugin, tách test config |
| `frontend/vitest.config.ts` | **Tạo mới** — tách vitest config ra khỏi vite.config.ts |
| `frontend/src/sw.ts` | **Tạo mới** — Service Worker |
| `frontend/public/icons/icon-192.png` | **Tạo mới** — app icon 192×192 |
| `frontend/public/icons/icon-512.png` | **Tạo mới** — app icon 512×512 |
| `frontend/src/hooks/usePushNotification.ts` | **Tạo mới** — với đầy đủ state machine |
| `frontend/src/components/InstallPrompt.tsx` | **Tạo mới** — banner với localStorage counter |

### Backend

| File | Hành động |
|------|-----------|
| `OrderMgmt.WebApi.csproj` | Thêm NuGet package `WebPush` (by Peter Beverloo) |
| `Domain/Notifications/PushSubscription.cs` | **Tạo mới** — entity |
| `Application/Notifications/Interfaces/IPushSender.cs` | **Tạo mới** — interface |
| `Application/Notifications/Interfaces/INotificationService.cs` | Thêm `SendAsync` method |
| `Application/Notifications/Services/NotificationService.cs` | Implement `SendAsync` (persist + dispatch push) |
| `Application/Common/Interfaces/IAppDbContext.cs` | Thêm `DbSet<PushSubscription>` |
| `Infrastructure/Notifications/PushSenderService.cs` | **Tạo mới** — WebPush impl + 410 cleanup |
| `Infrastructure/Notifications/VapidOptions.cs` | **Tạo mới** — options class |
| `Infrastructure/Persistence/Configurations/PushSubscriptionConfiguration.cs` | **Tạo mới** — EF config |
| `Infrastructure/Persistence/AppDbContext.cs` | Thêm `DbSet<PushSubscription>` |
| `Infrastructure/DependencyInjection.cs` | Register `IPushSender`, `VapidOptions` |
| `Application/DependencyInjection.cs` | Không cần thay đổi (IPushSender resolved qua DI) |
| `WebApi/Controllers/PushSubscriptionController.cs` | **Tạo mới** — POST + DELETE endpoints |
| `WebApi/appsettings.json` | Thêm `Vapid` section với placeholder rỗng |
| EF Migration `AddPushSubscriptions` | **Tạo mới** — `dotnet ef migrations add AddPushSubscriptions` |

---

## Implementation Phases

1. **Phase 1** — Installable + static precache  
   Frontend only: `vite-plugin-pwa`, `vitest.config.ts` split, `sw.ts` precache, icons, `InstallPrompt.tsx`. Không có backend changes.

2. **Phase 2** — Runtime caching cho GET `/api/**`  
   Thêm `registerRoute` NetworkFirst vào `sw.ts`. Không có backend changes.

3. **Phase 3** — Push notifications  
   Backend: `PushSubscription` entity + migration, `IPushSender` + `PushSenderService`, mở rộng `INotificationService.SendAsync`, `PushSubscriptionController`, VAPID config, tích hợp vào `QuotationService` cho 3 status transitions.  
   Frontend: `usePushNotification` hook, soft-prompt UX, wire vào Settings hoặc notification bell area.

---

## Open Questions (Resolved)

| Câu hỏi | Quyết định |
|---|---|
| Icons: cần logo nguồn | Cần file PNG nguồn để generate; placeholder dùng tạm trong Phase 1 |
| Theme color | `#1e40af` là estimate — cần confirm với CSS variable thực tế trước khi ship Phase 1 |
| Integration point | Gửi khi status chuyển sang `Confirmed`, `AccountingConfirmed`, `Cancelled` |
| Ai nhận notification? | Chủ báo giá (`OwnerUserId`) + tất cả Admin users |

---

## Next Steps

Invoke `write-plan` skill để tạo implementation plan chi tiết theo 3 phases.
