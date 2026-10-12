# PWA mobile — học từ CRMProjects

**Date:** 2026-10-07 23:04:36
**Status:** Design approved, pending implementation plan
**Related:** [260523-1500-pwa-progressive-web-app](../260523-1500-pwa-progressive-web-app/SUMMARY.md) (PWA gốc)

---

## Problem Framing

So sánh với `D:\Projects\CRM\CRMProjects` (Blazor Server + MudBlazor, PWA riêng tại `/m/` cho chụp ảnh công trình) cho thấy QLBanHang đã có hạ tầng PWA tốt hơn (installable, update prompt, Web Push VAPID, refresh token HttpOnly) nhưng còn 4 lỗ hổng mà CRM đã xử lý:

1. **Push không hiển thị** — backend gửi `{title, body, url}` qua `PushSenderService`, nhưng `frontend/src/sw.ts` chỉ có listener `message`; thiếu `push` và `notificationclick` dù brainstorm gốc đã thiết kế.
2. **Danh sách khó dùng trên điện thoại** — mọi list page là bảng TanStack cuộn ngang. CRM dùng `MudTable Breakpoint="Breakpoint.Sm"` để tự chuyển thành thẻ.
3. **iOS mỏng** — `index.html` chỉ có viewport; không `apple-touch-icon`, `theme-color`, `apple-mobile-web-app-*`, safe-area. Trên Safari chưa cài, `'Notification' in window` là false → hook trả `unsupported` → banner bật thông báo bị ẩn, người dùng không biết phải cài app.
4. **Thiếu tài liệu giới hạn nền tảng** — CRM có `docs/architecture/pwa-anh-cong-trinh.md` ghi rõ giới hạn iOS/Safari.

---

## Goals & Non-Goals

**Goals**
- Push hiển thị đúng title/body; bấm thông báo → focus tab đang mở và điều hướng SPA (không reload), hoặc mở cửa sổ mới tới `url`.
- 4 trang nghiệp vụ chính hiển thị dạng thẻ dưới `md` (768px): **Báo giá, Khách hàng, Hàng hóa, Phiếu nhập/xuất**.
- iOS: icon branding đúng trên màn hình chính, standalone không bị tai thỏ/home indicator che, **push khi đã cài (iOS ≥ 16.4)**, banner hướng dẫn cài khi chưa cài.
- Tài liệu `docs/architecture/pwa-mobile.md` với ma trận hỗ trợ theo nền tảng.

**Non-Goals**
- Offline mutations / Background Sync (giữ nguyên non-goal của PWA gốc).
- Mở rộng allow-list API cache (`lib/sw-routes.ts`).
- Bottom navigation.
- Card view cho trang admin/danh mục (users, nhóm hàng, kho, lý do nhập/xuất, PTTT, chi nhánh, NCC).
- App native.

---

## Constraints & Assumptions

- Frontend: React 18 + Vite + Tailwind 3 + shadcn/ui; responsive hiện **chỉ bằng class CSS**, không có hook `useMediaQuery` — giữ nguyên quy ước này.
- SW: `vite-plugin-pwa` `injectManifest`, `registerType: 'prompt'` → SW mới tới người dùng qua banner "có bản mới" sẵn có ở `App.tsx`.
- Backend push pipeline giữ nguyên: `QuotationService` → `NotificationService.SendAsync` → `IPushSender` (payload `{title, body, url}`, tự xóa subscription khi 410).
- Endpoint `GET api/settings/branding/icon/{size}` chỉ nhận 192/512; `IconRenderer` nhận mọi kích thước → thêm 180 là thay đổi nhỏ.
- Production: frontend (Caddy) và API khác origin trên Railway; manifest icon dùng `apiBase` tuyệt đối.
- Giả định: VAPID key đã/ sẽ được cấu hình trên production (nếu rỗng, `PushSenderService` không gửi gì).

---

## Approaches Considered (card view)

