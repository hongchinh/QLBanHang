# Nền tảng PWA — thông báo đẩy hiển thị, iOS, tài liệu (đợt 1 của khu quản trị mobile)

> Nguồn: [brainstorm 261014-0618-mobile-manager-hub](../../brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md), đợt 1 ([section-01 § Đợt 1](../../brainstorms/261014-0618-mobile-manager-hub/section-01-mobile-shell-and-screens.md#đợt-1--nền-tảng-pwa)). Thiết kế chi tiết lấy từ brainstorm 261007: [§1 Push handler](../../brainstorms/261007-2304-pwa-mobile-learn-from-crm/section-01-push-handler.md) và [§3 iOS và tài liệu](../../brainstorms/261007-2304-pwa-mobile-learn-from-crm/section-03-ios-and-platform-docs.md).
>
> Lập: 2026-10-14 22:27:06. Các đợt 2–6 có plan riêng.

## Goal

Thông báo đẩy mà backend đã gửi hiện ra trên Android và iPhone (iOS 16.4 trở lên, app đã cài), và bấm vào thì mở đúng trang trong app đang chạy, không tải lại. iPhone có icon màn hình chính, không bị che bởi tai thỏ hay thanh home, và người dùng Safari chưa cài được hướng dẫn cài để nhận thông báo. Caddy không cache `sw.js` và manifest. Giới hạn của từng nền tảng được ghi trong `docs/architecture/pwa-mobile.md`.

## Scope

**In scope**
- `frontend/src/lib/sw-push.ts` (thuần, có test): `parsePushPayload` (kèm trường tùy chọn `tag`), `notificationTag`, `resolveTargetUrl`, `focusOrOpenWindow`, hằng `NAVIGATE_MESSAGE`.
- Listener `push` và `notificationclick` trong `frontend/src/sw.ts`.
- Component `frontend/src/components/sw-navigate-listener.tsx`, gắn trong `BrowserRouter` của `App.tsx`.
- Backend: `GET /api/settings/branding/icon/180`.
- `frontend/src/lib/pwa-icons.ts`: URL icon branding và thẻ `<link>` favicon + `apple-touch-icon`, chèn qua `transformIndexHtml` trong `vite.config.ts`.
- Meta iOS trong `frontend/index.html`.
- Tiện ích `pt-safe`, `pb-safe`, `pl-safe`, `pr-safe` trong `frontend/src/index.css`; áp cho `AppShell` và drawer mobile. Sheet tìm kiếm mobile (đã có đệm 12px) dùng `max(0.75rem, env(safe-area-inset-*))`.
- `frontend/src/lib/ios-pwa.ts` và trạng thái `'ios-needs-install'` của `usePushNotification`; `PushPermissionPrompt` hiện hướng dẫn cài.
- Popup "Có phiên bản mới" và vùng toast trên điện thoại tránh thanh home của iPhone.
- `frontend/Caddyfile`: `Cache-Control: no-cache` cho `/sw.js` và `/manifest.webmanifest`.
- `frontend/Dockerfile`: truyền `VITE_VAPID_PUBLIC_KEY` vào bước build (hiện thiếu, nên push không bật được trên production).
- Tài liệu: `docs/architecture/pwa-mobile.md` (mới), `docs/SUMMARY.md`, `docs/code-standard/conventions.md`, `docs/architecture/system-architecture.md` (trỏ tới tài liệu mới).

**Out of scope**
- Thanh tab, trang Tổng quan, mục lục Báo cáo, `ResponsiveTable` (đợt 2).
- Thay đổi payload hoặc nơi gửi push ở backend; backend chưa gửi `tag` (đợt 3 mới gửi).
- Tùy chọn ẩn số tiền trên màn hình khóa (câu hỏi mở 5 của brainstorm).
- Offline mutations, Background Sync, mở rộng allow-list cache API.
- `InstallPrompt` (chỉ có trên trình duyệt hỗ trợ `beforeinstallprompt`, không có trên iOS) giữ nguyên.
- Migration, quyền mới.

## Assumptions

- **Chỉ lập đợt 1.** Brainstorm ghi "mỗi đợt một plan riêng" và bước kế tiếp là plan đợt 1.
- **Icon iOS không bị nền đen.** [SkiaSharpPwaIconRenderer.cs](../../../backend/src/OrderMgmt.Infrastructure/Branding/SkiaSharpPwaIconRenderer.cs) tô nền trắng (`ResizeWithPad`) hoặc nền xanh đặc (placeholder), nên PNG không có vùng trong suốt. Câu hỏi mở 3 (phần `IconRenderer`) đã có lời đáp; không đổi renderer.
- **Favicon hiện hỏng trên production.** `index.html` dùng đường dẫn tương đối `/api/settings/branding/icon/192`, mà Caddy không chuyển `/api` sang backend (không có `reverse_proxy` trong `Caddyfile`). Plan chuyển favicon sang cùng cơ chế `iconSrc(apiBase)` như manifest, như brainstorm 261007 đề nghị.
- **Thanh trạng thái iOS dùng `default`**, không `black-translucent`, nên nội dung không nằm dưới thanh trạng thái và đệm trên bằng 0 ở chế độ dọc. Safe-area cần cho trái/phải (xoay ngang) và đáy (thanh home).
- **Đặt đệm safe-area ở `AppShell` thay vì từng thanh dính.** Ngoài drawer và sheet tìm kiếm, popup "Có phiên bản mới" và vùng toast cũng `fixed` ở đáy nên có đệm riêng (phase 02, Task 11). Danh sách áp dụng của 261007 có thanh hành động của `quotation-form-page.tsx` và `stock-voucher-form-page.tsx`, nhưng hai thanh này là `sticky top-0` bên trong vùng cuộn của `AppShell`; đệm ở gốc `AppShell` đã phủ chúng, cùng với header và `PushPermissionPrompt`. Chỉ phần tử `fixed` (drawer, sheet tìm kiếm) cần đệm riêng. `InstallPrompt` không hiện trên iOS nên không đổi.
- **VAPID key đã cấu hình cho backend trên production** (giả định của brainstorm). Frontend thì chưa nhận được khóa: `frontend/Dockerfile` chỉ khai báo `ARG VITE_API_BASE_URL`. Phase 03 thêm `ARG VITE_VAPID_PUBLIC_KEY`; người dùng đặt biến này trên Railway sau merge.
- **Thông báo luôn hiện**, kể cả khi app đang mở (bắt buộc với `userVisibleOnly` của Chrome và iOS). Trùng với toast SignalR là chấp nhận được.
- Bấm thông báo khi có nhiều cửa sổ: focus cửa sổ đầu tiên `clients.matchAll` trả về.
- Code thực thi trên nhánh `feat/pwa-push-ios` tách từ `main` (hiện `8135477`), PR về remote `qlbanhang`.

## Risks

- **`sw.ts` không chạy được trong Vitest** (cần `ServiceWorkerGlobalScope`). Giảm rủi ro: toàn bộ logic nằm trong `lib/sw-push.ts` có test; `sw.ts` chỉ nối dây. Kiểm bằng `npm run build` và thử tay.
- **`vite.config.ts` import `src/lib/pwa-icons.ts`.** File này được kiểm kiểu theo `tsconfig.node.json` (lib ES2023, không DOM) nên phải thuần, không dùng alias `@/`, không dùng API trình duyệt.
- **iOS chỉ thử được trên máy thật** (iOS 16.4+). Không có máy thì các mục iOS trong danh sách thử tay để lại, ghi trong báo cáo thực thi.
- **Xung đột với plan [261014-2150-app-icon-registry](../261014-2150-app-icon-registry/SUMMARY.md)** (đã duyệt, chưa thực thi): plan đó sửa `App.tsx` và các file import `lucide-react`. Plan này chỉ thêm một dòng vào `App.tsx` và không thêm icon lucide, nên xung đột nhỏ; plan nào merge sau thì rebase.
- **Đệm đáy ở `AppShell`** làm vùng nội dung thấp hơn trên iPhone có thanh home (khoảng 34px). Đó là ý muốn; trên desktop và Android `env()` bằng 0 nên không đổi.

## Phases

- [x] Phase 01 — Push hiển thị và bấm mở đúng trang (M) — [phase-01-push-display-and-click.md](phase-01-push-display-and-click.md)
- [x] Phase 02 — Hỗ trợ iOS: icon, meta, safe-area, hướng dẫn cài (M) — [phase-02-ios-support.md](phase-02-ios-support.md)
- [x] Phase 03 — Cache header của Caddy và tài liệu nền tảng (S) — [phase-03-hosting-and-docs.md](phase-03-hosting-and-docs.md)

Phase 01 và 02 gần như độc lập về file và có thể làm song song trong hai worktree rồi gộp; cả hai cùng sửa `App.tsx` và `App.shell.test.tsx` ở những chỗ khác nhau (phase 01 Task 6, phase 02 Task 11), nên khi gộp có thể phải giải xung đột nhỏ trong file test. Phase 03 làm sau cùng vì tài liệu mô tả kết quả của hai phase đầu.

## Final Verification

Từ `frontend`:

```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

Từ `backend` (PostgreSQL cục bộ, không dùng `qldonhang_test`):

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
dotnet build
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BrandingIconTests"
```

Kiểm bản build:

```bash
grep -c 'rel="apple-touch-icon"' frontend/dist/index.html   # 1
grep -c 'viewport-fit=cover' frontend/dist/index.html          # 1
grep -c "addEventListener(\"push\"\|addEventListener('push'" frontend/dist/sw.js  # >= 1
```

Thử tay (ghi kết quả vào `EXECUTION-REPORT.md`; mục nào không có thiết bị thì ghi "chưa thử" và lý do):

1. Chrome desktop, `npm run dev` + backend: bật thông báo; đổi trạng thái một báo giá bằng tài khoản khác → thông báo hiện khi app đang mở và khi tab đã đóng; bấm vào → mở đúng trang báo giá, khi app đang mở thì không tải lại trang (form đang nhập không mất).
2. Chrome Android (bản deploy hoặc tunnel HTTPS): như mục 1, với app đã cài.
3. iPhone iOS 16.4+, Safari chưa cài: banner hướng dẫn "Chia sẻ → Thêm vào MH chính" hiện; "Để sau" ẩn 30 ngày.
4. iPhone đã cài: icon màn hình chính là logo branding, không nền đen; header, drawer, sheet tìm kiếm không bị tai thỏ hay thanh home che (cả khi xoay ngang); bật thông báo; nhận push; bấm mở đúng trang.
5. Production (sau khi đặt `VITE_VAPID_PUBLIC_KEY` trên Railway): DevTools → Network cho thấy `/sw.js` và `/manifest.webmanifest` có `Cache-Control: no-cache`; favicon hiện trên tab trình duyệt; nút "Bật thông báo" hiện (không phải `unsupported`).

## Rollback / Recovery

- Không có migration và không đổi dữ liệu. Hoàn tác bằng `git revert` commit merge của PR.
- Nếu chỉ phần push gây lỗi trên một nền tảng: revert riêng commit nối listener trong `sw.ts`; thông báo lại không hiện như trước, phần còn lại giữ nguyên.
- Service worker dùng `registerType: 'prompt'`: bản SW mới (kể cả bản revert) tới người dùng khi họ bấm cập nhật ở banner; nhờ `no-cache` cho `/sw.js` của phase 03, trình duyệt luôn thấy bản mới.
