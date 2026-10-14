# Execution Report — Nền tảng PWA (đợt 1)

**Ngày:** 2026-10-14 · **Nhánh:** `feat/pwa-push-ios` (từ `8135477`) · **Chưa push.**

## Cách chạy

- Trước khi thực thi, một agent độc lập review plan với code thật: 1 lỗi chặn, 6 cần sửa, 3 góp ý nhỏ. Tất cả đã được sửa vào plan trước khi bắt đầu (test iOS với `maxTouchPoints` không có trong jsdom; thiếu `VITE_VAPID_PUBLIC_KEY` trong `frontend/Dockerfile`; popup cập nhật và toast bị thanh home che; tài liệu trỏ tới file chưa có trên nhánh; `git add` sai; `sw.ts` không được typecheck; `renotify`; vị trí file test).
- Phase 01 và 02 chạy song song, mỗi phase một implementer (sonnet) trong worktree riêng (`.claude/worktrees/pwa-p1`, `pwa-p2`), TDD với dòng RED của từng task trong báo cáo. Controller soát diff, rồi gộp vào `feat/pwa-push-ios` (phase 01 fast-forward, phase 02 merge không xung đột). Phase 03 do controller làm trên nhánh tích hợp.

## Kết quả theo phase

| Phase | Commit | Ghi chú |
|---|---|---|
| 01 — Push hiển thị, bấm mở đúng trang | 6 (`90f3a9f` … `f90ad51`) | Không lệch plan. |
| 02 — iOS | 11 + 1 sửa (`540ba1f` … `ad871c3`, sửa `58d1c0c`), gộp bằng `7347c11` | Xem "Lệch so với plan". |
| 03 — Caddy, Dockerfile, tài liệu | 5 (`6e19183` … `3e8c161`) | Xem "Lệch so với plan". |

## Kiểm tra

- `npm run typecheck`, `npx tsc -p tsconfig.node.json`: sạch.
- `npm run build`: đạt; `dist/sw.js` có `notificationclick`; `dist/index.html` có một `apple-touch-icon` và `viewport-fit=cover`.
- `npm run test -- --testTimeout=30000 --maxWorkers=3` trên nhánh đã gộp: 2027 đạt, 3 lỗi. Ba test lỗi là ba test lỗi sẵn trên `main` (useNotificationHub "invalidates unread-count", bank-accounts-tab "adds a new account", payment-qr-page "generates and displays a QR"); implementer phase 02 đã chạy lại chúng trên checkout chính và chúng cũng lỗi ở đó.
- `npm run lint`: 3 lỗi, 13 cảnh báo, tất cả ở file không thuộc plan này (có sẵn trên `main`).
- Backend: `dotnet build` 0 lỗi; `BrandingIconTests` 6/6 đạt trên `qldonhang_integtest_pwa`. Bộ test backend đầy đủ không chạy: thay đổi backend chỉ là điều kiện kích thước trong `SettingsController.GetPwaIcon`.

## Lệch so với plan

- **Phase 02, Task 3:** commit `5d1d29b` lỡ chứa `frontend/tsconfig.app.tsbuildinfo` (file tracked, bị build sửa). Đã khôi phục bản gốc trong commit sửa.
- **Phase 02, Task 10:** `PushPermissionPrompt.test.tsx` bị ghi lại với kiểu xuống dòng trộn (CRLF/CR/LF) trong khi bản gốc là LF. Đã đưa về LF trong cùng commit sửa; diff thực còn 16 dòng thêm, 1 dòng bỏ.
- **Phase 02, Task 4:** implementer viết regex mất dấu `\` lần đầu và amend lại commit; snippet của plan đúng.
- **Phase 02, Task 10:** hai test (`it.each` với `ios-needs-install` và "keeps the iOS hint short with py-2") đã đạt trước khi sửa code, vì banner chung đã có `py-2` và `isPushPromptVisible` đã trả `true` cho trạng thái mới; test chính ("tells iOS Safari users how to install…") có RED.
- **Phase 03, Task 1b:** `grep -c VITE_VAPID_PUBLIC_KEY frontend/Dockerfile` trả `2`, không phải `3` như plan: `grep -c` đếm dòng, và dòng `ENV` chứa khóa hai lần. Nội dung đúng như plan.
- **Phase 03, Task 1:** Docker không chạy nên chưa `caddy validate`, cũng chưa build image để kiểm khóa VAPID trong bundle.

## Chưa làm, cần người dùng

1. **Đặt biến `VITE_VAPID_PUBLIC_KEY`** cho service frontend trên Railway (khóa công khai cùng cặp với `Vapid:PublicKey` của backend). Thiếu biến này thì nút "Bật thông báo" không hiện trên production.
2. **Thử tay** theo 5 mục của [pwa-mobile.md](../../architecture/pwa-mobile.md#manual-test-checklist): Chrome desktop, Chrome Android, iPhone chưa cài và đã cài, production. Chưa mục nào được thử trong phiên này.
3. **Push và PR:** nhánh chưa push lên remote `qlbanhang`.
4. Khi merge PR về `main`, `docs/SUMMARY.md` chưa commit trong checkout chính sẽ xung đột ở các dòng 261014-0618: giữ bản của nhánh, thêm lại các dòng chưa commit khác. Thư mục brainstorm 261014-0618 và thư mục plan này (chưa track trong checkout chính) cũng sẽ trùng với bản trên nhánh.
