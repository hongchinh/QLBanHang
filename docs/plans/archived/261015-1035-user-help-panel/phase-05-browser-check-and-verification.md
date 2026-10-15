# Phase 05 — Real-browser check and final verification

**Status:** [x] done (2026-10-15)
**Complexity:** M

## Objective

Prove in a real browser what jsdom cannot: the panel's push and overlay widths, the hint's position and that it never takes the focus, the full-height stock-out form with the hint open, and the mobile header at 360 and 390 px (P3). Then run the final verification and report.

## Files

- `docs/plans/261015-1035-user-help-panel/BROWSER-CHECK.md` (new)
- Screenshots and scripts stay in the session scratchpad (`$SCRATCH/help-browser/`), not committed.

## Tasks

### Task 5.1 — Browser check on a scratch database

**Isolation rules.** A developer backend usually listens on `localhost:5050` against `qldonhang_test`, and `frontend/vite.config.ts` proxies to 5050. Never send that process a request, never stop it, never edit `vite.config.ts`.

Setup:
1. `PGPASSWORD=1 "/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c "create database qldonhang_help_browser"`. Never use `qldonhang_test`, `qldonhang` or any `qldonhang_integtest*` database.
2. Run the API from the worktree on port 5056 (background), with both connection strings set (`DefaultConnection` wins over `Default`):
   ```bash
   CS="Host=localhost;Port=5432;Database=qldonhang_help_browser;Username=postgres;Password=1"
   ConnectionStrings__Default="$CS" ConnectionStrings__DefaultConnection="$CS" \
   ASPNETCORE_URLS=http://localhost:5056 ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrateAndSeed=true \
   dotnet run --project "$WT/backend/src/OrderMgmt.WebApi" --no-launch-profile
   ```
   Before the first request prove the override: `psql … -d qldonhang_help_browser -c "\dt"` lists tables. If it does not, stop and report.
3. Start your own Vite from the worktree: `cd "$WT/frontend" && npx vite --port 5182 --strictPort` (background; pick another 51xx if taken, `netstat -ano | grep :51`).
4. Browser: `const { chromium } = require('module').createRequire('D:/Projects/QLBanHang/package.json')('playwright');` (Playwright 1.60 of the main checkout, Chromium installed); fall back to `chromium.launch({ channel: 'msedge' })`. Create **every** context through one helper that blocks service workers and routes the API to your backend. The dev service worker (`devOptions.enabled` in `vite.config.ts`) fetches `/api/lookups/`, `/api/banks` and `/api/settings/branding` itself (`sw.ts`); Playwright routing does not see those fetches, so without `serviceWorkers: 'block'` they reach Vite's proxy and the developer backend on 5050, under a `:5182` URL the guard below cannot catch.
   ```js
   async function newIsolatedContext(browser, options = {}) {
     const context = await browser.newContext({ ...options, serviceWorkers: 'block' });
     await context.route('**/api/**', (r) => r.continue({ url: r.request().url().replace(/^http:\/\/localhost:\d+/, 'http://localhost:5056') }));
     await context.route('**/hubs/**', (r) => r.abort());
     context.on('request', (q) => { if (q.url().includes(':5050')) throw new Error('request to 5050: ' + q.url()); });
     return context;
   }
   ```
   Check once, after signing in, that `await page.evaluate(() => navigator.serviceWorker.controller)` is `null`.
5. Sign in through the login page as `admin` / `Admin@123`.
6. Afterwards: kill your backend and Vite, check with `netstat` that 5056 and 5182 are free and 5050 still listens if it did before, and `psql … -c "drop database qldonhang_help_browser"` (report if the drop fails).

Scenarios (each in `BROWSER-CHECK.md` with pass/fail, the measured numbers and the screenshot name):

