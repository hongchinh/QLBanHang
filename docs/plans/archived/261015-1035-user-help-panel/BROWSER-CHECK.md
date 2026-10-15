# Browser check — user help panel (Task 5.1)

- Date: 2026-10-15, branch `feat/user-help-panel` at 2463006f (no code change needed after the check).
- Backend: worktree API on `localhost:5056` against the scratch database `qldonhang_help_browser` (override proven with `\dt` before the first request); `Database__AutoMigrateAndSeed=true` (admin user, roles, one branch, one warehouse; no products or customers).
- Frontend: own Vite on `localhost:5182` (`--strictPort`), `vite.config.ts` untouched. Port 5050 was not listening before or after the check; no request went to it (the context guard never fired).
- Browser: Playwright 1.60 from the main checkout. The bundled Chromium headless shell is not installed, so every run used the fallback `channel: 'msedge'`. Every context was made by `newIsolatedContext` (`serviceWorkers: 'block'`, `/api/**` routed to 5056, `/hubs/**` aborted). `navigator.serviceWorker.controller` after sign-in: `null`.
- Screenshots and scripts live in the session scratchpad `help-browser/` (not committed); names are given per scenario.
- Every screen carries the push-permission banner `Bật thông báo …` above the app (46 px at 1280 wide), so header and panel tops sit 46 px lower than the 64 px header alone. The round button at the bottom right of the screenshots is the TanStack Query devtools button (dev build only).

## Results

| # | Scenario | Result |
|---|---|---|
| 1 | Hint on the stock-out form, 1280×800 | PASS |
| 2 | F1 overlay, 1280×800 | PASS (modal case measured on `/warehouses`, see below) |
| 3 | Push mode, 1700×900 | PASS |
| 4 | Mobile 360×740 and 390×844 | PASS |
| 5 | Screen without help | PASS |
| 6 | Related and access | PASS |
| 7 | Dark mode | PASS (panel); app-wide dark issues outside the panel noted |

No defect of the help panel was found, so no fix commit.

## 1. Hint on the stock-out form, 1280×800 — PASS

Fresh context, sign in as `admin`, open `/stock-out/new`.

- Hint box: top 104, bottom 271, left 691, right 1011 (320 wide). **?** button box: top 56, bottom 100, left 966.6, right 1010.6. `hint.top (104) >= button.bottom (100)`: yes; horizontal overlap: yes (hint right edge 1011 ≈ button right edge, `align="end"`).
- `document.activeElement` when the hint appeared (MutationObserver): `BODY`; 1 s later: `BODY`, same element (`===` true). The page does not autofocus a field, so the hint took no focus.
- Totals bar `[data-testid="stock-totals-total"]` bottom 779 ≤ innerHeight 800.
- `scrollWidth` 1280 ≤ `innerWidth` 1280.
- Tab twice (focus ends on a `BUTTON`): hint still visible.
- `Ẩn`: hint gone (0 nodes). Reload: still 0 nodes.
- `localStorage['qldonhang-ui-store']`: `"helpHintSeen":{"5a62cd3f-db13-4333-a00f-28191dae5609":["stock-out.form"]}`.
- Screenshots: `s1-hint-1280.png`, `s1-after-reload.png`.

## 2. F1 overlay, 1280×800 — PASS

Same context as scenario 1 (hint dismissed).

- Field A = `#stock-voucher-at` (Ngày xuất). F1 → `help-panel` visible: width 400, left 880, right 1280 = viewport right edge, top 110, bottom 800. Title `Phiếu xuất kho`. Focus moved to the panel title `H2#help-panel-title`.
- No browser help page: context pages before/after = 1 (only the app page); a bubble-phase `keydown` listener saw `defaultPrevented: true` for F1 (`s2f1.js`).
- `[data-testid="help-panel-body"]` computed `overflow-y: auto`, scrollHeight 986 > clientHeight 596. Footer `Cập nhật 15/10/2026`: top 767, bottom 800 ≤ 800 (inside the viewport).
- Esc with the focus in the panel: panel closed (0 nodes), focus back on `#stock-voucher-at` (`=== document.activeElement` true).
- Moved-on case: focus A, F1, click field B = `#stock-partner` (Mã đối tượng; panel stays open: true), F1 → panel closed, focus stays in `#stock-partner` (true).
- Popover open, panel closed: open the grid settings popover (`Cột & phím Enter`, 1 popper wrapper), F1 → panel visible; the popover closed when the focus moved to the panel title (0 popper wrappers). Screenshots `s2-popover-open.png`, `s2-popover-f1.png`.
- Popover open, panel open: at 1280 the overlay panel covers the grid toolbar, so this half ran in push mode (1700×900, scenario 3 context): panel open, open `Cột & phím Enter` (1 popper wrapper), F1 → panel closed (0 nodes). Screenshot `s2-popover-f1-close-1700.png`.
- Modal dialog: the stock-out form has modal dialogs only for a saved voucher (`Hủy phiếu` / `Khôi phục` / `Xóa` confirm) or for the negative-stock check on save; the scratch database has no product, so none is reachable on `/stock-out/new`. Measured instead on `/warehouses` with the `Thêm kho` Radix dialog (same global `useHelpShortcut`): dialog visible, F1 → no panel (0 nodes). Screenshot `s2-modal-f1.png`.
- Screenshot of the open panel: `s2-panel-1280.png`.

## 3. Push mode, 1700×900 — PASS

- Panel left 1300, right 1700, width 400; `main#main-content` left 240, right 1300. `panel.left (1300) >= main.right (1300)`: no overlap.
- `Lưu tạm` box left 1179.6 – right 1288: `elementFromPoint` at its centre is the button or inside it: yes. `Lưu và thoát` box left 1066.2 – right 1171.6: yes.
- `scrollWidth` 1700 = `innerWidth` 1700.
- Screenshot: `s3-push-1700.png`.

