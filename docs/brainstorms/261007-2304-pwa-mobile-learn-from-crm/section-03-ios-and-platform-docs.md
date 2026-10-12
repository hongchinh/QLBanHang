# Section 03 — Hỗ trợ iOS PWA và tài liệu giới hạn nền tảng

## Hiện trạng

- `frontend/index.html`: chỉ `<meta name="viewport" content="width=device-width, initial-scale=1.0">` và favicon `/api/settings/branding/icon/192` (đường dẫn tương đối).
- Không có safe-area handling (`env(safe-area-inset-*)`).
- `usePushNotification`: `!('Notification' in window)` → `'unsupported'` → `PushPermissionPrompt` trả `null`. Trên iOS Safari chưa cài, người dùng không thấy gợi ý nào.
- `SettingsController.GetPwaIcon` chỉ nhận size 192/512; `BrandingService.GetPwaIconAsync` → `IconRenderer.RenderAsync(size)` nhận mọi kích thước.

Tham chiếu CRM: `CRMApp/wwwroot/m/index.html` có `viewport-fit=cover`, `apple-mobile-web-app-*`, `apple-touch-icon`; `docs/architecture/pwa-anh-cong-trinh.md` ghi giới hạn iOS.

## Thiết kế

### Meta trong `index.html`

```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
<meta name="theme-color" content="#1e40af" />
<meta name="mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-title" content="QLĐơnHàng" />
<meta name="apple-mobile-web-app-status-bar-style" content="default" />
```

`status-bar-style=default` (không `black-translucent`) để nội dung không nằm dưới thanh trạng thái; safe-area vẫn cần cho trái/phải/dưới (landscape, home indicator).

### `apple-touch-icon` 180px

- Backend: `GetPwaIcon` cho phép thêm `180` (`size is 180 or 192 or 512`).
- Frontend: chèn `<link rel="apple-touch-icon" sizes="180x180" href="…/settings/branding/icon/180">` qua plugin `transformIndexHtml` trong `vite.config.ts`, dùng cùng hàm `iconSrc(apiBase)` như manifest. Cân nhắc chuyển favicon sang cùng cơ chế (xem Open Question 2 trong SUMMARY).
- Open question: iOS tô đen vùng trong suốt → kiểm tra `IconRenderer`; nếu cần, size 180 render nền đặc.

### Safe-area

- Thêm utilities Tailwind (plugin nhỏ hoặc class tùy biến): `pt-safe`, `pb-safe`, `pl-safe`, `pr-safe` = `env(safe-area-inset-*)`.
- Áp cho: `app-header.tsx`, drawer mobile trong `app-layout.tsx`, `header-search-mobile-sheet.tsx`, thanh hành động dính (`quotation-form-page.tsx`, `stock-voucher-form-page.tsx`), `InstallPrompt`, `PushPermissionPrompt`.
- Không ảnh hưởng desktop/Android (giá trị `env()` = 0).

### Push trên iOS (≥ 16.4, chỉ khi đã cài)

- `usePushNotification`: thêm trạng thái `'ios-needs-install'`:
  - iOS (UA iPhone/iPad, hoặc iPadOS desktop UA + `maxTouchPoints > 1`) **và** không standalone (`navigator.standalone !== true` và `!matchMedia('(display-mode: standalone)').matches`) → `'ios-needs-install'`.
  - iOS standalone → luồng hiện tại (`Notification.requestPermission()` từ nút bấm — đúng yêu cầu user gesture của iOS).
- `PushPermissionPrompt` (hoặc banner riêng `IosInstallHint`): với `'ios-needs-install'` hiện hướng dẫn "Bấm **Chia sẻ** → **Thêm vào MH chính** để nhận thông báo", nút "Để sau" ẩn 30 ngày (cùng cơ chế dismiss hiện tại).
- `InstallPrompt` hiện dựa vào `beforeinstallprompt` (iOS không có) → không đổi; hướng dẫn iOS nằm ở banner trên.

### Tài liệu `docs/architecture/pwa-mobile.md`

Nội dung:
- Ma trận: Chrome Android / Chrome-Edge desktop / iOS Safari chưa cài / iOS đã cài × (cài đặt, push, banner cập nhật, cache shell, cache API allow-list).
- Push iOS: chỉ iOS ≥ 16.4 và chỉ khi chạy standalone; quyền phải xin từ thao tác người dùng.
- iOS có thể xóa storage/cache sau ~7 ngày không mở app; không có Background Sync (đã là non-goal).
- Lý do allow-list API cache (`lib/sw-routes.ts`): cache key không chứa `X-Branch-Id`/`Authorization`.
- Luồng push: `QuotationService` → `NotificationService` → `PushSenderService` → SW `push` → `notificationclick` → `NAVIGATE`.
- Ghi chú hosting: Caddy `Cache-Control: no-cache` cho `/sw.js`, `/manifest.webmanifest`; VAPID key qua env.
- Cập nhật `docs/SUMMARY.md` (bảng Architecture).

## Kiểm thử

- Vitest: `usePushNotification` với UA iOS ± standalone → `'ios-needs-install'` / `'idle'`.
- xUnit: `GetPwaIcon(180)` → 200; size khác (vd. 100) → 400.
- Thủ công iPhone iOS ≥ 16.4: banner hướng dẫn trên Safari; sau khi cài — icon branding không nền đen, header/thanh dính không bị che, bật push và nhận thông báo, click mở đúng trang.
