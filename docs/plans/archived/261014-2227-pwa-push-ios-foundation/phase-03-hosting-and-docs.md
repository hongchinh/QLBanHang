# Phase 03 — Cache header của Caddy và tài liệu nền tảng

**Status:** [x] complete
**Complexity:** S

## Objective

Caddy trả `Cache-Control: no-cache` cho `/sw.js` và `/manifest.webmanifest`, để trình duyệt luôn hỏi lại bản mới của service worker và manifest. Bản build production nhận `VITE_VAPID_PUBLIC_KEY` (hiện `frontend/Dockerfile` chỉ khai báo `ARG VITE_API_BASE_URL`, nên khóa VAPID rỗng trong bundle và nút "Bật thông báo" rơi về `unsupported`). Giới hạn PWA theo nền tảng, luồng push và các quy ước mới được ghi vào tài liệu, để các đợt 2–6 dựa vào.

Phase này không có code ứng dụng; không có test runner cho `Caddyfile` và `Dockerfile`, nên kiểm bằng `grep`, `caddy validate` (Docker) và kiểm tra trên bản chạy thật. Tài liệu kiểm bằng `grep`.

Làm sau phase 01 và 02: tài liệu mô tả kết quả của chúng.

## Files

- `frontend/Caddyfile`
- `frontend/Dockerfile`
- `docs/brainstorms/261014-0618-mobile-manager-hub/` (đưa lên nhánh, Task 0)
- `docs/plans/261014-2227-pwa-push-ios-foundation/` (đưa lên nhánh, Task 0)
- `docs/architecture/pwa-mobile.md` (mới)
- `docs/architecture/system-architecture.md`
- `docs/code-standard/conventions.md`
- `docs/SUMMARY.md`

## Tasks

### Task 0 — Đưa brainstorm và plan lên nhánh

Nhánh tách từ `8135477`, nơi brainstorm 261014-0618 và thư mục plan này chưa được commit (chúng chỉ nằm trong working tree của `main`). Tài liệu ở Task 2–3 liên kết tới chúng.

1. Từ worktree của nhánh, chép hai thư mục từ checkout chính (`D:\Projects\QLBanHang`): `docs/brainstorms/261014-0618-mobile-manager-hub/` và `docs/plans/261014-2227-pwa-push-ios-foundation/` (bản mới nhất, kể cả dấu trạng thái phase).
2. **Kiểm** — `git status --short docs` chỉ liệt kê hai thư mục đó.
3. **Commit** — `git add docs/brainstorms/261014-0618-mobile-manager-hub docs/plans/261014-2227-pwa-push-ios-foundation && git commit -m "docs: manager mobile hub brainstorm and the round 1 plan"`

### Task 1 — `no-cache` cho service worker và manifest

1. **Kiểm trạng thái trước** — `grep -c "sw.js" frontend/Caddyfile` / Expected: `0`.

2. **Sửa** — trong `frontend/Caddyfile`, ngay sau khối `@html … header @html …`:

   ```caddyfile
   # The service worker and the manifest must be revalidated on every load, or an
   # installed app keeps an old worker (and its push handler) until the cache expires.
   @pwa path /sw.js /manifest.webmanifest
   header @pwa Cache-Control "no-cache"
   ```

3. **Kiểm cú pháp** (khi Docker Desktop đang chạy; xem memory về đường dẫn Docker) — từ thư mục gốc repo:

   ```bash
   docker run --rm -e PORT=8080 -v "$(pwd)/frontend/Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2 caddy validate --config /etc/caddy/Caddyfile
   ```

   Expected: `Valid configuration`. Không có Docker thì ghi "chưa validate" trong báo cáo thực thi và kiểm bằng `curl -I` sau deploy (mục 5 của Final Verification).

4. **Kiểm** — `grep -n "@pwa" frontend/Caddyfile` / Expected: 2 dòng.

