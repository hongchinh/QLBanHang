# Browser check: toast at the bottom center

- Date: 2026-10-15
- Commit under test: `23f8c1fb` (branch `fix/toast-bottom-center`, worktree `toast-bottom-center`). No source file was edited.
- Browser: Microsoft Edge (`playwright-core`, `channel: 'msedge'`), `serviceWorkers: 'block'`, locale `vi-VN`, `Asia/Ho_Chi_Minh`. Desktop 1280x768; phone 390x844 with `isMobile` and `hasTouch`.
- Scripts and raw results (including the full request log of each run) are in `browser-check/`: `lib.js`, `setup.js`, `s12.js` (S1+S2), `s3.js`, `s45.js` (S4+S5), `results-*.json`.

## Summary

| # | Scenario | Result | Numbers |
|---|---|---|---|
| S1 | Action bar stays clickable under a toast | PASS | Toast `li` x=446 y=676 w=388 h=76, viewport 1280x768: its centre x is 640 (offset 0 px from 640), top y=676 is in the bottom half (384+). All 7 sticky-bar controls reach `elementFromPoint` (Quay lại, In, Cài đặt, QR chuyển khoản, Cập nhật, Hủy phiếu, Xóa). Bar y=134..170, toast starts at y=676. Clicking "Cập nhật" with the toast shown sent `PUT /api/cash-vouchers/{id}` (200). |
| S2 | Two toasts | PASS | Success toast (446,676,388x76) and a mocked 400 "Không thể lưu phiếu" toast (446,592,388x76) stack upward (`flex-col-reverse`), both centered at x=640. All 7 bar controls reach `elementFromPoint`. |
| S3 | Tall quick-add dialog | PASS (no defect), with a 13.5 px margin | See below. |
| S4 | Hover and close | PASS | Hover 6.0 s: toast still open. After moving away it closed 3.8 s later (3.87 s on the second run). The close button removed the open toast in 182-185 ms; no `li` left after 600 ms. |
| S5 | Phone 390x844 | PASS | Toast x=16 y=752 w=358 h=76 (viewport `ol` is full width 0..390, 16 px padding), at the bottom (ends y=828 of 844). All 7 bar controls reach `elementFromPoint`; the toast's close button is reachable (24x24 at 342,760). No horizontal scroll (`scrollWidth` 390). |

Defects: none.

## S3 detail

Receipt form `/receipts/new`, typed "Khách Mới Thử" in the partner field, F4 opened "Thêm nhanh khách hàng" (name prefilled). `POST /api/customers` was routed to a 400; clicking "Tạo mới" showed the "Không thể lưu" toast while the dialog stayed open.

- Dialog: x=256 y=38.4 w=768 h=691.2 (bottom edge 729.6). The dialog body scrolls inside; after the save the content was scrolled so the footer is visible (`s3-dialog.png`; the state right after the click is `s3-dialog-as-opened.png`, where the footer was scrolled below the visible body and the error banner showed).
- Toast: x=446 y=676 w=388 h=76 (right edge 834, bottom 752).
- "Hủy": x=847.5 y=668.4 w=59.6 h=36, centre (877,686): `elementFromPoint` is the button. "Tạo mới": x=915.1 y=668.4 w=83.9 h=36, centre (957,686): reaches the button.
- Neither button overlaps the toast: the footer buttons are right-aligned, so there are 13.5 px between the toast's right edge (834) and "Hủy" (847.5). The toast covers the empty left part of the footer row (and the bottom of the red error banner, text partly hidden, see observations).
- Not covered by the toast but outside the visible area at that scroll position: the "Nhóm" select and the dialog close "Đóng" (they scroll with the dialog body, y<0). This is the dialog's own scrolling, not the toast.
- Risk: the margin is thin. A narrower dialog or viewport (the dialog is `max-w` 768 wide, buttons right aligned, toast max 420 centered) would overlap sooner. At 1280x768 it does not.

## Observations (not defects of this change)

- Toasts are translucent (`bg-emerald-500/10`, `bg-destructive/10`); at the bottom center they sit over table text and the red banner, so the text underneath shows through (`s2-two-toasts.png`, `s3-dialog.png`, `s5-phone.png`). Readable, but busy.
- On the phone, the floating round button at the bottom right visually overlaps the toast's right edge; the toast close button (342..366, 760..784) is still reachable.
- On the phone a first-visit help hint popover (`data-help-hint`) blocked taps on "Lưu tạm" until its "Ẩn" button was tapped. Unrelated to the toast; the S5 script dismisses it first.

## Selectors and method

- Login through the UI as `admin` (seeded password from `appsettings.Development.json` `Seed:AdminPassword`). Customer KH0001 "Công ty TNHH Xây dựng An Phát" created through the real API (`POST /api/customers`, 200).
- Form: `/receipts/new`, partner `#cash-partner` (type `KH0001`, Enter), amount `#cash-line-amount-0`, button by name "Lưu tạm". After the save the form moves to `/receipts/{id}` and the success toast shows.
- Toast: `ol li[data-state=open]`. Action bar: `div.sticky.top-0` buttons and links, each tested at its bounding-box centre with `document.elementFromPoint`.
- S2: second toast made by routing one `PUT /api/cash-vouchers/*` to a 400 and clicking "Cập nhật" right after a successful "Cập nhật".
- S3: F4 in the partner field, dialog buttons found inside `[role=dialog]`.

## Isolation proof

- Scratch database: `qldonhang_toast_browser` (created with psql; the API log shows its migrations applied, 50 public tables and the `admin` user). `qldonhang_test`, `qldonhang` and `qldonhang_integtest*` were never connected to.
- API: `dotnet run --no-launch-profile` from the worktree's `backend/src/OrderMgmt.WebApi` with `ConnectionStrings__Default` and `ConnectionStrings__DefaultConnection` set to the scratch DB, `ASPNETCORE_URLS=http://localhost:5056`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__AutoMigrateAndSeed=true`. The process (`OrderMgmt.WebApi.exe`, path inside the worktree) listened on 5056.
- Vite: `npx vite --port 5182 --strictPort` from the worktree's `frontend` (`vite.config.ts` unchanged). Port 5050 was never used.
- Browser routing: `/api/**` rewritten to `http://localhost:5056`; `/hubs/**` aborted and the hub WebSocket closed (`routeWebSocket`), so the notification hub was offline (SignalR error lines in the console are expected).
- Request logs: every request URL of every context is stored in `results-s12.json` (1133 entries), `results-s3.json` (1109) and `results-s45.json` (2822). Occurrences of `:5050`: 0 in all three; the only hosts are `http://localhost:5182` (page) and the rewritten API calls to 5056 (made by the browser's network stack after the route, not logged as separate URLs). Each script ends with an `assertNo5050()` check that passed.
- Cleanup: stopped my API (`OrderMgmt.WebApi.exe` PID 26428, its `dotnet run` PID 6412), my Vite (node PID 29156 and its cmd PID 26592). Nothing listens on 5056 or 5182 (only TIME_WAIT sockets on 5182). `drop database qldonhang_toast_browser with (force)` returned `DROP DATABASE`; `pg_database` has 0 rows with that name.

## User's look

pending