1. **Hint on the stock-out form, 1280×800.** Open `/stock-out/new` with a fresh `localStorage` (new context). Measure: the hint (`[data-help-hint]`) is visible and its box sits below the **?** button (`hint.top >= button.bottom`, horizontal overlap with the button); `document.activeElement` is the same element before and 1 s after the hint appears; the totals bar's bottom (`[data-testid="stock-totals-total"]`, `boundingBox().y + height`) is within the viewport; `document.documentElement.scrollWidth <= innerWidth`. Press Tab twice → the hint is still visible (P10: a focus move does not close it). Click `Ẩn` → the hint is gone; reload → it stays gone; `localStorage['qldonhang-ui-store']` holds `helpHintSeen` with `stock-out.form`.
2. **F1 overlay, 1280×800** (same context as scenario 1, so the hint is already dismissed). Click into the first field, press F1 → `help-panel` visible, 400 px wide (±1), right edge = viewport right edge, title `Phiếu xuất kho`, the browser help page did not open (no new page in the context); `[data-testid="help-panel-body"]` has computed `overflow-y: auto` and the footer `Cập nhật …` is inside the viewport. Esc with the focus in the panel → closed and the focus back in the field. Focus restore when the user moved on: focus field A, F1, click field B (outside the panel), F1 → closed and the focus stays in B. F1 with a popover open (the grid settings popover of `stock-grid-settings-popover.tsx`, or the partner suggestion list) → the panel toggles. F1 with a modal Radix dialog open (a picker or confirm dialog on the page if one exists; otherwise skip and note it) → no panel.
3. **Push mode, 1700×900.** F1 → the panel's left edge ≥ `main#main-content`'s right edge (no overlap); the voucher action buttons (`Lưu tạm`, `Lưu và thoát`) are hit-testable (`document.elementFromPoint` at each button's centre is the button or inside it).
4. **Mobile 360×740 and 390×844** (`isMobile: true`, `hasTouch: true`). On `/stock-out/new`: `header.scrollWidth <= header.clientWidth`; no visible **?** button; the branch switcher is at least 80 px wide (record the width); open the user menu → `Hướng dẫn màn hình này` → the bottom sheet shows, at most 85 % of the viewport height, its body scrolls (`scrollHeight > clientHeight` at 360×740, or note that the doc fits) while the sheet's title stays in place; tapping the backdrop closes it. Open `/receipts/new` in a fresh context → the hint appears anchored below the avatar (its box overlaps the avatar horizontally).
5. **Screen without help.** `/stock-in` (the list; round 1 covers only the form) → F1 → `Chưa có hướng dẫn cho màn hình này.` and, under `Hướng dẫn cùng nhóm:`, buttons for `Phiếu nhập kho`, `Phiếu xuất kho` and the opening-stock help (group "Kho"); clicking one shows it. `/products` → F1 → the same sentence and no `Hướng dẫn cùng nhóm:` list (no catalog screen has help yet).
6. **Related and access.** On `/stock-out/new` with the panel open, click `Phiếu thu` under "Xem thêm" → the panel shows `Phiếu thu` and `Quay lại` returns. Sign in as a user without `receipts.create` (create one through the admin user screen with the role shown as `Kho / giao hàng`, code `WAREHOUSE`: it holds `stock_out.create` but not `receipts.create` or `debt.matching`, `DbSeeder.cs:226-233`) → neither `Phiếu thu` nor `Đối trừ chứng từ` is listed.
7. **Dark mode.** Toggle the app's dark theme if the app has a switch (`document.documentElement.classList.add('dark')` otherwise) and screenshot the open panel: text readable, borders visible.

Fix every real defect with a failing test first (separate commits), then rerun the scenario. Commit the notes: `docs(plan): browser check notes for the user help panel`.

### Task 5.2 — Final verification

1. Run the **Final Verification** block of `SUMMARY.md` in full; record the outputs.
2. Hygiene, from `$WT`: `git diff <base>..HEAD --stat | grep -E "tsbuildinfo|TestResults|\.trx"` is empty; the line-ending check of "Executor conventions"; `git grep -n "lucide-react" -- frontend/src/components/help frontend/src/features/help` is empty; `git grep -n "from '@/features/help/content-index'\|from './content-index'" -- frontend/src ':!frontend/src/features/help/load-help.ts' ':!frontend/src/features/help/*.test.*'` is empty (the chunk is imported only by the loader).
3. Re-check the base: `git fetch qlbanhang main` (retry with the sandbox disabled if it fails), `git log --oneline HEAD..qlbanhang/main`. If it has commits the branch lacks:
   - `git merge qlbanhang/main`. A conflict in `App.tsx`: keep this branch's shell and apply the upstream route change inside `app-routes.tsx`; any other conflict: stop and report. A new upstream route makes the guard test ask for a help file or a `HELP_PENDING` entry (add it to `HELP_PENDING`).
   - Bring the help in line (P12: this branch merges second). `npm run help:stale`; for every round-1 key with a `Code đổi ngày …` line (`sources-newer-than-help`), rerun the `help-content` skill on the merged code for that key, redo its label check in `CONTENT-CHECK.md`, set `updated` to today and commit `docs(help): follow <area> changes from main`. Rerun `npm run help:stale` → no `Code đổi ngày` line for a round-1 key.
   - If the merge changed the stock-out or the cash voucher form, rerun scenarios 1–3 and 6 of Task 5.1 on the merged code and update `BROWSER-CHECK.md`.
   - Rerun the full frontend suite.
4. Tick the `Status` of every phase file and the checkboxes of `SUMMARY.md`.
5. Report: what is done, the baseline-only failures, the browser-check result (with the branch switcher widths of P3), the help files written, `HELP_PENDING` size, the P12 note for the cash form and QR plans. Push and PR are the user's call (`finishing-a-development-branch`).

## Verification

- Everything in Task 5.2.

## Exit Criteria

- `BROWSER-CHECK.md` records every scenario as pass (or a justified skip) with numbers.
- The final verification passes with only baseline failures; the help chunk is separate in the build and precached.
- After the last merge of `qlbanhang/main`, `help:stale` prints no `Code đổi ngày` line for a round-1 key.