5. **Commit** — `git commit -am "chore(hosting): never cache the service worker or the manifest"`

### Task 1b — Khóa VAPID công khai trong bản build production

1. **Kiểm trạng thái trước** — `grep -c "VITE_VAPID_PUBLIC_KEY" frontend/Dockerfile` / Expected: `0`.
2. **Sửa** — trong `frontend/Dockerfile`, ngay sau dòng `ENV VITE_API_BASE_URL=$VITE_API_BASE_URL`:

   ```dockerfile
   # Public VAPID key for web push; without it the push prompt reports "unsupported".
   ARG VITE_VAPID_PUBLIC_KEY
   ENV VITE_VAPID_PUBLIC_KEY=$VITE_VAPID_PUBLIC_KEY
   ```

3. **Kiểm** — `grep -c "VITE_VAPID_PUBLIC_KEY" frontend/Dockerfile` / Expected: `3`. Khi có Docker: `docker build --build-arg VITE_VAPID_PUBLIC_KEY=BTestKey -t qldh-fe-test frontend` chạy xong và `docker run --rm qldh-fe-test sh -c "grep -l BTestKey /srv/assets/*.js"` in ra ít nhất một file. Không có Docker thì ghi "chưa build image" trong báo cáo.
4. **Commit** — `git commit -am "chore(hosting): pass the VAPID public key to the frontend build"`
5. **Việc của người dùng sau merge** (ghi vào báo cáo thực thi, không làm được từ đây): đặt biến `VITE_VAPID_PUBLIC_KEY` cho service frontend trên Railway, bằng khóa công khai cùng cặp với khóa backend đang dùng.

### Task 2 — `docs/architecture/pwa-mobile.md`

Viết bằng tiếng Anh như các file trong `docs/architecture`. Nội dung bắt buộc, theo thứ tự:

1. **Platform matrix** — bảng 4 cột nền tảng (Chrome Android, Chrome/Edge desktop, iOS Safari chưa cài, iOS đã cài ≥ 16.4) × 5 dòng: cài đặt (`beforeinstallprompt` + `InstallPrompt` / thủ công qua Chia sẻ → Thêm vào MH chính), push (có / không / có), banner cập nhật SW (`registerType: 'prompt'`), cache app shell (precache), cache API allow-list.
2. **iOS limits** — push chỉ từ iOS 16.4 và chỉ khi chạy standalone; quyền phải xin từ thao tác người dùng (nút "Bật thông báo"); iOS có thể xóa storage và cache sau khoảng 7 ngày không mở app; không có Background Sync (non-goal); một push không hiện thông báo có thể làm iOS thu hồi subscription, nên SW luôn hiện thông báo; thanh trạng thái `default`, `viewport-fit=cover`, tiện ích `pt-safe`/`pb-safe`/`pl-safe`/`pr-safe` và nơi chúng được áp (`AppShell`, drawer mobile; sheet tìm kiếm, popup "Có phiên bản mới" và vùng toast dùng `max(<khoảng cách cũ>, env(...))`); `apple-touch-icon` 180px từ `GET /api/settings/branding/icon/180`, renderer tô nền đặc nên iOS không tô đen.
3. **Push flow** — `QuotationService` → `NotificationService.SendAsync` (lưu, SignalR) → `PushSenderService` (VAPID, payload `{ title, body, url }`, 410 xóa subscription) → SW `push` (`parsePushPayload`, `showNotification` với `tag` = `payload.tag ?? payload.url`, icon tĩnh `/icons/icon-192.png` cùng origin) → `notificationclick` (`resolveTargetUrl` chỉ giữ đường dẫn cùng origin, `focusOrOpenWindow`) → thông điệp `NAVIGATE` → `SwNavigateListener` gọi `navigate`. Ghi rõ: trường `tag` là tùy chọn và để dành cho đợt 3; thông báo cùng `tag` thay thế nhau và vẫn rung/kêu nhờ `renotify: true` (Safari bỏ qua cờ này); thông báo hiện cả khi app đang mở, trùng toast SignalR là chấp nhận được.
4. **Prompts** — trạng thái của `usePushNotification` (`idle`, `loading`, `granted`, `denied`, `unsupported`, `error`, `ios-needs-install`), banner nào hiện ở trạng thái nào (`isPushPromptVisible`), "Để sau" ẩn 30 ngày (`push_prompt_dismissed_until`).
5. **API cache allow-list** — lý do (`lib/sw-routes.ts`): cache key không chứa `X-Branch-Id`/`Authorization`; không bao giờ thêm đường dẫn theo chi nhánh hoặc người dùng.
6. **Hosting** — Caddy: `no-cache` cho `/sw.js`, `/manifest.webmanifest`, `/index.html`; asset có hash cache 1 năm. Frontend và API khác origin trên production: icon manifest, favicon và `apple-touch-icon` dùng `VITE_API_BASE_URL` (`src/lib/pwa-icons.ts`). VAPID: backend đọc cặp khóa từ cấu hình, frontend đọc `VITE_VAPID_PUBLIC_KEY` lúc build (`ARG` trong `frontend/Dockerfile`, biến trên Railway); thiếu khóa thì hook trả `unsupported`.
7. **Manual test checklist** — sao chép 5 mục thử tay của [SUMMARY.md](SUMMARY.md) (Final Verification).