## 4. Mobile 360×740 and 390×844 — PASS

`isMobile: true`, `hasTouch: true`, fresh context per viewport, `/stock-out/new`.

| measure | 360×740 | 390×844 |
|---|---|---|
| `header.scrollWidth` / `clientWidth` | 360 / 360 | 390 / 390 |
| **?** button visible | no | no |
| branch switcher width | 88 px | 118 px |
| document `scrollWidth` / `innerWidth` | 360 / 360 | 390 / 390 |
| bottom sheet top / bottom / height | 111 / 740 / 629 | 126.6 / 844 / 717.4 |
| sheet height / viewport height | 0.850 | 0.850 |
| sheet body scrollHeight / clientHeight | 886 / 534 (scrolls) | 846 / 622 (scrolls) |
| title top before / after scrolling the body | 130 / 130 (scrollTop 352) | 145.6 / 145.6 (scrollTop 224) |
| backdrop tap → panel nodes | 0 (closed) | 0 (closed) |

- The sheet opened from the user menu → `Hướng dẫn màn hình này` (the stock-out hint showed first and was dismissed with `Ẩn`).
- `/receipts/new` in a fresh context: hint visible below the avatar.
  - 360: hint top 154, left 32, right 352; avatar top 110, bottom 150, left 304, right 352 → below and overlapping horizontally, inside the viewport. Text `Lần đầu dùng Phiếu thu?` / `Mở menu tài khoản rồi chọn Hướng dẫn màn hình này.`
  - 390: hint top 134, left 62, right 382; avatar top 90, bottom 130, left 334, right 382 → below, overlapping, inside the viewport.
- Screenshots: `s4-360-stock-out.png`, `s4-360-sheet.png`, `s4-360-sheet-scrolled.png`, `s4-360-receipts-hint.png`, `s4-390-stock-out.png`, `s4-390-sheet.png`, `s4-390-sheet-scrolled.png`, `s4-390-receipts-hint.png`.

## 5. Screen without help — PASS

- `/stock-in` (list) → F1: panel title `Hướng dẫn`, body `Chưa có hướng dẫn cho màn hình này.`, then `Hướng dẫn cùng nhóm:` with the buttons `Phiếu nhập kho`, `Phiếu xuất kho`, `Tồn đầu kỳ` (sorted by `localeCompare(…, 'vi')`).
  - The plan's "the opening-stock help" is the button `Tồn đầu kỳ`: the title of `inventory-opening-stock.md` is `Tồn đầu kỳ`, matching the nav label. The expectation holds; only the wording in the plan was generic.
  - Clicking each one shows that doc (title `Phiếu nhập kho` / `Phiếu xuất kho` / `Tồn đầu kỳ`) with `Quay lại`, which returns to the list.
- `/products` → F1: `Chưa có hướng dẫn cho màn hình này.` and no `Hướng dẫn cùng nhóm:` (0 nodes).
- Screenshots: `s5-stock-in-nohelp.png`, `s5-stock-in-picked.png`, `s5-products-nohelp.png`.

## 6. Related and access — PASS

- `admin` on `/stock-out/new`, F1: `Xem thêm` lists `Phiếu thu`, `Đối trừ chứng từ`. Click `Phiếu thu` → title `Phiếu thu` (URL unchanged `/stock-out/new`); `Quay lại` → title `Phiếu xuất kho`. Screenshots `s6-admin-related.png`, `s6-admin-receipt-help.png`.
- Created user `khohelp` through `/admin/users` → `Thêm user`, role `WAREHOUSE`. Note: the admin user form lists role codes (`ADMIN`, `SALES`, `MANAGER`, `ACCOUNTANT`, `WAREHOUSE`), not the role name `Kho / giao hàng` the plan quotes; the seeded role `WAREHOUSE` (`DbSeeder.cs`) holds `stock_out.create` but neither `receipts.create` nor `debt.matching`. Screenshot `s6-create-user.png`.
- `khohelp` on `/stock-out/new`, F1: panel shows `Phiếu xuất kho`; no `Xem thêm` heading at all (both related docs filtered out), `Phiếu thu` 0 buttons, `Đối trừ chứng từ` 0 buttons. Screenshot `s6-warehouse-related.png`.

## 7. Dark mode — PASS (panel)

- The app has no theme switch, so `document.documentElement.classList.add('dark')` with the panel open at 1700×900.
- Panel background `rgb(2, 8, 23)`, left border `rgb(30, 41, 59)` 1 px (visible against the background and the page), title and body text `rgb(248, 250, 252)`. The `Lưu ý` callout, step numbers, `Insert` key chip and `Cập nhật` footer are readable.
- Outside the panel (not part of this feature, and dark mode is not offered to users): the header search box and the branch switcher keep `bg-white` while their text turns light, so their text is unreadable in dark mode.
- Screenshot: `s7-dark-panel.png`.

## Observations (no change made)

- The plan names the WAREHOUSE role by its display name `Kho / giao hàng`; the admin user form shows the code `WAREHOUSE`.
- The dev-only TanStack Query devtools button (bottom right, z-index above the panel) overlaps the bottom-right corner of the panel body and footer at 1280 and on mobile; it does not exist in production builds.

## Cleanup

- Stopped only the processes started for this check: `dotnet run` (PID 27000) and its `OrderMgmt.WebApi.exe` (PID 11056), Vite `node` (PID 15696) and its `cmd` wrapper (PID 28824).
- `netstat`: nothing listens on 5056 or 5182; 5050 did not listen before the check and does not now.
- `drop database qldonhang_help_browser`: `DROP DATABASE` (succeeded).