| Approach | Tóm tắt | Ưu | Nhược | Độ phức tạp |
| --- | --- | --- | --- | --- |
| **A — `ResponsiveTable` + `renderCard` (chọn)** | Component chung nhận instance TanStack; `md+` hiện `<Table>` cũ, `<md` map `getRowModel().rows` qua `renderCard` của từng trang; ẩn/hiện bằng `hidden md:block` / `md:hidden` | Giữ nguyên sort/filter/pagination/footer; thẻ được thiết kế riêng cho từng trang; đúng quy ước CSS-only | Render cả 2 khối DOM (không đáng kể với vài chục dòng/trang) | Thấp–TB |
| B — Thẻ tự sinh từ `column.meta.mobile` | Giống MudTable `Breakpoint.Sm` của CRM | Ít code mỗi trang | Thẻ chung chung; cell thiết kế cho bảng dễ vỡ; khó tùy biến theo trạng thái | TB |
| C — CSS biến `<tr>` thành block | `display:block` + `data-label` | Không thêm JS | Xung đột `TableColGroup`/sticky; kém a11y; khó với Tailwind | TB, không khuyến nghị |
| D — `useMediaQuery` render 1 trong 2 | Hook JS theo breakpoint | DOM gọn | Trái quy ước CSS-only; flicker khi xoay; khó test | Thấp |

Push handler, iOS meta và tài liệu không có lựa chọn thay thế đáng kể — xem section files.

---

## Recommended Approach

**Approach A** cho card view, kèm 3 hạng mục còn lại. Chỉ 4 trang nên `renderCard` viết tay rẻ mà UX tốt hơn hẳn B; nếu sau này nhân rộng, có thể thêm chế độ tự sinh từ `column.meta` vào cùng component.

Chi tiết:
- [section-01-push-handler.md](section-01-push-handler.md) — `push` / `notificationclick`, `lib/sw-push.ts`, điều hướng SPA.
- [section-02-mobile-card-view.md](section-02-mobile-card-view.md) — `ResponsiveTable`, nội dung thẻ cho 4 trang.
- [section-03-ios-and-platform-docs.md](section-03-ios-and-platform-docs.md) — meta iOS, apple-touch-icon 180, safe-area, banner cài app, push iOS, `pwa-mobile.md`.

### Rollout (4 phase độc lập, rủi ro tăng dần)

1. Push handler (chỉ `sw.ts` + `lib/sw-push.ts` + listener trong `App.tsx`).
2. iOS meta + safe-area + banner `ios-needs-install` + icon 180.
3. `ResponsiveTable` + Báo giá làm mẫu → Khách hàng, Hàng hóa, Phiếu nhập/xuất.
4. `docs/architecture/pwa-mobile.md` + cập nhật `docs/SUMMARY.md`; thêm `Cache-Control: no-cache` cho `/sw.js` và `/manifest.webmanifest` trong `frontend/Caddyfile`.

Không cần migration DB, không đổi cấu hình VAPID.

### Verification

- **Vitest:** `lib/sw-push.test.ts` (parse payload, resolve URL an toàn), `responsive-table.test.tsx` (số thẻ, empty/loading, class ẩn/hiện), `usePushNotification` với UA iOS ± standalone.
- **xUnit:** endpoint icon nhận 180, vẫn 400 với kích thước khác.
- **Thủ công:** Chrome Android (cài app, nhận push, click khi app mở/đóng); iPhone iOS ≥ 16.4 (banner hướng dẫn, icon không nền đen, safe-area, push sau khi cài); 4 list page ở 375px và 768px; SW mới kích hoạt qua banner cập nhật.

---

## Open Questions

1. `IconRenderer` có xuất PNG nền trong suốt không? iOS tô đen vùng trong suốt của `apple-touch-icon` → có thể cần nền đặc (trắng hoặc `theme_color`) riêng cho size 180.
2. Favicon trong `index.html` dùng đường dẫn tương đối `/api/settings/branding/icon/192`, nhưng Caddy production không proxy `/api` → có thể đang hỏng trên prod; nên xử lý cùng lúc với `apple-touch-icon` (chèn qua `transformIndexHtml` dùng `apiBase`).
3. Nội dung thẻ cụ thể cho Hàng hóa và Phiếu nhập/xuất (trường nào là tiêu đề/phụ) — chốt khi làm plan, dựa trên cột đang hiển thị.
4. VAPID key đã cấu hình trên Railway production chưa?

---

## Next Steps

- Chạy skill `write-plan` dựa trên brainstorm này → `docs/plans/<timestamp>-pwa-mobile-learn-from-crm/`.
- Thêm dòng brainstorm vào `docs/SUMMARY.md` (bảng **Other**).