Bước làm:

1. **Kiểm trạng thái trước** — `test -f docs/architecture/pwa-mobile.md; echo $?` / Expected: `1`.
2. **Viết file** theo 7 mục trên. Trước khi viết mục 3 và 6, đọc lại `backend/src/OrderMgmt.Infrastructure/Notifications/PushSenderService.cs` và phần cấu hình VAPID của nó để ghi đúng tên khóa cấu hình.
3. **Kiểm** — `grep -c "^## " docs/architecture/pwa-mobile.md` / Expected: `7`; `grep -c "ios-needs-install\|NAVIGATE\|pb-safe\|no-cache" docs/architecture/pwa-mobile.md` / Expected: ≥ 4.
4. **Commit** — `git add docs/architecture/pwa-mobile.md && git commit -m "docs(pwa): platform limits, push flow and hosting notes"`

### Task 3 — Cập nhật tài liệu chung

1. **`docs/architecture/system-architecture.md`** — trong mục "Dashboard, Reports, Search, Branding And Notifications", thêm một gạch đầu dòng: "Web push: `NotificationService` → `PushSenderService` (VAPID) → service worker `push` / `notificationclick` → `NAVIGATE` message handled by `SwNavigateListener`. `GET /api/settings/branding/icon/{180|192|512}` serves the PWA and iOS icons. Platform limits and the push flow: [pwa-mobile.md](pwa-mobile.md)."
2. **`docs/code-standard/conventions.md`** — trong khối "Frontend:" có dòng về `CACHEABLE_API_PREFIXES` (khoảng dòng 78), thêm ngay sau dòng đó:
   - "Service worker logic lives in pure modules next to `sw-routes.ts` (`lib/sw-push.ts`) and is unit-tested; `sw.ts` only wires listeners. A push always shows a notification. A notification opens only a same-origin path (`resolveTargetUrl`), through the `NAVIGATE` message to the open window."
   - "Fixed or full-height mobile UI keeps clear of the iPhone notch and home indicator with `pt-safe` / `pb-safe` / `pl-safe` / `pr-safe` (`index.css`); `AppShell` already pads left, right and bottom, so content inside it needs nothing. An element with its own padding uses `max(<padding>, env(safe-area-inset-*))` instead."
   - "`vite.config.ts` imports `src/lib/pwa-icons.ts`; keep that file pure (no DOM, no `@/` imports). Branding icon URLs always go through `brandingIconUrl`, never a relative `/api/...` path in `index.html`."
3. **`docs/SUMMARY.md`** — bảng Architecture, thêm dòng sau `system-architecture.md`:
   `| [architecture/pwa-mobile.md](architecture/pwa-mobile.md) | PWA platform matrix (Android, desktop, iOS before and after install), iOS limits and safe-area rules, web push flow from NotificationService to the service worker and the NAVIGATE message, push prompt states, API cache allow-list, hosting (Caddy no-cache, API-origin icons, VAPID) and the manual test checklist |`
   Bảng Other: trên nhánh (tách từ `8135477`) chưa có các dòng của brainstorm 261014-0618. Ngay sau dòng `brainstorms/261013-2247-executive-dashboard/SUMMARY.md`, thêm năm dòng brainstorm, chép nguyên văn từ `docs/SUMMARY.md` của working tree trong checkout chính (các dòng bắt đầu bằng `| [brainstorms/261014-0618-mobile-manager-hub/`), rồi dòng plan:
   `| [plans/261014-2227-pwa-push-ios-foundation/SUMMARY.md](plans/261014-2227-pwa-push-ios-foundation/SUMMARY.md) | Plan for round 1 of the manager mobile hub — service worker push and notificationclick with SPA navigation, 180px apple-touch-icon and API-origin favicon, iOS meta and safe-area utilities, iOS install hint, Caddy no-cache for sw.js and the manifest, pwa-mobile.md; 3 phases |`
4. **`docs/brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md`** (đã lên nhánh ở Task 0) — mục "Next steps", cuối bước 1 thêm: " Plan: [261014-2227-pwa-push-ios-foundation](../../plans/261014-2227-pwa-push-ios-foundation/SUMMARY.md)."
5. **Kiểm** — `grep -c "pwa-mobile.md" docs/SUMMARY.md docs/architecture/system-architecture.md` / Expected: mỗi file ≥ 1; `grep -c "pt-safe" docs/code-standard/conventions.md` / Expected: ≥ 1.
6. **Commit** — `git add docs/architecture/system-architecture.md docs/code-standard/conventions.md docs/SUMMARY.md docs/brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md && git commit -m "docs: link the PWA platform notes and record the PWA conventions"`

   Không dùng `git add docs`: checkout chính có nhiều file docs chưa track không thuộc nhánh này. Khi merge PR, `docs/SUMMARY.md` trong working tree của checkout chính sẽ xung đột với bản trên nhánh ở các dòng 261014-0618; giữ bản của nhánh và thêm lại các dòng chưa commit khác.

## Verification

- `grep -n "@pwa" frontend/Caddyfile` (2 dòng) và `caddy validate` như Task 1 khi có Docker.
- `grep -c "VITE_VAPID_PUBLIC_KEY" frontend/Dockerfile` (3).
- `grep -c "^## " docs/architecture/pwa-mobile.md` (7).
- Mọi liên kết tương đối mới trong tài liệu trỏ tới file có thật: với từng liên kết thêm ở Task 2–3, `test -f <đường dẫn đã resolve>`.
- `git status --short` chỉ còn các file ngoài phạm vi plan.
- Sau deploy (người dùng làm; HTTP client bị chặn trong phiên này): DevTools → Network trên `/sw.js` và `/manifest.webmanifest` cho thấy `Cache-Control: no-cache`.

## Exit Criteria

- `Caddyfile` có `@pwa` với `no-cache`, cấu hình hợp lệ.
- `Dockerfile` truyền `VITE_VAPID_PUBLIC_KEY` vào bước build.
- Brainstorm 261014-0618 và plan này đã có trên nhánh.
- `docs/architecture/pwa-mobile.md` có đủ 7 mục; `docs/SUMMARY.md`, `system-architecture.md`, `conventions.md` trỏ tới nó và ghi quy ước mới.
- Brainstorm 261014-0618 trỏ tới plan này.
